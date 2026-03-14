using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides operations related to genre logic.
/// </summary>
public interface IGenreService
{
	/// <summary>
	/// Marks genres as changed to trigger refresh logic.
	/// </summary>
	Task MarkGenresAsChangedAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Returns genres that are currently in their seasonal visibility window.
	/// </summary>
	Task<List<Genre>> GetSeasonalGenresAsync(CancellationToken cancellationToken = default);
}
