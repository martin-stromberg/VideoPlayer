using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.Playlists;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Services.Playlists
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
        
    }
}
