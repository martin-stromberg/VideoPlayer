using VideoWebPlayer.Tools.MarkdownLinkCheck;

namespace MarkdownLinkCheck.Tests;

public sealed class MarkdownLinkCheckerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "markdown-link-check-" + Guid.NewGuid());

    public MarkdownLinkCheckerTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public void ExistingRelativeLinkPasses()
    {
        Write("docs/index.md", "[Guide](guide/setup.md)");
        Write("docs/guide/setup.md", "# Setup");

        var result = MarkdownLinkChecker.Check(root);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.CheckedLinks);
    }

    [Fact]
    public void MissingFileFailsWithLine()
    {
        Write("docs/index.md", "Intro\n[Missing](missing.md)");

        var result = MarkdownLinkChecker.Check(root);

        var error = Assert.Single(result.Errors);
        Assert.Contains("docs/index.md:2", error);
        Assert.Contains("missing file", error);
    }

    [Fact]
    public void FragmentIsChecked()
    {
        Write("docs/index.md", "[Section](guide.md#Install Windows)");
        Write("docs/guide.md", "# Install Windows");

        var result = MarkdownLinkChecker.Check(root);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CasingMismatchFailsEvenOnWindows()
    {
        Write("docs/index.md", "[Guide](Guide.md)");
        Write("docs/guide.md", "# Guide");

        var result = MarkdownLinkChecker.Check(root);

        Assert.Contains(result.Errors, error => error.Contains("casing mismatch"));
    }

    [Fact]
    public void ExternalLinksAreSkipped()
    {
        Write("docs/index.md", "[External](https://example.invalid/missing)");

        var result = MarkdownLinkChecker.Check(root);

        Assert.Empty(result.Errors);
        Assert.Equal(0, result.CheckedLinks);
    }

    [Fact]
    public void NodeModulesIsIgnored()
    {
        // Third-party npm packages ship their own markdown with broken relative links (e.g.
        // pointing at files only present in their own git repo, not the installed package) -
        // scanning node_modules made every local commit fail after `npm install` for the
        // release tooling (semantic-release and its plugins), even though nothing in the
        // actual project changed.
        Write("node_modules/some-package/README.md", "[Missing](does-not-exist.md)");

        var result = MarkdownLinkChecker.Check(root);

        Assert.Empty(result.Errors);
        Assert.Equal(0, result.CheckedFiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
