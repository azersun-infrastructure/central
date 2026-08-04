namespace OpsCentral.Options;

/// <summary>
/// Deliberately a separate Entra app registration from the SSO login app (least-privilege:
/// keeps Graph app-only permissions out of the blast radius of the interactive-login app).
/// </summary>
public class GraphAppOnlyOptions
{
    public const string SectionName = "GraphAppOnly";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
