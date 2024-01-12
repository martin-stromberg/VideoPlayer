using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Mediathek.ViewModels.Management.Sources
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
            _MediaLibrary.ModelElementRemoved += _MediaLibrary_ModelElementRemoved;
            Title = $"Quellen";
            CreateNew = new Command(() => { ExecuteCreateNew(); });
        }

        private void _MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            RemoveSource(e.Element as MediaElementSource);
        }

        private void RemoveSource(MediaElementSource source)
        {
            if (source == null)
                return;
            var vm = Sources.FirstOrDefault(vm => vm.IsSource(source));
            if (vm != null)
                Sources.Remove(vm);
        }

        public ObservableCollection<SourceSettingsViewModel> Sources { get; } = new ObservableCollection<SourceSettingsViewModel>();

        public Command CreateNew { get; set; }

        private void ExecuteCreateNew()
        {
            var existing = Sources.FirstOrDefault(vm => vm.IsNew);
            if (existing != null)
                return;
            Sources.Insert(0, new SourceSettingsViewModel(MediaElementSource.New(),
                                                          StatusPublisher,
                                                          _MediaLibrary,
                                                          NavigationManager,
                                                          Settings,
                                                          _LibraryScanner));
        }

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
