using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Mediathek.Models.PlaybackHistory
{
    public class History
    {

        public ObservableCollection<HistoryEntry> Items { get; } = new ObservableCollection<HistoryEntry>();

    }
}
