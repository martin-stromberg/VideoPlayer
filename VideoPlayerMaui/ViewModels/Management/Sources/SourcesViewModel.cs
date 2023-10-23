using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management.Sources
{
    public class SourcesViewModel: BaseManagementContentViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;

        public SourcesViewModel(
            IMediaLibrary mediaLibrary,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(statusPublisher, navigationManager)
        {
            _MediaLibrary = mediaLibrary;
            Title = $"Quellen";
        }

        public ObservableCollection<SourceSettingsViewModel> Sources { get; } = new ObservableCollection<SourceSettingsViewModel>();

        private async void LoadSources()
        {
            foreach (var source in await _MediaLibrary.GetSourcesAsync())
            {
                if (Sources.Any(vm => vm.ContainsSource(source)))
                    continue;
                var vm = new SourceSettingsViewModel(source, StatusPublisher, _MediaLibrary, NavigationManager);
                Sources.Add(vm);
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadSources();
        }

    }
}
