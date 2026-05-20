using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RagApi.Infrastructure.Options;

namespace RagApi.Infrastructure.Ollama;

public class OllamaClientService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaClientService(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/embed";
        var request = new OllamaEmbedRequest(_options.EmbedModel, text);

        using var response = await _http.PostAsJsonAsync(url, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Ollama embed request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Ollama embed response was empty.");

        if (result.Embeddings is null || result.Embeddings.Length == 0)
        {
            throw new InvalidOperationException("Ollama embed response did not contain any embeddings.");
        }

        return result.Embeddings[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        int maxConcurrency = 4,
        CancellationToken ct = default)
    {
        var textList = texts.ToList();

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = textList.Select(async (text, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var embedding = await EmbedAsync(text, ct);
                return (index, embedding);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results
            .OrderBy(result => result.index)
            .Select(result => result.embedding)
            .ToList();
    }
}

internal record OllamaEmbedRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] string Input);

internal record OllamaEmbedResponse(
    [property: JsonPropertyName("embeddings")] float[][] Embeddings);
