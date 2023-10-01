using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    public class SourcesViewModel : NavigationContentViewModel
    {
        public SourcesViewModel(IMediaLibrary mediaLibrary, IServiceProvider serviceProvider)
            :base(mediaLibrary, serviceProvider)
        {
        }

        protected override void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            base.MediaLibrary_ModelElementAdded(sender, e);
            if (e.Element is MediaSource)
                AddSource(e.Element as MediaSource);
        }
        protected override void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            base.MediaLibrary_ModelElementRemoved(sender, e);
            if (e.Element is MediaSource)
                RemoveSource(e.Element as MediaSource);
        }
        public override async void OnAppeared()
        {
            base.OnAppeared();
            if (isFirstAppeared)
            {
                isFirstAppeared = false;
                await base.ReadAllSourcesAsync();
            }
        }
        private bool isFirstAppeared = true;


        internal void AddSource(MediaSource source)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var vm = ServiceProvider.GetService<SourceBoxViewModel>();
                vm.Title = source.Name;
                vm.Source = source;
                Items.Add(vm);
            });

        }
        internal void RemoveSource(MediaSource source)
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
