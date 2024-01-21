using System;
using System.Linq;

namespace Mediathek.Services.Playlists
{
    public interface IPlaylistManager
    {

        Task InitializeAsync();

        GeneralPlaylist GeneralPlaylist { get; }

        Task StartTVShowPlaylistAsync(TVShowEpisode episode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task StartTVShowPlaylistAsync(TVShow show);

        Task StartTVShowPlaylistAsync(TVShowSeason season);

        Task StartMoviePlaylistAsync(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task StartMoviePlaylistAsync(MovieCollection movieCollection);

        Task StartMediaItemPlaylistAsync(MediaItem mediaItem);

        Task StartPlaybackAsync();

        DownloadSource GetFirstVideoSource();

        Task<DownloadSource> ProcessMediaEndedAsync(MediaItem item);

        void ProcessMediaFailed(MediaItem item);

    }
}
