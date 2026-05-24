using MediatR;
using RagApi.Application.Interfaces;
using RagApi.Application.Models;

namespace RagApi.Application.Queries.GetAllDocuments;

public class GetAllDocumentsHandler : IRequestHandler<GetAllDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private readonly IVectorRepository _repository;

    public GetAllDocumentsHandler(IVectorRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<DocumentDto>> Handle(GetAllDocumentsQuery request, CancellationToken ct)
    {
        var documents = await _repository.GetAllDocumentsAsync(ct);
        return documents.Select(DocumentMapper.ToDto).ToList();
    }
}
