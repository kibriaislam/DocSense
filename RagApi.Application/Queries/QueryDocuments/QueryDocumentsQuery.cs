using MediatR;
using RagApi.Application.Models;

namespace RagApi.Application.Queries.QueryDocuments;

public record QueryDocumentsQuery(string Question, int TopK = 5) : IRequest<QueryResult>;
