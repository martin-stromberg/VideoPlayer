using System;
using System.Linq;
using VideoPlayer.Extensions;
using VideoPlayer.Models.Attributes;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Categorization
{
    public enum VideoType
    {

        [Translation("de", "Nicht ausgewählt")]
        None,
        [Translation("de", "Film")]
        Movie,
        [Translation("de", "Serie")]
        TVShow

    }

    public class MediaItemDetailsViewModel: BaseViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;

        public MediaItemDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager, settings)
        {
            _MediaLibrary = mediaLibrary;
        }

        public MediaItem Item
        {
            get
            {
                return GetProperty<MediaItem>();
            }
            set
            {
                SetProperty<MediaItem>(value);
                UpdateProperties();
            }
        }

        public string Path
        {
            get
            {
                return GetProperty<string>();
            }
            private set
            {
                SetProperty<string>(value);
            }
        }

        public string SourceName
        {
            get
            {
                return GetProperty<string>();
            }
            private set
            {
                SetProperty<string>(value);
            }
        }

        private string[] _VideoTypes = null;

        public string[] VideoTypes
        {
            get
            {
                if (_VideoTypes == null)
                {
                    var enumType = typeof(VideoType);
                    _VideoTypes = Enum.GetNames(typeof(VideoType))
                                      .Select(value =>
                                      {
                                          var memberInfos = enumType.GetMember(value);
                                          var enumValueMemberInfo = memberInfos.FirstOrDefault(m =>
                                                                                               m.DeclaringType == enumType);
                                          var valueAttributes = enumValueMemberInfo.GetCustomAttributes(typeof(TranslationAttribute), false);
                                          return ((TranslationAttribute)valueAttributes[0]).TranslationValue;
                                      })
                                      .ToArray();
                }
                return _VideoTypes;
            }
        }

        public string SelectedVideoTypeName
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
                SelectedVideoType = (VideoType)VideoTypes.IndexOf(value);
            }
        }

        protected VideoType SelectedVideoType
        {
            get
            {
                return GetProperty<VideoType>();
            }
            set
            {
                SetProperty<VideoType>(value);
                string newValue = VideoTypes[(int)value];
                if (newValue != SelectedVideoTypeName)
                    SelectedVideoTypeName = VideoTypes[(int)value];
            }
        }

        public ImageSource Picture
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            private set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public MediaInformation MetaInfo
        {
            get
            {
                return GetProperty<MediaInformation>();
            }
            private set
            {
                SetProperty<MediaInformation>(value);
                if (value is EpisodeInformation)
                    SelectedVideoType = VideoType.TVShow;
                else if (value is TVShowInformation)
                    SelectedVideoType = VideoType.Movie;
                else
                    SelectedVideoType = VideoType.None;
            }
        }

        private async Task<string> LoadSourceNameAsync()
        {
            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(Item.ParentCollectionId);
            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);
            return source.Name;
        }

        private async void UpdateProperties()
        {
            Title = Item?.Name ?? string.Empty;
            Path = Item?.Path ?? string.Empty;
            Picture = Item?.Picture ?? null;
            MetaInfo = Item?.MetaInfo;
            SourceName = await LoadSourceNameAsync();
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
        }

        public override void OnDisappeared(bool closing)
        {
            base.OnDisappeared(closing);
        }

    }
}
