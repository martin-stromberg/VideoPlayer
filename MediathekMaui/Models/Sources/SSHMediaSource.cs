using Mediathek.Services.Database;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Mediathek.Models.Sources
{
    [DataModelReference(
        typeof(Services.Database.Models.MediaDataSource),
        FilterPropertyName = nameof(Type),
        FilterPropertyValue = nameof(SSHMediaSource))]
    public class SSHMediaSource: RemoteMediaSource
    {

        public SSHMediaSource()
            : base()
        {
            Type = nameof(SSHMediaSource);
            PathDelimiter = '/';
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

        protected override void UpdateFromDataModel(Services.Database.Models.BaseDataModel dataModel)
        {
            BeginUpdate();
            try
            {
                base.UpdateFromDataModel(dataModel);
                try
                {
                    var obj = JsonConvert.DeserializeObject<SSHMediaSource>(Configuration);
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

        public override void Update(MediaElementSource newSource)
        {
            base.Update(newSource);
            ServerName = ((SSHMediaSource)newSource).ServerName;
            Username = ((SSHMediaSource)newSource).Username;
            Password = ((SSHMediaSource)newSource).Password;
        }

    }
}
