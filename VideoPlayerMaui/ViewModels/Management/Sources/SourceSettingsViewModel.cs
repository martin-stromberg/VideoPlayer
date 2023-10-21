using System;
using System.Linq;
using VideoPlayer.Models.Sources;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management.Sources
{
    public class SourceSettingsViewModel: BaseViewModel
    {

        private MediaSource source;

        public SourceSettingsViewModel(MediaSource source, IStatusPublisher statusPublisher)
            : base(statusPublisher)
        {
            this.source = source;
            Title = source?.Name;
        }

        public bool ContainsSource(MediaSource source)
        {
            return this.source.Id == source.Id;
        }

    }
}
