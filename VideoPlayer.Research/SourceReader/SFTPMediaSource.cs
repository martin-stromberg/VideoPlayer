using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Research.SourceReader
{
    public class SFTPMediaSource: MediaSource
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
