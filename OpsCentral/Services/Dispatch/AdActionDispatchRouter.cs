using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Services.Dispatch;

public class AdActionDispatchRouter(IServiceProvider serviceProvider, IOptions<AdActionRoutingOptions> routingOptions)
    : IAdActionDispatchRouter
{
    private readonly AdActionRoutingOptions _routing = routingOptions.Value;

    public DispatchTarget ResolveTarget(AdActionType actionType)
    {
        if (!_routing.TryGetValue(actionType.ToString(), out var targetName))
        {
            throw new InvalidOperationException(
                $"No AdActionRouting entry configured for action type '{actionType}'.");
        }

        if (!Enum.TryParse<DispatchTarget>(targetName, out var target))
        {
            throw new InvalidOperationException(
                $"AdActionRouting entry for '{actionType}' has an unrecognized target '{targetName}'.");
        }

        return target;
    }

    public IAdActionDispatcher ResolveDispatcher(DispatchTarget target) =>
        serviceProvider.GetRequiredKeyedService<IAdActionDispatcher>(target);
}
