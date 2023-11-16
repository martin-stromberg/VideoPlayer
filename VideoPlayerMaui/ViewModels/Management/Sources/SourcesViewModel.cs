using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management.Sources
{
    public class SourcesViewModel: BaseManagementContentViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly ILibraryScanner _LibraryScanner;

        public SourcesViewModel(
            IMediaLibrary mediaLibrary,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            ILibraryScanner libraryScanner)
            : base(statusPublisher, navigationManager, settingsService)
        {
            _LibraryScanner = libraryScanner;
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
                var vm = new SourceSettingsViewModel(source,
                                                     StatusPublisher,
                                                     _MediaLibrary,
                                                     NavigationManager,
                                                     Settings,
                                                     _LibraryScanner);
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
