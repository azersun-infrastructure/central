namespace OpsCentral.Options;

public class LocalAdminFallbackOptions
{
    public const string SectionName = "LocalAdminFallback";

    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
