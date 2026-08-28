using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AzmCrm.Infrastructure.Tests.Storage;

/// <summary>Minimal <see cref="IHostEnvironment"/> stub — only ContentRootPath is exercised.</summary>
internal sealed class StubHostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = "AzmCrm.Infrastructure.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public string EnvironmentName { get; set; } = "Test";
}
