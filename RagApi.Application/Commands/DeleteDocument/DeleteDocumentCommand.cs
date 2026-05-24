using MediatR;

namespace RagApi.Application.Commands.DeleteDocument;

public record DeleteDocumentCommand(Guid Id) : IRequest;
