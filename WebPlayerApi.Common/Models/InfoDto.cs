using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebPlayerApi.Common.Models
{
    public class InfoDto
    {
        public string Host { get; set; }
        public string RemoteIpAddress { get; set; }
        public string[] HostAddresses { get; set; }
    }
}
