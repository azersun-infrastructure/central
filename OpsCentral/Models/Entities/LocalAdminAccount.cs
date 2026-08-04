namespace OpsCentral.Models.Entities;

/// <summary>Fallback login used when Entra ID SSO is unreachable. Single seeded account, not a full user system.</summary>
public class LocalAdminAccount
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
