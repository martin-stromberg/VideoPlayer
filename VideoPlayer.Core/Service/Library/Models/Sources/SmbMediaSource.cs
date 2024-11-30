using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.Models.Sources
{
    [DataModelReference(typeof(MediaDataSource))]
    public class SmbMediaSource: MediaSource
    {

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
        public string ShareName
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

        public string Path
        {
            get => $"\\\\{Servername}\\{ShareName}\\{RootPath}";
        }
    }
}
