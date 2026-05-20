namespace RagApi.Application.Models;

public record QueryResult(
    string Answer,
    IReadOnlyList<SourceReference> Sources,
    long EmbedLatencyMs,
    long SearchLatencyMs,
    long GenerateLatencyMs);
