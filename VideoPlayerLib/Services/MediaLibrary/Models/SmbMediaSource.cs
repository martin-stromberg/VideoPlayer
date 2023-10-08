using Newtonsoft.Json;
using System;
using System.Linq;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(typeof(Database.Models.MediaSource), FilterPropertyName = nameof(Type), FilterPropertyValue = nameof(SmbMediaSource))]
    public class SmbMediaSource : RemoteMediaSource
    {
        public SmbMediaSource()
            : base()
        {
            Type = nameof(SmbMediaSource);
        }
        private void UpdateConfiguration()
        {
            Configuration = JsonConvert.SerializeObject(this);
        }
        public string Password
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); UpdateConfiguration(); }
        }
        public string Username
        {
            get { return GetProperty<string>(); }
            set { SetProperty<string>(value); UpdateConfiguration(); }
        }
        public override string Path
        {
            get => base.Path;
            set
            {
                base.Path = value;
                UpdateConfiguration();
            }
        }
        public string ServerName
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return string.Empty;
                var path = Path.Replace("\\", "/");
                if (path.EndsWith("/"))
                    path = path.Remove(path.Length - 1);
                path = path.Remove(0, 2);
                return path.Substring(0, path.IndexOf("/"));
            }
        }

        public string RelativePath
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return string.Empty;
                var path = Path.Replace("\\", "/");
                if (path.EndsWith("/"))
                    path = path.Remove(path.Length - 1);
                path = path.Remove(0, 2);
                path = path.Remove(0, ServerName.Length);
                return path;
            }
        }

        protected override void UpdateFromDataModel(BaseDataModel dataModel)
        {
            base.UpdateFromDataModel(dataModel);
            var obj = JsonConvert.DeserializeObject<SmbMediaSource>(Configuration);
            Password = obj.Password;
            Username = obj.Username;
            Path = obj.Path;
        }

        public override string GetItemPath(MediaItem item)
        {
            switch (item.CopyType)
            {
                case MediaItemCopyType.Cache:
                    return item.Path;
                default:
                    return $"\\\\{ServerName}{item.Path}";
            }
        }
    }
}
