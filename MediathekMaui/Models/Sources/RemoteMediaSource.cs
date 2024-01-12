using Mediathek.Services.Database.Models;
using Newtonsoft.Json;
using System.ComponentModel;

namespace Mediathek.Models.Sources
{
    public class RemoteMediaSource: MediaElementSource
    {

        private int updateLevel = 0;

        public RemoteMediaSource()
            : base()
        {
            PathDelimiter = '\\';
        }

        protected void BeginUpdate()
        {
            updateLevel += 1;
        }

        protected void EndUpdate()
        {
            updateLevel -= 1;
        }

        public override void ResetScan()
        {
            base.ResetScan();
            LatestScanPath = string.Empty;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if ((e.PropertyName != nameof(Configuration)) && (updateLevel == 0))
                UpdateConfiguration();
        }

        public override void Update(MediaElementSource newSource)
        {
            base.Update(newSource);
            LatestScanPath = ((RemoteMediaSource)newSource).LatestScanPath;
            Path = ((RemoteMediaSource)newSource).Path;
            PathDelimiter = ((RemoteMediaSource)newSource).PathDelimiter;
        }

        protected virtual void UpdateConfiguration()
        {
            Configuration = string.Empty;
            Configuration = JsonConvert.SerializeObject(this);
        }

        public string LatestScanPath
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

        public virtual string Path
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

        public char PathDelimiter
        {
            get
            {
                return GetProperty<char>();
            }
            protected set
            {
                SetProperty<char>(value);
            }
        }

        protected override void UpdateFromDataModel(BaseDataModel dataModel)
        {
            BeginUpdate();
            try
            {
                base.UpdateFromDataModel(dataModel);
                try
                {
                    var obj = JsonConvert.DeserializeObject<RemoteMediaSource>(Configuration);
                    Path = obj.Path;
                    LatestScanPath = obj.LatestScanPath;
                }
                catch
                {
                    Path = string.Empty;
                }
            }
            finally
            {
                EndUpdate();
            }
        }

        public override bool MustCache(MediaItems.MediaItem mediaItem)
        {
            return true;
        }

    }
}
