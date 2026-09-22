using VideoWebPlayer.Components.Pages.Admin.MediaSources;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public class MediaSourceAdminDetailsValidationTests : IDisposable
{
    private readonly string _rootDir;

    public MediaSourceAdminDetailsValidationTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-admin-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_rootDir, recursive: true); } catch { }
    }

    [Fact]
    public void TryValidateLocalDirectory_WithExistingDirectory_ReturnsTrue()
    {
        var valid = MediaSourceAdminDetails.TryValidateLocalDirectory(_rootDir, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateLocalDirectory_WithMissingDirectory_ReturnsNotFoundMessage()
    {
        var missingDir = Path.Combine(_rootDir, "missing");

        var valid = MediaSourceAdminDetails.TryValidateLocalDirectory(missingDir, out var error);

        Assert.False(valid);
        Assert.Equal("Verzeichnis existiert nicht.", error);
    }

    [Fact]
    public void TryValidateLocalDirectory_WithRelativePath_ReturnsInvalidPathMessage()
    {
        var valid = MediaSourceAdminDetails.TryValidateLocalDirectory(Path.Combine("relative", "path"), out var error);

        Assert.False(valid);
        Assert.Equal("Ungültiger Pfad: Bitte einen absoluten Verzeichnispfad angeben.", error);
    }

    [Fact]
    public void TryValidateLocalDirectory_WithIllegalPathCharacters_ReturnsInvalidPathMessage()
    {
        // Regressionstest: Ein absoluter, aber für GetFullPath ungültiger Pfad (Null-Zeichen)
        // wurde zuvor still abgefangen und fälschlich als "Verzeichnis existiert nicht." gemeldet.
        var valid = MediaSourceAdminDetails.TryValidateLocalDirectory("C:\\bad\0dir", out var error);

        Assert.False(valid);
        Assert.StartsWith("Ungültiger Pfad", error);
    }

    [Fact]
    public void TryValidateLocalDirectory_WithInaccessibleDirectory_ReturnsAccessDeniedMessage()
    {
        // Regressionstest: Ein existierendes Verzeichnis ohne Lesezugriff wurde zuvor
        // fälschlich als "Verzeichnis existiert nicht." gemeldet.
        var deniedDir = Path.Combine(_rootDir, "denied");
        Directory.CreateDirectory(deniedDir);
        try
        {
            TestHelpers.DenyReadAccess(deniedDir);

            var valid = MediaSourceAdminDetails.TryValidateLocalDirectory(deniedDir, out var error);

            Assert.False(valid);
            Assert.Equal("Auf das Verzeichnis kann nicht zugegriffen werden.", error);
        }
        finally
        {
            TestHelpers.ResetAccessControl(deniedDir);
        }
    }
}
