using RagApi.Domain.Enums;

namespace RagApi.Domain.Entities;

public class Document
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public DocumentStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public ICollection<DocumentChunk> Chunks { get; init; } = new List<DocumentChunk>();
}
