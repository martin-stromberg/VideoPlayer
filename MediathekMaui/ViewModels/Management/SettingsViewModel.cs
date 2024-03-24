using Mediathek.Extensions;
using Mediathek.Navigation;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.Management
{
    public class SettingsViewModel: BaseManagementContentViewModel
    {

        public SettingsViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService)
            : base(statusPublisher, navigationManager, settingsService)
        {
            Title = $"Einstellungen";
        }

        #region Videowiedergabe
        public int PlaybackHistory_SavePositionIntervallSeconds
        {
            get
            {
                return Settings.Current.PlaybackHistory_SavePositionIntervallSeconds;
            }
            set
            {
                Settings.Current.PlaybackHistory_SavePositionIntervallSeconds = value;
            }
        }

        private string[] _Player_ControlStyles = null;

        public string[] Player_ControlStyles
        {
            get
            {
                if (_Player_ControlStyles == null)
                {
                    var enumType = typeof(Settings.ControlStyle);
                    _Player_ControlStyles = Enum.GetNames(typeof(Settings.ControlStyle))
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
                return _Player_ControlStyles;
            }
        }

        public string Player_ControlStyle
        {
            get
            {
                var value = (Settings.ControlStyle)Settings.Current.Player_ControlStyle;
                var offset = Enum.GetValues(typeof(Settings.ControlStyle)).IndexOf(value);
                return Player_ControlStyles[offset];
            }
            set
            {
                var offset = Math.Max(0, Player_ControlStyles.IndexOf(value));
                var typeValue = (Settings.ControlStyle)Enum.GetValues(typeof(Settings.ControlStyle)).GetValue(offset);
                Settings.Current.Player_ControlStyle = typeValue;
            }
        }
        #endregion

        #region Startseite
        public int PlaybackHistory_IgnoreSecondsAtVideoStart
        {
            get
            {
                return Settings.Current.PlaybackHistory_IgnoreSecondsAtVideoStart;
            }
            set
            {
                Settings.Current.PlaybackHistory_IgnoreSecondsAtVideoStart = value;
            }
        }

        public int PlaybackHistory_IgnoreSecondsAtVideoEnding
        {
            get
            {
                return Settings.Current.PlaybackHistory_IgnoreSecondsAtVideoEnding;
            }
            set
            {
                Settings.Current.PlaybackHistory_IgnoreSecondsAtVideoEnding = value;
            }
        }
        #endregion

        #region Quellen
        public bool LibraryScan_AutomaticScan
        {
            get
            {
                return Settings.Current.LibraryScan_AutomaticScan;
            }
            set
            {
                Settings.Current.LibraryScan_AutomaticScan = value;
            }
        }

        public int LibraryScan_ScanIntervalHours
        {
            get
            {
                return Settings.Current.LibraryScan_ScanIntervalHours;
            }
            set
            {
                Settings.Current.LibraryScan_ScanIntervalHours = value;
            }
        }
        #endregion

        #region Downloads
        public bool Downloads_AutomaticUnloading
        {
            get
            {
                return Settings.Current.KeepingDuration != TimeSpan.Zero;
            }
            set
            {
                if (value)
                    Downloads_KeepingDays = "7";
                else
                    Downloads_KeepingDays = "0";
                SetProperty<bool>(value);
            }
        }

        public TimeSpan Downloads_KeepingDuration
        {
            get
            {
                return Settings.Current.KeepingDuration;
            }
            private set
            {
                Settings.Current.KeepingDuration = value;
                SetProperty<TimeSpan>(value);
            }
        }

        public string Downloads_KeepingDays
        {
            get
            {
                return Downloads_KeepingDuration.Days.ToString();
            }
            set
            {
                if (int.TryParse(value, out int days))
                {
                    Downloads_KeepingDuration = TimeSpan.FromDays(days);
                    SetProperty<string>(value);
                }
            }
        }
        #endregion

    }
}
