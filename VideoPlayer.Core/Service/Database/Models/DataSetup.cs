using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    public class DataSetup : BaseDataModel
    {
        public TimeSpan DownloadManager_DueTime_Cache { get => GetProperty<TimeSpan>(); set => SetProperty(value); }
        public TimeSpan DownloadManager_DueTime_Download { get => GetProperty<TimeSpan>(); set => SetProperty(value); }
        public TimeSpan DownloadManager_DueTime_Watched { get => GetProperty<TimeSpan>(); set => SetProperty(value); }
        public TimeSpan DownloadManager_DueTime_NextPlaylistCache { get => GetProperty<TimeSpan>(); set => SetProperty(value); }
    }
}
