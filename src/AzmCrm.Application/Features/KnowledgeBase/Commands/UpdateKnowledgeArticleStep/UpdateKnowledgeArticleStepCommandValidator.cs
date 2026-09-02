using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;

public sealed class UpdateKnowledgeArticleStepCommandValidator : AbstractValidator<UpdateKnowledgeArticleStepCommand>
{
    public UpdateKnowledgeArticleStepCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.KnowledgeArticleId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Knowledge Article Id"]);

        RuleFor(x => x.StepId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Step Id"]);

        RuleFor(x => x.StepNumber)
            .GreaterThan(0)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Step Number", 0]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Description"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 4000]);
    }
}
