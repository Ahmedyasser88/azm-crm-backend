using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundWhatsAppMessage;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class ReceiveInboundWhatsAppMessageCommandValidatorTests
{
    private static ReceiveInboundWhatsAppMessageCommandValidator CreateValidator() =>
        new(new StubLocalizationService());

    [Fact]
    public void Empty_FromPhoneNumber_fails()
    {
        var result = CreateValidator().Validate(new ReceiveInboundWhatsAppMessageCommand("", "Body", null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_FromPhoneNumber_fails()
    {
        var result = CreateValidator().Validate(
            new ReceiveInboundWhatsAppMessageCommand("not-a-phone", "Body", null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = CreateValidator().Validate(
            new ReceiveInboundWhatsAppMessageCommand("+966512345678", "", null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = CreateValidator().Validate(
            new ReceiveInboundWhatsAppMessageCommand("+966512345678", "Body", null));
        Assert.True(result.IsValid);
    }
}
