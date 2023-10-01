using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.VideoSources;

namespace VideoMeister.ViewModels.Navigation
{
    public class SourcesViewModel : NavigationContentViewModel
    {
        internal void AddSource(VideoSource source)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var vm = new SourceBoxViewModel()
                {
                    Name = source.Name,
                    Source = source
                };
                Items.Add(vm);
            });
            
        }
        internal void RemoveSource(VideoSource source)
        {
            var existing = Items.Cast<SourceBoxViewModel>()
                .FirstOrDefault(i => i.Source.Equals(source));
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Items.Remove(existing);
            });
        }
    }
}
