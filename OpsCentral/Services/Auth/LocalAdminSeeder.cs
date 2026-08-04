using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsCentral.Data;
using OpsCentral.Models.Entities;

namespace OpsCentral.Services.Auth;

/// <summary>
/// Seeds the single local fallback admin account from env vars at startup. There is no
/// password-rotation UI in the MVP — rotating requires re-seeding (with the table emptied)
/// or a direct DB update.
/// </summary>
public static class LocalAdminSeeder
{
    public const string UsernameEnvVar = "OPSCENTRAL_LOCAL_ADMIN_USERNAME";
    public const string PasswordEnvVar = "OPSCENTRAL_LOCAL_ADMIN_PASSWORD";

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.LocalAdminAccounts.AnyAsync(ct))
        {
            return;
        }

        var username = Environment.GetEnvironmentVariable(UsernameEnvVar);
        var password = Environment.GetEnvironmentVariable(PasswordEnvVar);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No local fallback admin account exists and {UsernameEnvVar}/{PasswordEnvVar} are not set — " +
                "sign-in will only be possible via Entra ID SSO until one is seeded.",
                UsernameEnvVar, PasswordEnvVar);
            return;
        }

        var account = new LocalAdminAccount { Username = username };
        account.PasswordHash = new PasswordHasher<LocalAdminAccount>().HashPassword(account, password);

        db.LocalAdminAccounts.Add(account);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded local fallback admin account '{Username}'.", username);
    }
}
