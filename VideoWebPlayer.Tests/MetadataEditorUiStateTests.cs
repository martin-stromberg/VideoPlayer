using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MetadataEditorUiStateTests
{
    [Theory]
    [InlineData("TVShowDetails.razor")]
    [InlineData("MovieCollectionDetails.razor")]
    public async Task DetailsPage_UsesBlazorValidationAndDirtyDialog(string fileName)
    {
        var content = await ReadComponentAsync(fileName);

        Assert.Contains("<EditForm EditContext=\"@editContext\"", content);
        Assert.Contains("<DataAnnotationsValidator />", content);
        Assert.Contains("<ValidationMessage For=\"@(() => editModel.Name)\"", content);
        Assert.Contains("RunWithDirtyCheckAsync", content);
        Assert.Contains("showDiscardDialog", content);
        Assert.Contains("DiscardAndContinueAsync", content);
        Assert.Contains("ContinueEditing", content);
        Assert.Contains("editContext.OnFieldChanged += OnEditFieldChanged", content);
    }

    [Fact]
    public async Task TVShowDetails_SeasonEditClearsEpisodeContext()
    {
        var content = await ReadComponentAsync("TVShowDetails.razor");

        Assert.Contains("selectedEpisode = null;", content);
        Assert.Contains("BeginEdit(selectedSeason)", content);
    }

    [Fact]
    public async Task MovieCollectionDetails_DoesNotAutoSelectSingleMovie()
    {
        var content = await ReadComponentAsync("MovieCollectionDetails.razor");

        Assert.DoesNotContain("collection.Movies.Length == 1", content);
        Assert.Contains("ShowFirstMovie", content);
    }

    private static Task<string> ReadComponentAsync(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "VideoPlayer.sln")))
            current = current.Parent;

        Assert.NotNull(current);
        var area = fileName.StartsWith("TV", StringComparison.Ordinal) ? "TV" : "Movies";
        var path = Path.Combine(current.FullName, "VideoWebPlayer", "Components", "Pages", area, fileName);
        return File.ReadAllTextAsync(path);
    }
}
