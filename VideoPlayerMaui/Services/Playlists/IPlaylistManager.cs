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

        Task StartTVShowPlaybackAsync(TVShowEpisode episode, Func<IEnumerable<BaseModel>> GetCollectionElements);

        Task StartMoviePlaybackAsync(Movie movie, Func<IEnumerable<BaseModel>> GetCollectionElements);

        DownloadSource GetFirstVideoSource();

        DownloadSource ProcessMediaEnded(MediaItem item);

    }
}
