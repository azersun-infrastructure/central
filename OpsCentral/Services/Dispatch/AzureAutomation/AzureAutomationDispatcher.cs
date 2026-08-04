using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Services.Dispatch.AzureAutomation;

/// <summary>
/// Triggers an Azure Automation runbook via its webhook URL and polls its status through
/// the ARM Jobs API. The webhook response never carries a job ID (a known Azure Automation
/// limitation), so PollStatusAsync must list recent jobs on the Automation Account and match
/// by the CorrelationId input parameter the runbook is expected to echo back — this is a
/// contract the runbook authors need to honor (see plan judgment call #4).
/// </summary>
public class AzureAutomationDispatcher(IHttpClientFactory httpClientFactory, IOptions<AzureAutomationOptions> options)
    : IAdActionDispatcher
{
    private const string ArmScope = "https://management.azure.com/.default";
    private const string ArmApiVersion = "2019-06-01";

    private readonly AzureAutomationOptions _options = options.Value;
    private ClientSecretCredential? _credential;

    public DispatchTarget Target => DispatchTarget.AzureAutomation;

    public async Task<DispatchResult> DispatchAsync(AdActionDispatchContext context, CancellationToken ct)
    {
        if (!_options.WebhookUrls.TryGetValue(context.ActionType.ToString(), out var webhookUrl) ||
            string.IsNullOrEmpty(webhookUrl))
        {
            return DispatchResult.Failed($"No Azure Automation webhook configured for action type '{context.ActionType}'.");
        }

        var client = httpClientFactory.CreateClient("AzureAutomation");

        var body = new
        {
            CorrelationId = context.CorrelationId,
            Input = context.Input,
            CallbackUrl = context.CallbackUrl
        };

        using var response = await client.PostAsJsonAsync(webhookUrl, body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return DispatchResult.Failed($"Azure Automation webhook returned {(int)response.StatusCode}: {responseBody}");
        }

        // No job id available yet — PollStatusAsync resolves it later by matching CorrelationId.
        return DispatchResult.Ok(null, null);
    }

    public async Task<JobStatusResult> PollStatusAsync(AdActionRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("AzureAutomation");
        await AttachArmTokenAsync(client, ct);

        // RawResultPayload is used as the poll cache (mirrors JenkinsDispatcher): once a job is
        // matched by CorrelationId, its ARM job id is cached here so subsequent polls skip the list+scan.
        var cache = ArmPollCache.FromRawPayload(request.RawResultPayload);
        var jobId = cache.JobId;

        if (string.IsNullOrEmpty(jobId))
        {
            jobId = await TryFindJobIdByCorrelationIdAsync(client, request, ct);
            if (jobId is null)
            {
                return new JobStatusResult(JobStatus.Running, "No matching Azure Automation job found yet.", cache.ToRawPayload());
            }

            cache = cache with { JobId = jobId };
        }

        var jobUrl = $"https://management.azure.com{_options.AutomationAccountResourceId}/jobs/{jobId}?api-version={ArmApiVersion}";
        using var jobResponse = await client.GetAsync(jobUrl, ct);
        if (!jobResponse.IsSuccessStatusCode)
        {
            return new JobStatusResult(JobStatus.Running, "Azure Automation job lookup pending.", cache.ToRawPayload());
        }

        using var jobDoc = JsonDocument.Parse(await jobResponse.Content.ReadAsStringAsync(ct));
        var armStatus = jobDoc.RootElement.GetProperty("properties").GetProperty("status").GetString();

        var status = armStatus switch
        {
            "New" or "Activating" or "Queued" => JobStatus.Running,
            "Running" or "Resuming" or "Stopping" or "Suspending" => JobStatus.Running,
            "Completed" => JobStatus.Succeeded,
            "Failed" or "Suspended" or "Stopped" => JobStatus.Failed,
            _ => JobStatus.Running
        };

        return new JobStatusResult(status, $"Azure Automation job status: {armStatus}", cache.ToRawPayload());
    }

    private record ArmPollCache(string? JobId)
    {
        public static ArmPollCache FromRawPayload(string? rawPayload)
        {
            if (string.IsNullOrEmpty(rawPayload))
            {
                return new ArmPollCache(JobId: null);
            }

            try
            {
                return JsonSerializer.Deserialize<ArmPollCache>(rawPayload) ?? new ArmPollCache(JobId: null);
            }
            catch (JsonException)
            {
                return new ArmPollCache(JobId: null);
            }
        }

        public string ToRawPayload() => JsonSerializer.Serialize(this);
    }

    private async Task<string?> TryFindJobIdByCorrelationIdAsync(HttpClient client, AdActionRequest request, CancellationToken ct)
    {
        var windowStart = (request.DispatchedAtUtc ?? request.RequestedAtUtc).AddSeconds(-30).ToString("O");
        var listUrl = $"https://management.azure.com{_options.AutomationAccountResourceId}/jobs" +
                      $"?api-version={ArmApiVersion}&$filter=startTime ge {Uri.EscapeDataString(windowStart)}";

        using var listResponse = await client.GetAsync(listUrl, ct);
        if (!listResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(ct));
        if (!listDoc.RootElement.TryGetProperty("value", out var jobs))
        {
            return null;
        }

        foreach (var job in jobs.EnumerateArray())
        {
            var candidateJobId = job.GetProperty("properties").GetProperty("jobId").GetString();
            if (candidateJobId is null)
            {
                continue;
            }

            var detailUrl = $"https://management.azure.com{_options.AutomationAccountResourceId}/jobs/{candidateJobId}?api-version={ArmApiVersion}";
            using var detailResponse = await client.GetAsync(detailUrl, ct);
            if (!detailResponse.IsSuccessStatusCode)
            {
                continue;
            }

            using var detailDoc = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync(ct));
            if (detailDoc.RootElement.GetProperty("properties").TryGetProperty("parameters", out var parameters) &&
                parameters.TryGetProperty("CorrelationId", out var correlationProp) &&
                string.Equals(correlationProp.GetString(), request.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return candidateJobId;
            }
        }

        return null;
    }

    private async Task AttachArmTokenAsync(HttpClient client, CancellationToken ct)
    {
        _credential ??= new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret);
        var token = await _credential.GetTokenAsync(new TokenRequestContext([ArmScope]), ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }
}
