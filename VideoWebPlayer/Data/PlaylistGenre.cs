using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Records that a <see cref="Data.Playlist"/> is associated with a <see cref="Data.Genre"/> - either
    /// automatically derived from the genres of the titles currently contained in the playlist (see
    /// <see cref="Services.PlaylistGenreService.RecomputeGenresAsync"/>), or, once the owner has manually
    /// overridden the selection (<see cref="Playlist.GenresManuallyOverridden"/>), exactly the genres the
    /// owner picked.
    /// </summary>
    /// <remarks>
    /// Stores every genre that occurs in at least one contained title (or, once overridden, every genre
    /// the owner picked) - not just the ones actually displayed. Display is capped to the first
    /// <see cref="Playlist.MaxDisplayedGenres"/> entries (by <see cref="Count"/> descending, see
    /// <see cref="Services.PlaylistService"/>'s DTO conversion) purely as a presentation concern; filtering
    /// and searching by genre (see <c>PlaylistsController.GetPlaylists</c>) considers every row here,
    /// matching a playlist as soon as any of its (not just its displayed) genres matches.
    /// </remarks>
    public class PlaylistGenre
    {
        /// <summary>
        /// Gets or sets the record identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the owning playlist identifier.
        /// </summary>
        [ForeignKey(nameof(Playlist))]
        public long PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the owning playlist.
        /// </summary>
        public Playlist Playlist { get; set; } = null!;

        /// <summary>
        /// Gets or sets the genre identifier.
        /// </summary>
        [ForeignKey(nameof(Genre))]
        public long GenreId { get; set; }

        /// <summary>
        /// Gets or sets the genre.
        /// </summary>
        public Genre Genre { get; set; } = null!;

        /// <summary>
        /// Gets or sets how many distinct titles (movies and/or TV shows) currently contained in the
        /// playlist carry this genre, used to sort the playlist's genres by frequency descending. While
        /// <see cref="Playlist.GenresManuallyOverridden"/> is set, this is always <c>1</c> for every row
        /// (the owner's picks carry no frequency signal), so display order falls back to the genre name.
        /// </summary>
        public int Count { get; set; }
    }
}
