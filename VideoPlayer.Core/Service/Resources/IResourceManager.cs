using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Resources
{
    public interface IResourceManager
    {
        ImageSource GetDefaultItemPicture();
        ImageSource GetGenreIcon(Genre genre);
        CommunityToolkit.Maui.Views.MediaSource GetLoadingVideo();
    }
}
