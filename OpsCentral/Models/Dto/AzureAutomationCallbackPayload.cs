namespace OpsCentral.Models.Dto;

/// <summary>Body posted by an Azure Automation runbook to /api/webhooks/azureautomation/callback on completion.</summary>
public class AzureAutomationCallbackPayload
{
    public Guid CorrelationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ActionNote { get; set; }
    public string? JobUrl { get; set; }
    public string? RawPayload { get; set; }
}
