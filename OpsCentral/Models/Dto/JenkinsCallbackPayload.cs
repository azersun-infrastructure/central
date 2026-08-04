namespace OpsCentral.Models.Dto;

/// <summary>Body posted by a Jenkins job to /api/webhooks/jenkins/callback on completion.</summary>
public class JenkinsCallbackPayload
{
    public Guid CorrelationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ActionNote { get; set; }
    public string? BuildUrl { get; set; }
    public string? RawPayload { get; set; }
}
