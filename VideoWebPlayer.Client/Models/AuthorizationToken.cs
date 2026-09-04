using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoWebPlayer.Client.Models
{
    public class AuthorizationToken
    {
        public string token { get; set; } = string.Empty;
        public DateTime expires { get; set; }
    }
}
