using AzmCrm.Application.Features.AgentTasks.Commands.CreateAgentTask;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.AgentTasks;

public class CreateAgentTaskCommandValidatorTests
{
    private readonly CreateAgentTaskCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_Title_fails()
    {
        var result = _validator.Validate(new CreateAgentTaskCommand("", null, null, null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Title_over_200_chars_fails()
    {
        var result = _validator.Validate(new CreateAgentTaskCommand(new string('a', 201), null, null, null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(new CreateAgentTaskCommand("Title", "Description", null, null, null));

        Assert.True(result.IsValid);
    }
}
