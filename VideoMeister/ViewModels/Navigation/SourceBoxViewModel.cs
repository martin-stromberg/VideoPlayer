using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.ViewModels.Navigation
{
    public class SourceBoxViewModel : BaseMediaElementBoxViewModel
    {
        public VideoSource Source { get; internal set; }
    }
}
