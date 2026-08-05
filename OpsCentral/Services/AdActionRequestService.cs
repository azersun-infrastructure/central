using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsCentral.Data;
using OpsCentral.Models.Entities;
using OpsCentral.Options;
using OpsCentral.Services.Dispatch;

namespace OpsCentral.Services;

public class AdActionRequestService(
    AppDbContext db,
    IAdActionDispatchRouter router,
    IOptions<AppOptions> appOptions,
    IOptions<ReconciliationOptions> reconciliationOptions) : IAdActionRequestService
{
    private readonly AppOptions _appOptions = appOptions.Value;
    private readonly ReconciliationOptions _reconciliationOptions = reconciliationOptions.Value;

    public async Task<AdActionRequest> SubmitActionAsync(
        AdActionType actionType,
        string input,
        string requestedByUpnOrUsername,
        string requestedByAuthSource,
        CancellationToken ct)
    {
        var request = new AdActionRequest
        {
            ActionType = actionType.ToString(),
            Input = input,
            RequestedByUpnOrUsername = requestedByUpnOrUsername,
            RequestedByAuthSource = requestedByAuthSource,
            Status = JobStatus.Pending.ToString()
        };

        db.AdActionRequests.Add(request);
        await db.SaveChangesAsync(ct);

        var target = router.ResolveTarget(actionType);
        request.DispatchTarget = target.ToString();
        request.Status = JobStatus.Dispatching.ToString();
        await db.SaveChangesAsync(ct);

        var callbackUrl = $"{_appOptions.PublicBaseUrl.TrimEnd('/')}/api/webhooks/{target.ToString().ToLowerInvariant()}/callback";
        var context = new AdActionDispatchContext(request.Id, actionType, input, requestedByUpnOrUsername, callbackUrl);

        var dispatcher = router.ResolveDispatcher(target);
        var result = await dispatcher.DispatchAsync(context, ct);

        var now = DateTimeOffset.UtcNow;
        request.DispatchedAtUtc = now;

        if (result.Success && result.IsSynchronous)
        {
            // Dispatcher already produced the final result (e.g. n8n's synchronous webhook) —
            // no Running/poll phase, and CallbackReceivedAtUtc keeps reconciliation from touching it.
            request.Status = (result.FinalStatus ?? JobStatus.Succeeded).ToString();
            request.ActionNote = result.ActionNote;
            request.RawResultPayload = result.RawPayload;
            request.ExternalJobUrl = result.ExternalJobUrl;
            request.CallbackReceivedAtUtc = now;
        }
        else if (result.Success)
        {
            request.Status = JobStatus.Running.ToString();
            request.ExternalJobId = result.ExternalJobId;
            request.ExternalJobUrl = result.ExternalJobUrl;
            request.TimeoutAtUtc = now.AddSeconds(_reconciliationOptions.JobTimeoutSeconds);
            request.ActionNote = "Dispatched, awaiting result.";
        }
        else
        {
            request.Status = JobStatus.DispatchFailed.ToString();
            request.ErrorDetail = result.ErrorMessage;
            request.ActionNote = "Dispatch failed.";
        }

        db.AdActionEvents.Add(new AdActionEvent
        {
            AdActionRequestId = request.Id,
            Source = AdActionEventSource.Dispatch,
            StatusAtEvent = request.Status,
            Message = result.Success ? (result.IsSynchronous ? result.ActionNote : "Dispatched successfully.") : result.ErrorMessage,
            RawPayload = result.IsSynchronous ? result.RawPayload : null
        });

        await db.SaveChangesAsync(ct);

        return request;
    }

    public async Task<List<AdActionRequest>> GetRecentAsync(int take, CancellationToken ct)
    {
        // Ordered client-side: the SQLite provider can't translate ORDER BY on DateTimeOffset.
        var all = await db.AdActionRequests.ToListAsync(ct);
        return all.OrderByDescending(r => r.RequestedAtUtc).Take(take).ToList();
    }

    public Task<AdActionRequest?> GetWithEventsAsync(Guid id, CancellationToken ct) =>
        db.AdActionRequests
            // Ordered by the auto-increment Id (== insertion/chronological order) rather than
            // OccurredAtUtc, since the SQLite provider can't translate ORDER BY on DateTimeOffset.
            .Include(r => r.Events.OrderBy(e => e.Id))
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
