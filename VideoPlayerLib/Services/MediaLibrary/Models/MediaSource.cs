using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.MediaSource))]
    public class MediaSource: BaseModel
    {
        public string Type
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); }
        }
        public string Configuration
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); }
        }
        public DateTime LastScan
        {
            get { return GetProperty<DateTime>(); }
            set { SetProperty<DateTime>(value); }
        }

        public virtual string GetItemPath(MediaItem item)
        {
            return item.Path;
        }

        public virtual bool MustCache(MediaItem mediaItem)
        {
            return false;
        }
    }

    public class RemoteMediaSource: MediaSource
    {
        private int updateLevel = 0;

        public RemoteMediaSource()
            :base()
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
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName != nameof(Configuration) && updateLevel == 0)
                UpdateConfiguration();
        }
        protected virtual void UpdateConfiguration()
        {
            Configuration = JsonConvert.SerializeObject(this);
        }
        public virtual string Path
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); }
        }

        public char PathDelimiter
        {
            get { return GetProperty<char>(); }
            protected set { SetProperty<char>(value); }
        }

        protected override void UpdateFromDataModel(BaseDataModel dataModel)
        {
            BeginUpdate();
            try
            {
                base.UpdateFromDataModel(dataModel);
                var obj = JsonConvert.DeserializeObject<RemoteMediaSource>(Configuration);
                Path = obj.Path;
            }
            finally 
            {
                EndUpdate();
            }
        }

        public override bool MustCache(MediaItem mediaItem)
        {
            return true;
        }
    }
}
