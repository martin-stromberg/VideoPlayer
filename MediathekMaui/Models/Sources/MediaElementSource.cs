using Mediathek.Services.Database;
using Mediathek.Services.Database.Models;
using System;
using System.Linq;

namespace Mediathek.Models.Sources
{

    [DataModelReference(typeof(MediaSource))]
    public class MediaElementSource: BaseModel
    {

        public static MediaElementSource New()
        {
            return new MediaElementSource();
        }

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

        public bool CompleteNextScan
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

        public virtual string GetItemPath(MediaItems.MediaItem item)
        {
            return item.Path;
        }

        public virtual bool MustCache(MediaItems.MediaItem mediaItem)
        {
            return false;
        }

        public virtual void Update(MediaElementSource newSource)
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
