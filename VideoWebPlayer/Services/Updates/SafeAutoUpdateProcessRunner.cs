using msTools.Updater;
using System.Diagnostics;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Replacement for <see cref="DefaultAutoUpdateProcessRunner"/> that does not call
/// <see cref="IAutoUpdatePackageStore.RecoverWorkspaceAsync"/> before the installation script is started.
/// The default runner recovers the workspace after the script has already been generated, which deletes the
/// downloaded package and leaves the script pointing at a missing zip file.
/// </summary>
public sealed class SafeAutoUpdateProcessRunner : IAutoUpdateProcessRunner
{
    private const string DefaultUpdateUnitName = "VideoWebPlayer-AutoUpdate";

    private readonly ILogger<SafeAutoUpdateProcessRunner> _logger;
    private readonly AutoUpdateOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeAutoUpdateProcessRunner"/> class.
    /// </summary>
    public SafeAutoUpdateProcessRunner(ILogger<SafeAutoUpdateProcessRunner> logger, AutoUpdateOptions options)
    {
        _logger = logger;
        _options = options;
    }

    private string UpdateUnitName => _options.UpdateUnitName ?? DefaultUpdateUnitName;

    /// <summary>
    /// Resets the transient systemd unit used to run the update script.
    /// </summary>
    public void EnsureUpdateUnitAvailable(string scriptPath)
    {
        // Reset a previously failed transient unit so systemd-run can create a fresh one.
        // We deliberately do not recover the package workspace here; the install orchestrator
        // already did that before the package was downloaded.
        TryRun("systemctl", ["--user", "reset-failed", UpdateUnitName]);
        TryRun("systemctl", ["reset-failed", UpdateUnitName]);
    }

    /// <summary>
    /// Starts the generated installation script via systemd-run.
    /// </summary>
    public void StartScript(string scriptPath)
    {
        _logger.LogInformation("Starting update script: {ScriptPath}", scriptPath);

        var startInfo = new ProcessStartInfo("systemd-run")
        {
            ArgumentList =
            {
                "--unit", UpdateUnitName,
                "--service-type", "exec",
                "/bin/bash", scriptPath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start systemd-run.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"systemd-run exited with {process.ExitCode}. Output: {output}. Error: {error}");
        }
    }

    private void TryRun(string fileName, string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                Arguments = string.Join(" ", arguments),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignored failure while resetting update unit {UpdateUnit}.", UpdateUnitName);
        }
    }
}
