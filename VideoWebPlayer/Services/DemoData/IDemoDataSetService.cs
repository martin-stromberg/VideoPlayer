namespace VideoWebPlayer.Services.DemoData;

/// <summary>
/// Lightweight info about a demo data set found on disk.
/// </summary>
/// <param name="Id">The identifier (usually the JSON file name without extension).</param>
/// <param name="Name">Display name of the data set.</param>
/// <param name="Description">Optional description.</param>
public sealed record DemoDataSetInfo(string Id, string Name, string? Description);

/// <summary>
/// Provides discovery and application of demo data sets (JSON files).
/// </summary>
public interface IDemoDataSetService
{
	/// <summary>
	/// Returns available demo data sets.
	/// </summary>
	Task<IReadOnlyList<DemoDataSetInfo>> GetAvailableAsync(CancellationToken cancellationToken = default);
	/// <summary>
	/// Applies a demo data set to the database.
	/// </summary>
	Task ApplyAsync(string demoDataSetId, Data.ApplicationUser user, CancellationToken cancellationToken = default);
}
