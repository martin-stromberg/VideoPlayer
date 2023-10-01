using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Helper;
using VideoMeister.Services.Models;
using VideoMeister.Services.VideoSources;
using VideoMeister.ViewModels.Navigation;

namespace VideoMeister.ViewModels
{
    public class MainPageViewModel: BaseViewModel
    {
        private readonly VideoSourceManager videoSourceManager;
        private readonly NavigationManager navigationManager;

        public MainPageViewModel(VideoSourceManager videoSourceManager) 
            :base()
        {
            this.videoSourceManager = videoSourceManager;
            VideoVisible = false;
            NavigationContent = null;

            navigationManager = new NavigationManager(videoSourceManager);
            navigationManager.NavigationCompleted += NavigationManager_NavigationCompleted;
            navigationManager.PlaybackRequest += NavigationManager_PlaybackRequest;

            //var firstSource = FindFirstFile(videoSourceManager.Sources.FirstOrDefault());
            //VideoSource = videoSourceManager.CreateVideoSource(firstSource);
            //VideoVisible = true;
        }

        private void NavigationManager_PlaybackRequest(object sender, MediaSourceEventArgs e)
        {
            VideoSource = e.Source;
        }

        private void NavigationManager_NavigationCompleted(object sender, Helper.NavigationEventArgs e)
        {
            NavigationContent = e.ContentViewModel;
        }

        private MediaItem FindFirstFile(VideoSource videoSource)
        {
            var file = videoSource.Files.FirstOrDefault();
            if (file != null)
                return file;
            var folder = videoSource.Folders.FirstOrDefault(folder => folder.Files.Any() || FindFirstFile(folder) != null);
            if (folder != null)
                return FindFirstFile(folder);
            return null;
        }

        private MediaItem FindFirstFile(MediaItemCollection folder)
        {
            var file = folder.Files.FirstOrDefault();
            if (file != null)
                return file;
            var folder2 = folder.Folders.FirstOrDefault(f => f.Files.Any() || FindFirstFile(f) != null);
            if (folder2 != null)
                return FindFirstFile(folder2);
            return null;
        }

        internal void ProcessMediaEnded()
        {
            VideoSource = null;
        }

        internal void ProcessMediaFailed(string errorMessage)
        {
            VideoSource = null;
        }

        internal void ProcessMediaOpened()
        {
            
        }

        internal void ProcessMediaPositionChanged(TimeSpan position)
        {
            
        }

        internal void ProcessMediaSeekCompleted()
        {
            
        }

        internal void OnPageAppearing()
        {
            videoSourceManager.Initialize();
        }

        public bool VideoVisible
        {
            get { return GetProperty<bool>(); }
            set { SetProperty<bool>(value); }
        } 
        public MediaSource VideoSource
        {
            get { return GetProperty<MediaSource>(); }
            set
            {
                SetProperty<MediaSource>(value);
                VideoVisible = value != null;
            }
        }

        public bool NavigationVisible
        {
            get { return GetProperty<bool>(); }
            set { SetProperty<bool>(value); }
        }
        public NavigationContentViewModel NavigationContent
        {
            get { return GetProperty<NavigationContentViewModel>(); }
            set {                 
                SetProperty<NavigationContentViewModel>(value);                
                NavigationVisible = value != null; 
            }
        }
    }
}
