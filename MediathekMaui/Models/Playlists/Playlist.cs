using Mediathek.Services.Database;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Mediathek.Models.Playlists
{
    public enum PlaylistType
    {

        General,
        User,
        TVShowCollection

    }

    [DataModelReference(typeof(Services.Database.Models.Playlist))]
    public class Playlist: BaseModel
    {

        public PlaylistType Type
        {
            get
            {
                return GetProperty<PlaylistType>();
            }
            set
            {
                SetProperty<PlaylistType>(value);
            }
        }

        public ObservableCollection<PlaylistEntry> Items { get; } = new ObservableCollection<PlaylistEntry>();
        public int CurrentPosition { get; internal set; }

        public void Add(PlaylistEntry entry)
        {
            entry.PlaylistId = Id;
            Items.Add(entry);
        }

        public void Add(MediaItem entry)
        {
            Add(new PlaylistEntry() { Item = entry });
        }

        public void RemoveUpTo(MediaItem item)
        {
            foreach (var listItem in Items.TakeWhile(i =>
                                                     (i.Item.Id != item.Id) && (i.Item.Id != item.OriginalMediaItemId))
                                          .ToArray())
                Remove(listItem);
            foreach (var listItem in Items.TakeWhile(i =>
                                                     (i.Item.Id == item.Id) || (i.Item.Id == item.OriginalMediaItemId))
                                          .ToArray())
                Remove(listItem);
        }

        private void Remove(PlaylistEntry listItem)
        {
            Items.Remove(listItem);
        }

        public override void UpdateAutoincrements(Services.Database.Models.BaseDataModel dataModel)
        {
            base.UpdateAutoincrements(dataModel);
            foreach (var item in Items)
                item.PlaylistId = Id;
        }

    }
}
