using System;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataSetup))]
    public class Setup: BaseServiceModel
    {

        public Setup()
            : this(null) { }

        public Setup(DataSetup dataModel)
            : base(dataModel) 
        { 
            if (DataModel is not null)
            {
                DownloadManager_DueTime_Cache = ((DataSetup)DataModel).DownloadManager_DueTime_Cache;
                DownloadManager_DueTime_Download = ((DataSetup)DataModel).DownloadManager_DueTime_Download;
                DownloadManager_DueTime_Watched = ((DataSetup)DataModel).DownloadManager_DueTime_Watched;
            }
        }

        public TimeSpan DownloadManager_DueTime_Download 
        {   get => GetProperty<TimeSpan>();
            internal set
            {
                if (value == TimeSpan.Zero)
                    value = Default().DownloadManager_DueTime_Download;
                SetProperty(value);
            }
        }
        public TimeSpan DownloadManager_DueTime_Cache
        {
            get => GetProperty<TimeSpan>();
            internal set
            {
                if (value == TimeSpan.Zero)
                    value = Default().DownloadManager_DueTime_Cache;
                SetProperty(value);
            }
        }
        public TimeSpan DownloadManager_DueTime_Watched
        {
            get => GetProperty<TimeSpan>();
            internal set
            {
                if (value == TimeSpan.Zero)
                    value = Default().DownloadManager_DueTime_Watched;
                SetProperty(value);
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataSetup)DataModel).DownloadManager_DueTime_Cache = DownloadManager_DueTime_Cache;
                ((DataSetup)DataModel).DownloadManager_DueTime_Download= DownloadManager_DueTime_Download;
                ((DataSetup)DataModel).DownloadManager_DueTime_Watched = DownloadManager_DueTime_Watched;
            }
        }
        internal static Setup Default()
        {
            return new Setup()
            {
                DownloadManager_DueTime_Cache = TimeSpan.FromHours(6),
                DownloadManager_DueTime_Download = TimeSpan.FromDays(7),
                DownloadManager_DueTime_Watched = TimeSpan.FromHours(1)
            };
        }
    }
}
