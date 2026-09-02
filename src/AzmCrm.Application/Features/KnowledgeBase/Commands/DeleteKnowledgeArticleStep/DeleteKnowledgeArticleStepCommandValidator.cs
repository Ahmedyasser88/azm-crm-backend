using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;

public sealed class DeleteKnowledgeArticleStepCommandValidator : AbstractValidator<DeleteKnowledgeArticleStepCommand>
{
    public DeleteKnowledgeArticleStepCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.KnowledgeArticleId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Knowledge Article Id"]);

        RuleFor(x => x.StepId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Step Id"]);
    }
}
