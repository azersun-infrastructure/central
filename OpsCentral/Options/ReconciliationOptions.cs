namespace OpsCentral.Options;

public class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    public int PollIntervalSeconds { get; set; } = 30;
    public int JobTimeoutSeconds { get; set; } = 180;
    public int MaxPollAttempts { get; set; } = 20;
}
