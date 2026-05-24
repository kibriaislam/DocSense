using MediatR;
using RagApi.Application.Interfaces;

namespace RagApi.Application.Commands.DeleteDocument;

public class DeleteDocumentHandler : IRequestHandler<DeleteDocumentCommand>
{
    private readonly IVectorRepository _repository;

    public DeleteDocumentHandler(IVectorRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteDocumentCommand request, CancellationToken ct) =>
        await _repository.DeleteDocumentAsync(request.Id, ct);
}
