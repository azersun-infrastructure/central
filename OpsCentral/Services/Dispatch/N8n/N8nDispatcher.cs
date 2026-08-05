using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Services.Dispatch.N8n;

/// <summary>
/// Calls an n8n webhook that completes synchronously (n8n's "Respond: When Last Node Finishes"
/// mode) and returns its result directly in the HTTP response — e.g. the LDAP Search workflow.
/// Unlike Jenkins/Azure Automation, there is no job id to track: DispatchAsync returns the
/// final result immediately via DispatchResult.Completed, so PollStatusAsync is never invoked.
/// </summary>
public class N8nDispatcher(IHttpClientFactory httpClientFactory, IOptions<N8nOptions> options) : IAdActionDispatcher
{
    private readonly N8nOptions _options = options.Value;

    public DispatchTarget Target => DispatchTarget.N8n;

    public async Task<DispatchResult> DispatchAsync(AdActionDispatchContext context, CancellationToken ct)
    {
        if (!_options.WebhookUrls.TryGetValue(context.ActionType.ToString(), out var webhookUrl) ||
            string.IsNullOrEmpty(webhookUrl))
        {
            return DispatchResult.Failed($"No n8n webhook configured for action type '{context.ActionType}'.");
        }

        var client = httpClientFactory.CreateClient("N8n");
        client.Timeout = TimeSpan.FromSeconds(20);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(webhookUrl, new { searchTerm = context.Input }, ct);
        }
        catch (Exception ex)
        {
            return DispatchResult.Failed($"n8n webhook-a qoşulma xətası: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return DispatchResult.Completed(JobStatus.Failed, $"n8n {(int)response.StatusCode} qaytardı.", body);
        }

        var entries = ParseEntries(body);

        var note = entries.Count switch
        {
            0 => "Nəticə tapılmadı.",
            1 => DescribeEntry(entries[0]),
            _ => $"{entries.Count} nəticə tapıldı."
        };

        var pretty = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });

        return DispatchResult.Completed(JobStatus.Succeeded, note, pretty);
    }

    private static List<JsonElement> ParseEntries(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        try
        {
            // n8n returns a bare JSON array when the workflow's last node produced 0 or 2+ items.
            return JsonSerializer.Deserialize<List<JsonElement>>(body) ?? [];
        }
        catch (JsonException)
        {
            // n8n unwraps a single-item result to a bare JSON object instead of a one-element array.
            return [JsonSerializer.Deserialize<JsonElement>(body)];
        }
    }

    private static string DescribeEntry(JsonElement entry)
    {
        string? GetString(string name) =>
            entry.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var name = GetString("displayName") ?? GetString("sAMAccountName") ?? "?";
        var enabled = entry.TryGetProperty("enabled", out var e) && e.ValueKind == JsonValueKind.True;
        var locked = entry.TryGetProperty("locked", out var l) && l.ValueKind == JsonValueKind.True;

        return $"{name} — {(enabled ? "aktiv" : "deaktiv")}{(locked ? ", kilidli" : "")}";
    }

    public Task<JobStatusResult> PollStatusAsync(AdActionRequest request, CancellationToken ct) =>
        throw new NotSupportedException(
            "N8nDispatcher completes synchronously in DispatchAsync (DispatchResult.Completed) — PollStatusAsync should never be reached.");
}
