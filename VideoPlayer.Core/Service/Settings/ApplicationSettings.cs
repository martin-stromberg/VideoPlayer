using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Extensions;

namespace VideoPlayer.Service.Settings
{
    public interface IApplicationSettings
    {
        bool ScanningEnabled { get; set;  }
        bool ClassificationEnabled { get; set; }
        bool ImageScrappingEnabled { get; set; }
        DateTime LastPictureOrphanagesCheck { get; set; }

        TimeSpan DownloadDueTimeCache { get; set; }
        TimeSpan DownloadDueTimeNextPlaylistCache { get; set; }
        TimeSpan DownloadDueTimeDownload { get; set; }
        TimeSpan DownloadDueTimeWatched { get; set; }
    }
    public class ApplicationSettings: IApplicationSettings
    {
        private const string IdScanningEnabled = "scanning_enabled";
        private const bool DefaultScanningEnabled = true;

        private const string IdClassificationEnabled = "classification_enabled";
        private const bool DefaultClassificationEnabled = true;

        private const string IdImageScrappingEnabled = "imagescrapping_enabled";
        private const bool DefaultImageScrappingEnabled = true;

        private const string IdLastPictureOrphanagesCheck = "lastPictureOrphanagesCheck";

        private const string IdDownloadDueTimeCache = "downloadDueTimeCache";
        private TimeSpan DefaultDownloadDueTimeCache = TimeSpan.FromHours(6);
        private const string IdDownloadDueTimeNextPlaylistCache = "downloadDueTimeNextPlaylistCache";
        private TimeSpan DefaultDownloadDueTimeNextPlaylistCache = TimeSpan.FromDays(3);
        private const string IdDownloadDueTimeDownload = "downloadDueTimeDownload";
        private TimeSpan DefaultDownloadDueTimeDownload = TimeSpan.FromDays(7);
        private const string IdDownloadDueTimeWatched = "downloadDueTimeWatched";
        private TimeSpan DefaultDownloadDueTimeWatched = TimeSpan.FromHours(1);

        #region Scanning
        public bool ScanningEnabled 
        {
            get => Preferences.Get(IdScanningEnabled, DefaultScanningEnabled); 
            set => Preferences.Set(IdScanningEnabled, value);
        }
        public bool ClassificationEnabled
        {
            get => Preferences.Get(IdClassificationEnabled, DefaultClassificationEnabled);
            set => Preferences.Set(IdClassificationEnabled, value);
        }
        public bool ImageScrappingEnabled
        {
            get => Preferences.Get(IdImageScrappingEnabled, DefaultImageScrappingEnabled);
            set => Preferences.Set(IdImageScrappingEnabled, value);
        }
        public DateTime LastPictureOrphanagesCheck
        {
            get => Preferences.Get(IdLastPictureOrphanagesCheck, DateTime.MinValue);
            set => Preferences.Set(IdLastPictureOrphanagesCheck, value);
        }
        #endregion

        #region Downloads
        public TimeSpan DownloadDueTimeCache {
            get => TimeSpan.FromMinutes(Preferences.Get(IdDownloadDueTimeCache, DefaultDownloadDueTimeCache.TotalMinutes));
            set => Preferences.Set(IdDownloadDueTimeCache, value.TotalMinutes);
        }
        public TimeSpan DownloadDueTimeNextPlaylistCache
        {
            get => TimeSpan.FromMinutes(Preferences.Get(IdDownloadDueTimeNextPlaylistCache, DefaultDownloadDueTimeNextPlaylistCache.TotalMinutes));
            set => Preferences.Set(IdDownloadDueTimeNextPlaylistCache, value.TotalMinutes);
        }
        public TimeSpan DownloadDueTimeDownload
        {
            get => TimeSpan.FromMinutes(Preferences.Get(IdDownloadDueTimeDownload, DefaultDownloadDueTimeDownload.TotalMinutes));
            set => Preferences.Set(IdDownloadDueTimeDownload, value.TotalMinutes);
        }
        public TimeSpan DownloadDueTimeWatched
        {
            get => TimeSpan.FromMinutes(Preferences.Get(IdDownloadDueTimeWatched, DefaultDownloadDueTimeWatched.TotalMinutes));
            set => Preferences.Set(IdDownloadDueTimeWatched, value.TotalMinutes);
        }
        #endregion
    }
}
