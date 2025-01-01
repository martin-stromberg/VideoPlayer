using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;

namespace VideoPlayer.Service.Playlists
{
    public class FavoritePlaylist : BasePlaylistService
    {
        public FavoritePlaylist(
            IMediaLibrary mediaLibrary, 
            IMediaCollectionSelector mediaCollectionSelector, 
            ILogger logger) 
            : base(mediaLibrary, mediaCollectionSelector, PlaylistType.Favorite, logger)
        {
            base.CorrectInvisibleMediaItems = false;
        }
        protected override Playlist InitCurrentPlaylist()
        {
            var playlist = base.InitCurrentPlaylist();
            playlist.AutoDownload = false;
            playlist.BagMode = false;
            return playlist;
        }

        public void Add(ClassifiedEntry entry)
        {
            if (Contains(entry)) return;
            Current.Add(new PlaylistEntry(null)
            {
                Entry = entry,
                Name = entry.Name,
            });
            SaveChanges();
        }
        public void Remove(ClassifiedEntry entry)
        {
            var existing = Current.Items.FirstOrDefault(item => item.EntryId == entry.Id);
            if (existing is null) return;
            Current.Items.Remove(existing);
            SaveChanges();
        }
        public bool Contains(ClassifiedEntry entry)
        {
            return Current.Items.Any(item => item.EntryId == entry.Id);
        }
    }
}
