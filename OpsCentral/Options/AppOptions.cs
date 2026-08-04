namespace OpsCentral.Options;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Externally reachable base URL used to build callback URLs sent to Jenkins/Azure Automation.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
