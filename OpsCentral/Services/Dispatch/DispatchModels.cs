using OpsCentral.Models.Entities;

namespace OpsCentral.Services.Dispatch;

public record AdActionDispatchContext(
    Guid CorrelationId,
    AdActionType ActionType,
    string Input,
    string RequestedBy,
    string CallbackUrl);

public record DispatchResult(
    bool Success,
    string? ExternalJobId,
    string? ExternalJobUrl,
    string? ErrorMessage,
    bool IsSynchronous = false,
    JobStatus? FinalStatus = null,
    string? ActionNote = null,
    string? RawPayload = null)
{
    public static DispatchResult Ok(string? jobId, string? jobUrl) => new(true, jobId, jobUrl, null);
    public static DispatchResult Failed(string error) => new(false, null, null, error);

    /// <summary>For dispatchers that complete synchronously within DispatchAsync (e.g. n8n's Search
    /// webhook, which returns the result directly in the HTTP response) — no Running/poll phase needed.</summary>
    public static DispatchResult Completed(JobStatus status, string? actionNote, string? rawPayload, string? externalJobUrl = null) =>
        new(true, null, externalJobUrl, null, IsSynchronous: true, FinalStatus: status, ActionNote: actionNote, RawPayload: rawPayload);
}

public record JobStatusResult(JobStatus Status, string? ActionNote, string? RawPayload);
