namespace OpsCentral.Models.Entities;

public enum AdActionEventSource
{
    Dispatch,
    Callback,
    Poll,
    System
}

/// <summary>
/// Append-only history for an <see cref="AdActionRequest"/>. The parent row always
/// reflects the latest state; this table is the full log shown in the detail modal.
/// </summary>
public class AdActionEvent
{
    public long Id { get; set; }

    public Guid AdActionRequestId { get; set; }
    public AdActionRequest? AdActionRequest { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public AdActionEventSource Source { get; set; }
    public string StatusAtEvent { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? RawPayload { get; set; }
}
