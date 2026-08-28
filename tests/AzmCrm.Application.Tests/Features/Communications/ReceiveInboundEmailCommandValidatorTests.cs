using AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class ReceiveInboundEmailCommandValidatorTests
{
    private static ReceiveInboundEmailCommandValidator CreateValidator() =>
        new(new StubLocalizationService());

    [Fact]
    public void Empty_FromEmail_fails()
    {
        var result = CreateValidator().Validate(new ReceiveInboundEmailCommand("", null, null, "Body", null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_FromEmail_fails()
    {
        var result = CreateValidator().Validate(
            new ReceiveInboundEmailCommand("not-an-email", null, null, "Body", null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = CreateValidator().Validate(
            new ReceiveInboundEmailCommand("jane@example.com", null, null, "", null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = CreateValidator().Validate(
            new ReceiveInboundEmailCommand("jane@example.com", "Jane Doe", "Help", "Body", null));
        Assert.True(result.IsValid);
    }
}
