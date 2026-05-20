namespace RagApi.Application.Models;

public record IngestDocumentRequest(
    string FileName,
    string ContentType,
    byte[] FileBytes);
