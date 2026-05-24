using RagApi.Domain.Entities;

namespace RagApi.Application.Interfaces;

public interface IGenerationService
{
    IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string question,
        IReadOnlyList<DocumentChunk> context,
        CancellationToken ct = default);

    Task<string> CompleteAsync(
        string systemPrompt,
        string question,
        IReadOnlyList<DocumentChunk> context,
        CancellationToken ct = default);
}
