using FluentValidation;
using RagApi.Application.Models;

namespace RagApi.Application.Validators;

public class QueryRequestValidator : AbstractValidator<QueryRequest>
{
    public QueryRequestValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(1000);

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 20);
    }
}
