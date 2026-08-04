using OpsCentral.Models.Entities;

namespace OpsCentral.Services.Dispatch;

public record AdActionDispatchContext(
    Guid CorrelationId,
    AdActionType ActionType,
    string Input,
    string RequestedBy,
    string CallbackUrl);

public record DispatchResult(bool Success, string? ExternalJobId, string? ExternalJobUrl, string? ErrorMessage)
{
    public static DispatchResult Ok(string? jobId, string? jobUrl) => new(true, jobId, jobUrl, null);
    public static DispatchResult Failed(string error) => new(false, null, null, error);
}

public record JobStatusResult(JobStatus Status, string? ActionNote, string? RawPayload);
