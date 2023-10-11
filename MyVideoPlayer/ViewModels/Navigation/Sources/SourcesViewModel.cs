using MyVideoPlayer.ViewModels.Menu;
using System;
using System.Data;
using System.Linq;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    public class SourcesViewModel: NavigationContentViewModel
    {

        public SourcesViewModel(IMediaLibrary mediaLibrary, IServiceProvider serviceProvider)
            : base(mediaLibrary, serviceProvider) { }

        private SourcesMenuViewModel menuViewModel;

        public override MenuViewModel MenuViewModel
        {
            get
            {
                if (menuViewModel == null)
                {
                    menuViewModel = new SourcesMenuViewModel();
                    menuViewModel.CommandExecuted += MenuViewModel_CommandExecuted;
                }
                return menuViewModel;
            }
        }

        private async void MenuViewModel_CommandExecuted(object sender, MenuActionEventArgs e)
        {
            switch (e.Action.CommandParameter)
            {
                case SourcesMenuViewModel.CommandName_NewSource:
                    var newSource = new MediaSource() { Name = "Neue Quelle" };
                    await MediaLibrary.AddSourceAsync(newSource);
                    OnNavigationRequest(new ViewModelEventArgs(CreateViewModel(typeof(SourceConfigurationViewModel), newSource)));
                    break;
            }
        }

        protected override void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e)
        {
            base.MediaLibrary_ModelElementUpdated(sender, e);
            if (e.Element is MediaSource)
                UpdateSource(e.Element as MediaSource);
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
                await ReadAllSourcesAsync();
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

        private void UpdateSource(MediaSource mediaSource)
        {
            var vm = Items.Cast<SourceBoxViewModel>().FirstOrDefault(item => item.Source.Id == mediaSource.Id);
            if (vm == null)
                return;
            MainThread.BeginInvokeOnMainThread(() => { vm.Source.Update(mediaSource); });
        }

        internal void RemoveSource(MediaSource source)
        {
            var existing = Items.Cast<SourceBoxViewModel>().FirstOrDefault(i => i.Source.Id == source.Id);
            if (existing == null)
                return;
            MainThread.BeginInvokeOnMainThread(() => { Items.Remove(existing); });
        }

    }
}
