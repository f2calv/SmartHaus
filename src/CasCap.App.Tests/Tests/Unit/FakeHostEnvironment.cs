using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CasCap.Tests.Unit;

/// <summary>
/// <see cref="IHostEnvironment"/> substitute fixed to Development so the comms service skips the
/// signal-cli readiness probe.
/// </summary>
public sealed class FakeHostEnvironment : IHostEnvironment
{
    /// <inheritdoc/>
    public string EnvironmentName { get; set; } = Environments.Development;

    /// <inheritdoc/>
    public string ApplicationName { get; set; } = nameof(CasCap);

    /// <inheritdoc/>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc/>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
