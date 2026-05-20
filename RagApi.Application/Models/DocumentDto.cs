namespace RagApi.Application.Models;

public record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string Status,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    int ChunkCount);
