using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsCentral.Data;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Pages.Account;

/// <summary>
/// A plain Razor Page (not a Blazor component) because cookie sign-in requires setting a
/// Set-Cookie response header, which can't be done from inside a live Blazor Server
/// SignalR circuit.
/// </summary>
public class LocalLoginModel(AppDbContext db, IOptions<LocalAdminFallbackOptions> options) : PageModel
{
    private readonly LocalAdminFallbackOptions _options = options.Value;

    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Username and password are required.";
            return Page();
        }

        var account = await db.LocalAdminAccounts
            .FirstOrDefaultAsync(a => a.Username == Username && a.IsActive);

        if (account is null)
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        if (account.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
        {
            ErrorMessage = $"Account locked until {lockedUntil:u}.";
            return Page();
        }

        var verifyResult = new PasswordHasher<LocalAdminAccount>()
            .VerifyHashedPassword(account, account.PasswordHash, Password);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= _options.MaxFailedLoginAttempts)
            {
                account.LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(_options.LockoutMinutes);
                account.FailedLoginAttempts = 0;
            }

            await db.SaveChangesAsync();

            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        account.FailedLoginAttempts = 0;
        account.LockedUntilUtc = null;
        account.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, account.Username),
            new("AuthSource", "Local")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl);
    }
}
