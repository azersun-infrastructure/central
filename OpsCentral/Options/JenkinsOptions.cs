namespace OpsCentral.Options;

public class JenkinsOptions
{
    public const string SectionName = "Jenkins";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string CallbackSharedSecret { get; set; } = string.Empty;

    /// <summary>ActionType (e.g. "Unlock") -> Jenkins job name.</summary>
    public Dictionary<string, string> Jobs { get; set; } = [];
}
