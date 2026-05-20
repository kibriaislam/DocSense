using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RagApi.Infrastructure.Options;

namespace RagApi.Infrastructure.Ollama;

internal record OllamaEmbedRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] string Input);

internal record OllamaEmbedResponse(
    [property: JsonPropertyName("embeddings")] float[][] Embeddings);

internal record OllamaGenerateChunk(
    [property: JsonPropertyName("response")] string? Response,
    [property: JsonPropertyName("done")] bool Done);

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

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/generate";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = _options.GenerateModel,
                prompt,
                stream = true
            })
        };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Ollama generate request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = JsonSerializer.Deserialize<OllamaGenerateChunk>(line);
            if (!string.IsNullOrEmpty(chunk?.Response))
            {
                yield return chunk.Response;
            }

            if (chunk?.Done == true)
            {
                break;
            }
        }
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var builder = new StringBuilder();

        await foreach (var token in GenerateStreamAsync(prompt, ct))
        {
            builder.Append(token);
        }

        return builder.ToString();
    }
}
