using System;
using System.ComponentModel;
using System.Linq;
using VideoPlayer.Extensions;
using VideoPlayer.Models.Attributes;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.Services.MediaLibrary.Scanner;
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
        private readonly ILibraryScanner _LibraryScanner;

        public MediaItemDetailsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings,
            IMediaLibrary mediaLibrary,
            ILibraryScanner libraryScanner)
            : base(statusPublisher, navigationManager, settings)
        {
            _LibraryScanner = libraryScanner;
            _MediaLibrary = mediaLibrary;
            Save = new Command(() => ExecuteSaveAsync(), () => CanSave());
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            Save.ChangeCanExecute();
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
                IsMovie = value == VideoType.Movie;
                IsTVShow = value == VideoType.TVShow;
            }
        }

        public ImageSource Picture
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public string Name
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(System.IO.Path.GetExtension(value)))
                    value = System.IO.Path.GetFileNameWithoutExtension(value);
                SetProperty<string>(value);
            }
        }

        public string Plot
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string Genre
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public DateTime ReleaseDate
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
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
                {
                    SelectedVideoType = VideoType.TVShow;
                    Name = ((EpisodeInformation)value).Title ?? Name;
                    ShowName = ((EpisodeInformation)value).ShowName ?? ShowName;
                    EpisodeNo = ((EpisodeInformation)value).Episode ?? EpisodeNo;
                    SeasonNo = ((EpisodeInformation)value).Season ?? SeasonNo;
                }
                else if (value is MovieInformation)
                {
                    SelectedVideoType = VideoType.Movie;
                    Name = ((MovieInformation)value).Title ?? Name;
                    Plot = ((MovieInformation)value).Plot ?? Plot;
                    Genre = ((MovieInformation)value).Genre ?? Genre;
                    ReleaseDate = ((MovieInformation)value).ReleaseDate;
                }
                else
                    SelectedVideoType = VideoType.None;
            }
        }

        public string ShowName
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string SeasonNo
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string EpisodeNo
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public bool IsMovie
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public bool IsTVShow
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public Command Save { get; set; }

        private async Task<string> LoadSourceNameAsync()
        {
            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(Item.ParentCollectionId);
            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);
            return source.Name;
        }

        private async void UpdateProperties()
        {
            var collection = (Item == null) ? null : (await _MediaLibrary.GetMediaItemCollectionAsync(Item.ParentCollectionId));
            var seasonCollection = collection;
            while ((collection != null) && (collection.ParentCollectionId != 0)
                && ((collection.MetaInfo as TVShowInformation) == null))
                collection = await _MediaLibrary.GetMediaItemCollectionAsync(collection.ParentCollectionId);
            Title = Item?.Name ?? string.Empty;
            Path = Item?.Path ?? string.Empty;
            Picture = Item?.Picture ?? null;
            Name = Item?.Name ?? string.Empty;
            Plot = string.Empty;
            ReleaseDate = DateTime.MinValue;
            Genre = string.Empty;
            ShowName = (collection?.MetaInfo as TVShowInformation)?.Title ?? string.Empty;
            EpisodeNo = string.Empty;
            SeasonNo = ((seasonCollection != null)
                && ((collection?.MetaInfo as TVShowInformation) != null)) ? seasonCollection.Name : string.Empty;
            if (!string.IsNullOrWhiteSpace(ShowName))
                SelectedVideoType = VideoType.TVShow;
            else
                SelectedVideoType = VideoType.None;
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

        private void ExecuteSaveAsync()
        {
            switch (SelectedVideoType)
            {
                case VideoType.Movie:
                    MetaInfo = new MovieInformation()
                    {
                        Genre = Genre,
                        Plot = Plot,
                        Title = Name,
                        ReleaseDate = ReleaseDate,
                        Year = (ReleaseDate == DateTime.MinValue) ? 0 : ReleaseDate.Year
                    };
                    break;
                case VideoType.TVShow:
                    MetaInfo = new EpisodeInformation()
                    {
                        Title = Name,
                        ShowName = ShowName,
                        Episode = EpisodeNo.ToString(),
                        Season = SeasonNo.ToString()
                    };
                    break;
                default:
                    throw new NotImplementedException($"{SelectedVideoType}");
            }
            _LibraryScanner.SaveMetaInformation(Item, MetaInfo);
            NavigationManager.NavigateBack();
        }

        private bool CanSave()
        {
            return ((SelectedVideoType == VideoType.Movie) && !string.IsNullOrWhiteSpace(Name))
                || ((SelectedVideoType == VideoType.TVShow) && !string.IsNullOrWhiteSpace(Name)
                    && !string.IsNullOrWhiteSpace(ShowName) && int.TryParse(EpisodeNo, out var eNo) && (eNo > 0)
                    && int.TryParse(SeasonNo, out var sNo) && (sNo >= 0));
        }

    }
}
