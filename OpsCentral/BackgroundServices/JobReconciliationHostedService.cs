using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsCentral.Data;
using OpsCentral.Models.Entities;
using OpsCentral.Options;
using OpsCentral.Services.Dispatch;

namespace OpsCentral.BackgroundServices;

/// <summary>
/// Fallback for jobs whose Jenkins/Azure Automation callback never arrives: polls each
/// dispatcher's PollStatusAsync for any request that's timed out without a callback.
/// </summary>
public class JobReconciliationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReconciliationOptions> options,
    ILogger<JobReconciliationHostedService> logger) : BackgroundService
{
    private readonly ReconciliationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job reconciliation pass failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<IAdActionDispatchRouter>();

        var now = DateTimeOffset.UtcNow;
        var pendingStatuses = new[] { JobStatus.Dispatching.ToString(), JobStatus.Running.ToString() };

        // TimeoutAtUtc is filtered client-side: the SQLite provider can't translate
        // DateTimeOffset <= comparisons combined with the other predicates here.
        var candidates = await db.AdActionRequests
            .Where(r => pendingStatuses.Contains(r.Status) && r.CallbackReceivedAtUtc == null)
            .ToListAsync(ct);

        var dueRequests = SelectDueRequests(candidates, now);

        foreach (var request in dueRequests)
        {
            await PollOneAsync(db, router, request, ct);
        }

        if (dueRequests.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Pure filter, split out from ReconcileOnceAsync purely so it's unit-testable without a DB.</summary>
    internal static List<AdActionRequest> SelectDueRequests(IEnumerable<AdActionRequest> candidates, DateTimeOffset now) =>
        candidates.Where(r => r.TimeoutAtUtc is not null && r.TimeoutAtUtc <= now).ToList();

    private async Task PollOneAsync(AppDbContext db, IAdActionDispatchRouter router, AdActionRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<DispatchTarget>(request.DispatchTarget, out var target))
        {
            logger.LogWarning("AdActionRequest {Id} has unrecognized DispatchTarget '{Target}'.", request.Id, request.DispatchTarget);
            return;
        }

        var dispatcher = router.ResolveDispatcher(target);

        JobStatusResult result;
        try
        {
            result = await dispatcher.PollStatusAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Polling AdActionRequest {Id} via {Target} failed.", request.Id, target);
            return;
        }

        request.LastPolledAtUtc = DateTimeOffset.UtcNow;
        request.PollAttemptCount++;
        request.Status = result.Status.ToString();
        request.ActionNote = result.ActionNote;
        request.RawResultPayload = result.RawPayload;

        var isTerminal = result.Status is JobStatus.Succeeded or JobStatus.Failed;

        if (!isTerminal && request.PollAttemptCount > _options.MaxPollAttempts)
        {
            request.Status = JobStatus.TimedOut.ToString();
            request.ActionNote = $"No result after {request.PollAttemptCount} poll attempts.";
        }
        else if (!isTerminal)
        {
            // Push the timeout out so the next reconciliation pass polls again.
            request.TimeoutAtUtc = DateTimeOffset.UtcNow.AddSeconds(_options.PollIntervalSeconds);
        }

        db.AdActionEvents.Add(new AdActionEvent
        {
            AdActionRequestId = request.Id,
            Source = AdActionEventSource.Poll,
            StatusAtEvent = request.Status,
            Message = result.ActionNote,
            RawPayload = result.RawPayload
        });
    }
}
