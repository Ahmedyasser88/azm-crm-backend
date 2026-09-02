using AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.QuickReplies;

public class CreateQuickReplyTemplateCommandValidatorTests
{
    private readonly CreateQuickReplyTemplateCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_Title_fails()
    {
        var result = _validator.Validate(new CreateQuickReplyTemplateCommand("", "Body"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = _validator.Validate(new CreateQuickReplyTemplateCommand("Title", ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(new CreateQuickReplyTemplateCommand("Title", "Body"));

        Assert.True(result.IsValid);
    }
}
