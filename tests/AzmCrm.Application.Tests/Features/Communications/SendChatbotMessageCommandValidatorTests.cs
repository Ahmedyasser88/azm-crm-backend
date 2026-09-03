using AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class SendChatbotMessageCommandValidatorTests
{
    private static SendChatbotMessageCommandValidator CreateValidator() =>
        new(new StubLocalizationService());

    [Fact]
    public void Empty_ConversationId_fails()
    {
        var result = CreateValidator().Validate(new SendChatbotMessageCommand(Guid.Empty, "Body"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = CreateValidator().Validate(new SendChatbotMessageCommand(Guid.NewGuid(), ""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = CreateValidator().Validate(new SendChatbotMessageCommand(Guid.NewGuid(), "Hi there"));
        Assert.True(result.IsValid);
    }
}
