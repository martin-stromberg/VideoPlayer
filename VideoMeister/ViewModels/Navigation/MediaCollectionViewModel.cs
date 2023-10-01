using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.Models;

namespace VideoMeister.ViewModels.Navigation
{
    public class MediaCollectionViewModel : NavigationContentViewModel
    {
        internal void AddItems(IEnumerable<MediaItem> items)
        {
            foreach (var item in items)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Items.Add(new MediaItemViewModel()
                    {
                        Name = item.Name,
                        Item = item
                    });
                });
            
        }
    }
}
