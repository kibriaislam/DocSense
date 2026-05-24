using MediatR;

namespace RagApi.Application.Commands.IngestDocument;

public record IngestDocumentCommand(
    string FileName,
    string ContentType,
    byte[] FileBytes) : IRequest<Guid>;
