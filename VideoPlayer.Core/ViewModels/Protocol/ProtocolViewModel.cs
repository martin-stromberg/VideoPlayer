using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.ViewModels.Protocol
{
    public class ProtocolListEntryViewModel : BaseViewModel
    {

        public ProtocolListEntryViewModel(ProtocolEntry entry)
        {
            Description = entry.Description;
            CreatedAt = entry.CreatedAt;
        }

        public string Description { get; private set; }
        public DateTime CreatedAt { get; private set; }
    }
    public class ProtocolViewModel: BaseViewModel
    {
        private readonly IMediaLibrary mediaLibrary;

        public ProtocolViewModel(
            IMediaLibrary mediaLibrary)
            :base()
        {
            this.mediaLibrary = mediaLibrary;
        }

        public ObservableCollection<ProtocolListEntryViewModel> Items { get; } = new ObservableCollection<ProtocolListEntryViewModel>();
        public ClassifiedEntry Entry { get; private set; }
        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            if (Entry is not null)
                LoadEntries();
        }

        internal void LoadParent(string elementType, long elementId)
        {
            var entry = mediaLibrary.GetClassifiedEntry(elementId);
            if (entry is null) return;
            if (entry.GetType().Name != elementType)
            {
                mediaLibrary.Release(entry);
                return;
            }
            Entry = entry;
            if (IsAppeared)
                LoadEntries();
        }

        private void LoadEntries()
        {
            var entries = mediaLibrary.GetProtocolEntries(Entry);
            Items.Clear();
            foreach (var e in entries
                .Select(e => new ProtocolListEntryViewModel(e)))
                Items.Add(e);
        }
    }
}
