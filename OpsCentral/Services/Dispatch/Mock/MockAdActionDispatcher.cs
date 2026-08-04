using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsCentral.Models.Dto;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Services.Dispatch.Mock;

/// <summary>
/// Local-dev stand-in for the real Jenkins/Azure Automation dispatchers (enabled via
/// Dispatch:UseMock). Instead of mutating the DB directly, it loops back through the app's
/// own real callback HTTP endpoint a few seconds after dispatch — exercising the identical
/// code path the real integrations will hit (callback controller, correlation lookup, event
/// logging) before any real Jenkins/Azure Automation access exists.
/// </summary>
public class MockAdActionDispatcher(
    IHttpClientFactory httpClientFactory,
    ILogger<MockAdActionDispatcher> logger,
    IOptions<JenkinsOptions> jenkinsOptions,
    IOptions<AzureAutomationOptions> azureAutomationOptions,
    DispatchTarget simulatedTarget) : IAdActionDispatcher
{
    private static readonly TimeSpan LoopbackDelay = TimeSpan.FromSeconds(4);

    public DispatchTarget Target => simulatedTarget;

    public Task<DispatchResult> DispatchAsync(AdActionDispatchContext context, CancellationToken ct)
    {
        var mockJobId = $"mock-{Guid.NewGuid():N}";

        _ = SendLoopbackCallbackAsync(context, mockJobId);

        return Task.FromResult(DispatchResult.Ok(mockJobId, context.CallbackUrl));
    }

    public Task<JobStatusResult> PollStatusAsync(AdActionRequest request, CancellationToken ct) =>
        Task.FromResult(new JobStatusResult(JobStatus.Running, "Mock dispatcher: awaiting loopback callback.", null));

    private async Task SendLoopbackCallbackAsync(AdActionDispatchContext context, string mockJobId)
    {
        try
        {
            await Task.Delay(LoopbackDelay);

            var client = httpClientFactory.CreateClient("MockLoopback");

            HttpResponseMessage response;
            if (simulatedTarget == DispatchTarget.Jenkins)
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, context.CallbackUrl)
                {
                    Content = JsonContent.Create(new JenkinsCallbackPayload
                    {
                        CorrelationId = context.CorrelationId,
                        Status = JobStatus.Succeeded.ToString(),
                        ActionNote = $"[mock] {context.ActionType} completed for '{context.Input}'.",
                        BuildUrl = $"https://jenkins.example/mock/{mockJobId}"
                    })
                };
                httpRequest.Headers.Add("X-Callback-Token", jenkinsOptions.Value.CallbackSharedSecret);
                response = await client.SendAsync(httpRequest);
            }
            else
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, context.CallbackUrl)
                {
                    Content = JsonContent.Create(new AzureAutomationCallbackPayload
                    {
                        CorrelationId = context.CorrelationId,
                        Status = JobStatus.Succeeded.ToString(),
                        ActionNote = $"[mock] {context.ActionType} completed for '{context.Input}'.",
                        JobUrl = $"https://portal.azure.com/mock/{mockJobId}"
                    })
                };
                httpRequest.Headers.Add("X-Callback-Token", azureAutomationOptions.Value.CallbackSharedSecret);
                response = await client.SendAsync(httpRequest);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Mock loopback callback to {CallbackUrl} returned {StatusCode}",
                    context.CallbackUrl, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mock loopback callback to {CallbackUrl} failed", context.CallbackUrl);
        }
    }
}
