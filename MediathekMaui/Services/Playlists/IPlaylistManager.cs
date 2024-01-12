using System;
using System.Linq;

namespace Mediathek.Services.Playlists
{
    public interface IPlaylistManager
    {

        Task InitializeAsync();

        Playlist GeneralPlaylist { get; }

        Task StartTVShowPlaylistAsync(TVShowEpisode episode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task StartTVShowPlaylistAsync(TVShow show);

        Task StartTVShowPlaylistAsync(TVShowSeason season);

        Task StartMoviePlaylistAsync(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task StartMoviePlaylistAsync(MovieCollection movieCollection);

        Task StartMediaItemPlaylistAsync(MediaItem mediaItem);

        Task StartPlaybackAsync();

        DownloadSource GetFirstVideoSource();

        DownloadSource ProcessMediaEnded(MediaItem item);

        void ProcessMediaFailed(MediaItem item);

    }
}
