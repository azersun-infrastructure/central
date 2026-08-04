namespace OpsCentral.Options;

public class AzureAutomationOptions
{
    public const string SectionName = "AzureAutomation";

    /// <summary>ARM resource ID of the Automation Account, used by PollStatusAsync to list jobs.</summary>
    public string AutomationAccountResourceId { get; set; } = string.Empty;
    public string CallbackSharedSecret { get; set; } = string.Empty;

    /// <summary>Service principal used to call the ARM Jobs API for polling (separate from GraphAppOnly's app registration).</summary>
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>ActionType (e.g. "Search") -> runbook webhook URL.</summary>
    public Dictionary<string, string> WebhookUrls { get; set; } = [];
}
