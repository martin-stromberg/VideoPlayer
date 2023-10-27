using System;
using System.Linq;
using VideoPlayer.Models.Playlists;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Downloads;

namespace VideoPlayer.Services.Playlists
{
    public class PlaylistManager: IPlaylistManager
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaDownloader _MediaDownloader;

        public PlaylistManager(IMediaLibrary mediaLibrary, IMediaDownloader mediaDownloader)
        {
            _MediaDownloader = mediaDownloader;
            _MediaLibrary = mediaLibrary;
        }

        public async Task InitializeAsync()
        {
            await InitializeGeneralPlaylist();
        }

        private async Task InitializeGeneralPlaylist()
        {
            var playlist = (await _MediaLibrary.GetPlaylists(PlaylistType.General)).FirstOrDefault();
            if (playlist == null)
            {
                playlist = new Playlist() { Type = PlaylistType.General, Name = $"Allgemein" };
                await _MediaLibrary.AddPlaylistAsync(playlist);
            }
            GeneralPlaylist = playlist;
        }

        public Playlist GeneralPlaylist { get; private set; }

    }
}
