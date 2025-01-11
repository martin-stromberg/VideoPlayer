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
    }
    public class ApplicationSettings: IApplicationSettings
    {
        private const string IdScanningEnabled = "scanning_enabled";
        private const bool DefaultScanningEnabled = true;

        private const string IdClassificationEnabled = "classification_enabled";
        private const bool DefaultClassificationEnabled = true;

        private const string IdImageScrappingEnabled = "imagescrapping_enabled";
        private const bool DefaultImageScrappingEnabled = true;

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
            get => Preferences.Get(IdImageScrappingEnabled, DateTime.MinValue);
            set => Preferences.Set(IdImageScrappingEnabled, value);
        }
    }
}
