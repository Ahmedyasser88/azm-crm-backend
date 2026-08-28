using AzmCrm.Application.Features.Communications.Commands.SubmitWebForm;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class SubmitWebFormCommandValidatorTests
{
    private static SubmitWebFormCommandValidator CreateValidator() =>
        new(new StubLocalizationService());

    [Fact]
    public void Empty_Name_fails()
    {
        var result = CreateValidator().Validate(
            new SubmitWebFormCommand("", "jane@example.com", null, null, "Body"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_Email_fails()
    {
        var result = CreateValidator().Validate(
            new SubmitWebFormCommand("Jane Doe", "not-an-email", null, null, "Body"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = CreateValidator().Validate(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", null, null, ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_Phone_fails_when_provided()
    {
        var result = CreateValidator().Validate(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", "not-a-phone", null, "Body"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_with_no_phone_passes()
    {
        var result = CreateValidator().Validate(
            new SubmitWebFormCommand("Jane Doe", "jane@example.com", null, "Subject", "Body"));

        Assert.True(result.IsValid);
    }
}
