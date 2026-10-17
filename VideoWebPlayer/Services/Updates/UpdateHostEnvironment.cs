namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Reports properties of the hosting environment that are relevant to update behavior.
/// </summary>
public interface IUpdateHostEnvironment
{
    /// <summary>
    /// Whether the application runs under the IIS AspNetCoreModule (in-process or out-of-process).
    /// A configured service name has no effect in that case: the updater neither detects nor
    /// controls IIS application pools as Windows services.
    /// </summary>
    bool RunsUnderIis { get; }
}

/// <summary>
/// Detects the hosting mode from the environment variables the AspNetCoreModule sets for the
/// child process it manages.
/// </summary>
public sealed class UpdateHostEnvironment : IUpdateHostEnvironment
{
    // ANCM sets ASPNETCORE_TOKEN/ASPNETCORE_PORT for out-of-process hosting and the
    // ASPNETCORE_IIS_* variables for in-process hosting.
    private static readonly string[] IisMarkerVariables =
    [
        "ASPNETCORE_TOKEN",
        "ASPNETCORE_PORT",
        "ASPNETCORE_IIS_HTTPAUTH",
        "ASPNETCORE_IIS_APPL_PATH",
        "ASPNETCORE_IIS_PHYSICAL_PATH"
    ];

    /// <inheritdoc />
    public bool RunsUnderIis { get; } = IisMarkerVariables.Any(
        name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));
}
