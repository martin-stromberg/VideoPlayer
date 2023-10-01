using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyVideoPlayer.Helper.Navigation
{
    public class MediaSourceEventArgs: EventArgs
    {
        public MediaSourceEventArgs(MediaSource mediaSource) 
            :base()
        {
            MediaSource = mediaSource;
        }

        public MediaSource MediaSource { get; }
    }
}
