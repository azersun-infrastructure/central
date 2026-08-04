namespace OpsCentral.Models.Entities;

/// <summary>
/// One row per submitted action: doubles as the audit log row shown in the
/// Request Result Table and as the async job lifecycle tracker.
/// ActionType/DispatchTarget/Status are stored as strings (not native enum
/// columns) so future modules can introduce new action keys without a migration.
/// </summary>
public class AdActionRequest
{
    /// <summary>Also sent to Jenkins/Azure Automation as the CorrelationId.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ActionType { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;

    public string RequestedByUpnOrUsername { get; set; } = string.Empty;
    public string RequestedByAuthSource { get; set; } = string.Empty;

    public string DispatchTarget { get; set; } = string.Empty;
    public string? ExternalJobId { get; set; }
    public string? ExternalJobUrl { get; set; }

    public string Status { get; set; } = Entities.JobStatus.Pending.ToString();
    public string? ActionNote { get; set; }
    public string? RawResultPayload { get; set; }
    public string? ErrorDetail { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public DateTimeOffset? CallbackReceivedAtUtc { get; set; }
    public DateTimeOffset? LastPolledAtUtc { get; set; }
    public DateTimeOffset? TimeoutAtUtc { get; set; }

    public int PollAttemptCount { get; set; }

    public List<AdActionEvent> Events { get; set; } = [];
}
