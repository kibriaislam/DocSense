using FluentValidation;
using RagApi.Application.Queries.QueryDocuments;

namespace RagApi.Application.Validators;

public class QueryDocumentsQueryValidator : AbstractValidator<QueryDocumentsQuery>
{
    public QueryDocumentsQueryValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(1000);

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 20);
    }
}
