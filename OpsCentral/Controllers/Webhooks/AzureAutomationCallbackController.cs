using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsCentral.Data;
using OpsCentral.Models.Dto;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Controllers.Webhooks;

/// <summary>
/// Receives job-completion callbacks from Azure Automation runbooks. Excluded from the
/// app-wide auth fallback policy and validated with a static shared-secret header instead.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/azureautomation")]
public class AzureAutomationCallbackController(AppDbContext db, IOptions<AzureAutomationOptions> options) : ControllerBase
{
    private readonly AzureAutomationOptions _options = options.Value;

    [HttpPost("callback")]
    public async Task<IActionResult> Callback(
        [FromBody] AzureAutomationCallbackPayload payload,
        [FromHeader(Name = "X-Callback-Token")] string? callbackToken,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.CallbackSharedSecret) ||
            !string.Equals(callbackToken, _options.CallbackSharedSecret, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var request = await db.AdActionRequests.FirstOrDefaultAsync(r => r.Id == payload.CorrelationId, ct);
        if (request is null)
        {
            return NotFound();
        }

        request.CallbackReceivedAtUtc = DateTimeOffset.UtcNow;
        request.Status = payload.Status;
        request.ActionNote = payload.ActionNote;
        request.ExternalJobUrl = payload.JobUrl ?? request.ExternalJobUrl;
        request.RawResultPayload = payload.RawPayload;

        db.AdActionEvents.Add(new AdActionEvent
        {
            AdActionRequestId = request.Id,
            Source = AdActionEventSource.Callback,
            StatusAtEvent = payload.Status,
            Message = payload.ActionNote,
            RawPayload = payload.RawPayload
        });

        await db.SaveChangesAsync(ct);

        return Ok();
    }
}
