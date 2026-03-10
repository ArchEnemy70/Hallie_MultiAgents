using ExternalServices;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class N8nPoller : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;     // ex: https://ton-n8n/webhook/events
    //private readonly string _apiKey;      // secret
    //private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    //private string? _cursor;

    public N8nPoller()
    {
        _http = new();
        _baseUrl = "http://localhost:5678/webhook-test/brief";
       // _apiKey = "";
       // _timer = new PeriodicTimer(new TimeSpan(60));

        // header commun
        //_http.DefaultRequestHeaders.Remove("X-Api-Key");
        //_http.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
    }
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true // tolérant (même si on a JsonPropertyName)
    };
    public async Task<N8nResponse?> StartAsync(string city, string topic)
    {
        var req = new N8nRequest(city, topic);

        using var resp = await _http.PostAsJsonAsync(_baseUrl, req, JsonOpts);
        //
        var body = await resp.Content.ReadAsStringAsync();
        LoggerService.LogInfo($"N8nPoller.StartAsync --> {body}");

        if (!resp.IsSuccessStatusCode)
        {
            LoggerService.LogError($"N8nPoller.StartAsync --> HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
            return null;
        }

        var result2 = JsonSerializer.Deserialize<N8nResponse>(body, JsonOpts)
                     ?? throw new JsonException("N8nPoller.StartAsync --> JSON vide/non valide");
        //
        if (!resp.IsSuccessStatusCode)
        {
            var body2 = await resp.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"n8n HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body2}");
        }

        var result = await resp.Content.ReadFromJsonAsync<N8nResponse>(JsonOpts)
                     ?? throw new JsonException("N8nPoller.StartAsync --> Réponse n8n vide ou non JSON.");

        if (!result.Success || result.Data is null)
        {
            LoggerService.LogError($"N8nPoller.StartAsync --> n8n a répondu success=false ou data=null. Body: {body}");
            return null;
        }

        return result;
    }

    public void Stop() => _cts.Cancel();
    public void Dispose() { _cts.Cancel(); _cts.Dispose(); }
    //public void Dispose() { _cts.Cancel(); _timer.Dispose(); _cts.Dispose(); }
}


public record N8nRequest(
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("topic")] string Topic
);

public record N8nResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] N8nData? Data
);

public record N8nData(
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("topic_utilise")] string TopicUtilise,
    [property: JsonPropertyName("source_rss")] string SourceRss,
    [property: JsonPropertyName("meteo")] Meteo Meteo,
    [property: JsonPropertyName("news")] List<NewsItem> News
);

public record Meteo(
    [property: JsonPropertyName("resume")] string Resume,
    [property: JsonPropertyName("temp")] string Temp,
    [property: JsonPropertyName("vent")] string Vent
);

public record NewsItem(
    [property: JsonPropertyName("titre")] string Titre,
    [property: JsonPropertyName("resume")] string Resume,
    [property: JsonPropertyName("lien")] string Lien
);