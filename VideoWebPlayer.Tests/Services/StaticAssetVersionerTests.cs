using Microsoft.Extensions.FileProviders;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="StaticAssetVersioner"/>: the fingerprint follows the file content, so a changed stylesheet
/// gets a new URL, and a missing file falls back to the plain path.
/// </summary>
public class StaticAssetVersionerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "static-asset-versioner-" + Guid.NewGuid().ToString("N"));

    public StaticAssetVersionerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private StaticAssetVersioner Create() => new(new PhysicalFileProvider(_root));

    [Fact]
    public void Url_AppendsAFingerprintOfTheFileContent()
    {
        File.WriteAllText(Path.Combine(_root, "app.css"), "body { color: red; }");

        var url = Create().Url("app.css");

        Assert.Matches(@"^app\.css\?v=[0-9a-f]{12}$", url);
    }

    [Fact]
    public void Url_IsStableForTheSameContent_AndChangesWhenTheContentChanges()
    {
        var file = Path.Combine(_root, "app.css");
        File.WriteAllText(file, "a");
        var first = Create().Url("app.css");
        Assert.Equal(first, Create().Url("app.css"));

        File.WriteAllText(file, "b");

        Assert.NotEqual(first, Create().Url("app.css"));
    }

    [Fact]
    public void Url_MissingFile_ReturnsThePlainPath()
    {
        Assert.Equal("missing.css", Create().Url("missing.css"));
    }
}
