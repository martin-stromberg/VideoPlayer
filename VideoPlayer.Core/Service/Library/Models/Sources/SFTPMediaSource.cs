using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.Models.Sources
{
    [DataModelReference(typeof(MediaDataSource), ReferenceFieldName = nameof(MediaDataSource.Type), ReferenceFieldValue = nameof(MediaSourceType.SFTP))]
    public class SFTPMediaSource: MediaSource
    {
        public SFTPMediaSource()
            : this(null) { }
        public SFTPMediaSource(MediaDataSource dataModel)
             : base(dataModel)
        {
            if (dataModel is not null)
            {
                if (dataModel.Type != MediaSourceType.SFTP)
                    throw new ArgumentException(nameof(MediaDataSource.Type));

                var copy = JsonConvert.DeserializeObject(dataModel.Configuration, typeof(SFTPMediaSource)) as SFTPMediaSource;
                Servername = copy.Servername;
                Port = copy.Port;
                Username = copy.Username;
                Password = copy.Password;
                Servername = copy.Servername;
                RootPath = copy.RootPath;
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            ((MediaDataSource)DataModel).Type = MediaSourceType.SFTP;
        }
        public string Servername
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
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
                SetProperty(value);
            }
        }
        public string Password
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
            }
        }
        public string RootPath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty(value);
            }
        }
        public short Port
        {
            get => GetProperty<short>();
            set => SetProperty(value);
        }
    }
}
