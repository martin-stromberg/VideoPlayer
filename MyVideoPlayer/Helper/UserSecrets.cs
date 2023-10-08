using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace MyVideoPlayer.Helper
{
    public class UserSecrets
    {

        private IConfigurationRoot config;

        public UserSecrets()
            : base()
        {
            config = new ConfigurationBuilder()
                .AddUserSecrets<App>()
                .Build();
        }

        public string this[string name]
        {
            get
            {
                return config[name];
            }
        }

    }
}
