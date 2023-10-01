using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Models;

namespace VideoMeister.ViewModels.Navigation
{
    public class MediaItemViewModel : BaseMediaElementBoxViewModel
    {
        public MediaItem Item { get; internal set; }
    }
}
