using MediatR;
using RagApi.Application.Models;

namespace RagApi.Application.Queries.GetAllDocuments;

public record GetAllDocumentsQuery() : IRequest<IReadOnlyList<DocumentDto>>;
