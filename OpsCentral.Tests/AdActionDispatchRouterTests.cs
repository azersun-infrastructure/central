using MsOptions = Microsoft.Extensions.Options.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpsCentral.Models.Entities;
using OpsCentral.Options;
using OpsCentral.Services.Dispatch;

namespace OpsCentral.Tests;

public class AdActionDispatchRouterTests
{
    [Theory]
    [InlineData("Unlock", DispatchTarget.Jenkins)]
    [InlineData("Search", DispatchTarget.AzureAutomation)]
    public void ResolveTarget_ReturnsConfiguredTarget(string actionType, DispatchTarget expected)
    {
        var routing = new AdActionRoutingOptions
        {
            ["Unlock"] = "Jenkins",
            ["Search"] = "AzureAutomation"
        };
        var router = CreateRouter(routing, new ServiceCollection().BuildServiceProvider());

        var result = router.ResolveTarget(Enum.Parse<AdActionType>(actionType));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveTarget_ThrowsWhenActionTypeNotConfigured()
    {
        var router = CreateRouter(new AdActionRoutingOptions(), new ServiceCollection().BuildServiceProvider());

        Assert.Throws<InvalidOperationException>(() => router.ResolveTarget(AdActionType.Unlock));
    }

    [Fact]
    public void ResolveTarget_ThrowsWhenConfiguredTargetNameIsInvalid()
    {
        var routing = new AdActionRoutingOptions { ["Unlock"] = "NotARealTarget" };
        var router = CreateRouter(routing, new ServiceCollection().BuildServiceProvider());

        Assert.Throws<InvalidOperationException>(() => router.ResolveTarget(AdActionType.Unlock));
    }

    [Fact]
    public void ResolveDispatcher_ReturnsTheDispatcherRegisteredUnderThatKey()
    {
        var jenkinsDispatcher = new FakeDispatcher(DispatchTarget.Jenkins);
        var azureAutomationDispatcher = new FakeDispatcher(DispatchTarget.AzureAutomation);

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAdActionDispatcher>(DispatchTarget.Jenkins, jenkinsDispatcher);
        services.AddKeyedSingleton<IAdActionDispatcher>(DispatchTarget.AzureAutomation, azureAutomationDispatcher);
        var provider = services.BuildServiceProvider();

        var router = CreateRouter(new AdActionRoutingOptions(), provider);

        Assert.Same(jenkinsDispatcher, router.ResolveDispatcher(DispatchTarget.Jenkins));
        Assert.Same(azureAutomationDispatcher, router.ResolveDispatcher(DispatchTarget.AzureAutomation));
    }

    private static AdActionDispatchRouter CreateRouter(AdActionRoutingOptions routing, IServiceProvider serviceProvider) =>
        new(serviceProvider, MsOptions.Create(routing));

    private class FakeDispatcher(DispatchTarget target) : IAdActionDispatcher
    {
        public DispatchTarget Target => target;

        public Task<DispatchResult> DispatchAsync(AdActionDispatchContext context, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<JobStatusResult> PollStatusAsync(AdActionRequest request, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
