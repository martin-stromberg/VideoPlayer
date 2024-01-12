using CommunityToolkit.Maui.Views;
using System;
using System.Linq;

namespace Mediathek.Models
{
    public class MediaSourceEventArgs: EventArgs
    {

        public MediaSourceEventArgs(MediaSource item)
        {
            Source = item;
        }

        public MediaSource Source { get; }

    }
}
