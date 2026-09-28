using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal <see cref="IWebHostEnvironment"/> stand-in for tests that only need content and web root to
/// point somewhere valid. Shared so the same stub is not re-declared in every test class that needs one.
/// </summary>
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    /// <inheritdoc />
    public string ApplicationName { get; set; } = "VideoWebPlayer";

    /// <inheritdoc />
    public string EnvironmentName { get; set; } = "Test";

    /// <inheritdoc />
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc />
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; } = null!;

    /// <inheritdoc />
    public IFileProvider WebRootFileProvider { get; set; } = null!;
}
