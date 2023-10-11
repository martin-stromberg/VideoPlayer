using Newtonsoft.Json;
using System;
using System.Linq;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.MediaLibrary.Models
{
    [DataModelReference(
        typeof(Database.Models.MediaSource),
        FilterPropertyName = nameof(Type),
        FilterPropertyValue = nameof(FtpMediaSource))]
    public class FtpMediaSource: RemoteMediaSource
    {

        public FtpMediaSource()
            : base()
        {
            Type = nameof(FtpMediaSource);
            PathDelimiter = '/';
        }

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

        public string ServerName
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

        protected override void UpdateFromDataModel(BaseDataModel dataModel)
        {
            BeginUpdate();
            try
            {
                base.UpdateFromDataModel(dataModel);
                try
                {
                    var obj = JsonConvert.DeserializeObject<FtpMediaSource>(Configuration);
                    Password = obj.Password;
                    Username = obj.Username;
                    ServerName = obj.ServerName;
                }
                catch { }
            }
            finally
            {
                EndUpdate();
            }
        }

        public override string GetItemPath(MediaItem item)
        {
            switch (item.CopyType)
            {
                case MediaItemCopyType.Cache:
                    return item.Path;
                default:
                    return $"ftp://{ServerName}{item.Path}";
            }
        }

        public override void Update(MediaSource newSource)
        {
            base.Update(newSource);
            ServerName = ((FtpMediaSource)newSource).ServerName;
            Username = ((FtpMediaSource)newSource).Username;
            Password = ((FtpMediaSource)newSource).Password;
        }

    }
}
