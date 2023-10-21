using System;
using System.Linq;
using VideoPlayer.Models.Attributes;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.Sources
{

    [DataModelReference(typeof(Services.Database.Models.MediaSource))]
    public class MediaSource: BaseModel
    {

        public string Type
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        [Password]
        public string Configuration
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public DateTime LastScan
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public DateTime LastScanStart
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public bool Inactive
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public virtual string GetItemPath(MediaItem item)
        {
            return item.Path;
        }

        public virtual bool MustCache(MediaItem mediaItem)
        {
            return false;
        }

        public virtual void Update(MediaSource newSource)
        {
            if (!string.IsNullOrWhiteSpace(Type) && (Type != newSource.Type))
                throw new ApplicationException("Source type change is not supported.");
            Type = newSource.Type;
            Name = newSource.Name;
        }

        public virtual void ResetScan()
        {
            LastScan = DateTime.MinValue;
            LastScanStart = DateTime.MinValue;
        }

    }
}
