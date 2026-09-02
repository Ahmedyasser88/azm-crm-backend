using FluentValidation;

namespace AzmCrm.Application.Features.Identity.Queries.SearchAgents;

public sealed class SearchAgentsQueryValidator : AbstractValidator<SearchAgentsQuery>
{
    public SearchAgentsQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page Size must be between 1 and 50.");
    }
}
