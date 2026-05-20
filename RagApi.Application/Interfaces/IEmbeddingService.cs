namespace RagApi.Application.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);

    Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken ct = default);
}
