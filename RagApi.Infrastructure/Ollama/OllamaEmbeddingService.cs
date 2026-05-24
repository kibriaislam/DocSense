using RagApi.Application.Interfaces;

namespace RagApi.Infrastructure.Ollama;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly OllamaClientService _client;

    public OllamaEmbeddingService(OllamaClientService client)
    {
        _client = client;
    }

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default) =>
        _client.EmbedAsync(text, ct);

    public Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken ct = default) =>
        _client.EmbedBatchAsync(texts, ct: ct);
}
