using Microsoft.Graph.Models;

namespace OpsCentral.Services.Graph;

/// <summary>
/// General-purpose Microsoft Graph plumbing (app-only auth). Not consumed by any UI in the
/// MVP — exercised only in verification to confirm the wiring works before the M365 module
/// is built on top of it.
/// </summary>
public interface IGraphAppOnlyService
{
    Task<User?> GetUserByUpnAsync(string userPrincipalName, CancellationToken ct);
}
