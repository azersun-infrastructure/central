namespace OpsCentral.Options;

public class N8nOptions
{
    public const string SectionName = "N8n";

    /// <summary>ActionType (e.g. "Search") -> n8n webhook URL.</summary>
    public Dictionary<string, string> WebhookUrls { get; set; } = [];
}
