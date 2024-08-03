using Mediathek.Services.Database;
using Mediathek.Services.Database.Models;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Mediathek.Models.Sources
{
    [DataModelReference(
        typeof(MediaDataSource),
        FilterPropertyName = nameof(Type),
        FilterPropertyValue = nameof(SmbMediaSource))]
    public class SmbMediaSource: RemoteMediaSource
    {

        public SmbMediaSource()
            : base()
        {
            Type = nameof(SmbMediaSource);
        }

        private new void UpdateConfiguration()
        {
            Configuration = JsonConvert.SerializeObject(this);
        }

        [Password]
        public string Password
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

        public string Username
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

        public override string Path
        {
            get
            {
                return base.Path;
            }
            set
            {
                base.Path = value;
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
            BeginUpdate();
            try
            {
                base.UpdateFromDataModel(dataModel);
                var obj = JsonConvert.DeserializeObject<SmbMediaSource>(Configuration);
                Password = obj.Password;
                Username = obj.Username;
                Path = obj.Path;
            }finally { EndUpdate(); }
        }

        public override string GetItemPath(MediaItems.MediaItem item)
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
