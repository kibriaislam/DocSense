namespace RagApi.Application.Models;

public record ChunkScore(
    Guid DocumentId,
    string FileName,
    int ChunkIndex,
    float SimilarityScore);

public record QueryDebugResult(
    string Answer,
    IReadOnlyList<SourceReference> Sources,
    long EmbedLatencyMs,
    long SearchLatencyMs,
    long GenerateLatencyMs,
    IReadOnlyList<float> QueryVectorPreview,
    IReadOnlyList<ChunkScore> ChunkScores,
    string PromptPreview);
