using MyVideoPlayer.Helper.Navigation;
using System;
using System.Linq;
using System.Windows.Input;
using VideoPlayerLib.Extensions;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    internal class SourceConfigurationViewModel: NavigationContentViewModel
    {

        private readonly IMediaLibrary _MediaLibrary;
        private INavigationManager navigationManager;
        private MediaSource currentSource;

        public enum SourceType
        {

            [Translation(LanguageCode = "de", Name ="Ftp")]
            Ftp,
            [Translation(LanguageCode = "de", Name = "Netzwerkfreigabe")]
            Smb

        }

        public IEnumerable<string> GetSourceTypeNames(string languageCode)
        {
            var enumType = typeof(SourceType);
            return Enum.GetValues(typeof(SourceType))
                       .Cast<SourceType>()
                       .Select(st =>
                       {
                           var memberInfos = enumType.GetMember(st.ToString());
                           var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType);
                           var valueAttributes = enumValueMemberInfo.GetCustomAttributes(typeof(TranslationAttribute), false)
                                                                    .Cast<TranslationAttribute>();
                           return valueAttributes.FirstOrDefault(a => a.LanguageCode == languageCode)?.Name ?? st.ToString();
                       });
        }

        public SourceConfigurationViewModel(
            MediaSource source,
            IMediaLibrary mediaLibrary,
            IServiceProvider serviceProvider)
            : base(mediaLibrary, serviceProvider)
        {
            _MediaLibrary = mediaLibrary;
            currentSource = source;
            navigationManager = serviceProvider.GetService<INavigationManager>();
            Name = source?.Name;
            switch (source.Type)
            {
                case nameof(FtpMediaSource):
                    Type = TypeNames.Skip((int)SourceType.Ftp).FirstOrDefault().ToString();
                    Host = ((FtpMediaSource)source).ServerName;
                    Username = ((FtpMediaSource)source).Username;
                    Password = ((FtpMediaSource)source).Password;
                    RootPath = ((FtpMediaSource)source).Path;
                    break;
                case nameof(SmbMediaSource):
                    Type = TypeNames.Skip((int)SourceType.Smb).FirstOrDefault().ToString();
                    Host = ((SmbMediaSource)source).ServerName;
                    Username = ((SmbMediaSource)source).Username;
                    Password = ((SmbMediaSource)source).Password;
                    RootPath = ((SmbMediaSource)source).Path;
                    break;
            }
            Cancel = new Command(() => ExecuteCancel());
            Save = new Command(() => ExecuteSave());
        }

        public ICommand Cancel
        {
            get
            {
                return GetProperty<ICommand>();
            }
            private set
            {
                SetProperty<ICommand>(value);
            }
        }

        public ICommand Save
        {
            get
            {
                return GetProperty<ICommand>();
            }
            private set
            {
                SetProperty<ICommand>(value);
            }
        }

        private void Close()
        {
            navigationManager.NavigateBack();
        }

        private void ExecuteCancel()
        {
            Close();
        }

        private async void ExecuteSave()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name))
                    throw new ApplicationException("No name is entered.");
                int offset = TypeNames.IndexOf(Type);
                if (offset < 0)
                    throw new ApplicationException("No source type is selected.");
                var currType = (SourceType)offset;
                MediaSource newSource;
                switch (currType)
                {
                    case SourceType.Ftp:
                        newSource = new FtpMediaSource()
                        {
                            Id = currentSource.Id,
                            Name = Name,
                            Password = Password,
                            Username = Username,
                            ServerName = Host,
                            Path = RootPath
                        };
                        break;
                    default:
                        throw new ApplicationException($"Source type {currType} is not supported.");
                }
                currentSource.Update(newSource);
                await _MediaLibrary.AddSourceAsync(currentSource);
                Close();
            }
            catch { }
        }

        public string Name
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

        public string[] TypeNames
        {
            get
            {
                return GetSourceTypeNames("de").ToArray();
            }
        }

        public string Type
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

        public string Host
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

        public string Username
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

        public string Password
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

        public string RootPath
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

    }
}
