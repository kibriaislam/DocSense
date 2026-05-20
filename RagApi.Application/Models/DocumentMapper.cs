using RagApi.Domain.Entities;

namespace RagApi.Application.Models;

public static class DocumentMapper
{
    public static DocumentDto ToDto(Document document) =>
        new(
            document.Id,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.Status.ToString(),
            document.CreatedAt,
            document.ProcessedAt,
            document.Chunks.Count);
}
