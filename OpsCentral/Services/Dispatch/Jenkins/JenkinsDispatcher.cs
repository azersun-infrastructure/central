using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Services.Dispatch.Jenkins;

/// <summary>
/// Triggers a Jenkins job via buildWithParameters and polls its status.
/// Jenkins' initial response only yields a queue-item URL, not a build number, so
/// PollStatusAsync resolves queue-item -> build -> build result on each call, caching
/// the resolved build URL in the JobStatusResult RawPayload (persisted back onto
/// AdActionRequest.RawResultPayload by the caller) to avoid re-resolving every poll.
/// </summary>
public class JenkinsDispatcher(IHttpClientFactory httpClientFactory, IOptions<JenkinsOptions> options)
    : IAdActionDispatcher
{
    private readonly JenkinsOptions _options = options.Value;

    public DispatchTarget Target => DispatchTarget.Jenkins;

    public async Task<DispatchResult> DispatchAsync(AdActionDispatchContext context, CancellationToken ct)
    {
        if (!_options.Jobs.TryGetValue(context.ActionType.ToString(), out var jobName))
        {
            return DispatchResult.Failed($"No Jenkins job configured for action type '{context.ActionType}'.");
        }

        var client = CreateClient();

        var query = $"?CorrelationId={Uri.EscapeDataString(context.CorrelationId.ToString())}" +
                    $"&Input={Uri.EscapeDataString(context.Input)}" +
                    $"&CallbackUrl={Uri.EscapeDataString(context.CallbackUrl)}";

        using var response = await client.PostAsync($"job/{jobName}/buildWithParameters{query}", content: null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return DispatchResult.Failed($"Jenkins returned {(int)response.StatusCode}: {body}");
        }

        var queueItemUrl = response.Headers.Location?.ToString();
        if (string.IsNullOrEmpty(queueItemUrl))
        {
            return DispatchResult.Failed("Jenkins did not return a queue-item Location header.");
        }

        return DispatchResult.Ok(queueItemUrl, null);
    }

    public async Task<JobStatusResult> PollStatusAsync(AdActionRequest request, CancellationToken ct)
    {
        var client = CreateClient();
        var cache = JenkinsPollCache.FromRawPayload(request.RawResultPayload);

        var buildUrl = cache.BuildUrl;

        if (buildUrl is null)
        {
            var queueItemUrl = request.ExternalJobId;
            if (string.IsNullOrEmpty(queueItemUrl))
            {
                return new JobStatusResult(JobStatus.Failed, "Missing Jenkins queue-item URL.", cache.ToRawPayload());
            }

            using var queueResponse = await client.GetAsync($"{queueItemUrl.TrimEnd('/')}/api/json", ct);
            if (!queueResponse.IsSuccessStatusCode)
            {
                return new JobStatusResult(JobStatus.Running, "Waiting on Jenkins queue.", cache.ToRawPayload());
            }

            using var queueDoc = JsonDocument.Parse(await queueResponse.Content.ReadAsStringAsync(ct));
            if (queueDoc.RootElement.TryGetProperty("executable", out var executable) &&
                executable.TryGetProperty("url", out var urlProp))
            {
                buildUrl = urlProp.GetString();
                cache = cache with { BuildUrl = buildUrl };
            }
            else
            {
                return new JobStatusResult(JobStatus.Running, "Queued in Jenkins, not yet building.", cache.ToRawPayload());
            }
        }

        using var buildResponse = await client.GetAsync($"{buildUrl!.TrimEnd('/')}/api/json", ct);
        if (!buildResponse.IsSuccessStatusCode)
        {
            return new JobStatusResult(JobStatus.Running, "Build in progress.", cache.ToRawPayload());
        }

        var buildBody = await buildResponse.Content.ReadAsStringAsync(ct);
        using var buildDoc = JsonDocument.Parse(buildBody);
        var building = buildDoc.RootElement.TryGetProperty("building", out var b) && b.GetBoolean();
        var result = buildDoc.RootElement.TryGetProperty("result", out var r) ? r.GetString() : null;

        var status = (building, result) switch
        {
            (true, _) => JobStatus.Running,
            (false, "SUCCESS") => JobStatus.Succeeded,
            (false, null) => JobStatus.Running,
            (false, _) => JobStatus.Failed
        };

        return new JobStatusResult(status, $"Jenkins build result: {result ?? "pending"}", cache.ToRawPayload());
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("Jenkins");
        client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        if (!string.IsNullOrEmpty(_options.Username))
        {
            var raw = Encoding.ASCII.GetBytes($"{_options.Username}:{_options.ApiToken}");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
        }

        return client;
    }

    private record JenkinsPollCache(string? BuildUrl)
    {
        public static JenkinsPollCache FromRawPayload(string? rawPayload)
        {
            if (string.IsNullOrEmpty(rawPayload))
            {
                return new JenkinsPollCache(BuildUrl: null);
            }

            try
            {
                return JsonSerializer.Deserialize<JenkinsPollCache>(rawPayload) ?? new JenkinsPollCache(BuildUrl: null);
            }
            catch (JsonException)
            {
                return new JenkinsPollCache(BuildUrl: null);
            }
        }

        public string ToRawPayload() => JsonSerializer.Serialize(this);
    }
}
