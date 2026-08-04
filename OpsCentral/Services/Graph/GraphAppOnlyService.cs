using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace OpsCentral.Services.Graph;

public class GraphAppOnlyService(GraphServiceClient graphClient) : IGraphAppOnlyService
{
    public Task<User?> GetUserByUpnAsync(string userPrincipalName, CancellationToken ct) =>
        graphClient.Users[userPrincipalName].GetAsync(cancellationToken: ct);
}
