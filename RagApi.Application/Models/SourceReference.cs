namespace RagApi.Application.Models;

public record SourceReference(
    Guid DocumentId,
    string FileName,
    int ChunkIndex,
    float SimilarityScore,
    string Snippet);
