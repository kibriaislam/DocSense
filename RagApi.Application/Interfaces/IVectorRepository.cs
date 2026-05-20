using RagApi.Domain.Entities;
using RagApi.Domain.Enums;

namespace RagApi.Application.Interfaces;

public interface IVectorRepository
{
    Task SaveDocumentAsync(Document document, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        CancellationToken ct = default);

    Task<IReadOnlyList<Document>> GetAllDocumentsAsync(CancellationToken ct = default);

    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);

    Task UpdateDocumentStatusAsync(
        Guid documentId,
        DocumentStatus status,
        string? errorMessage = null,
        CancellationToken ct = default);
}
