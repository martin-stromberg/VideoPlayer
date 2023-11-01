using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
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

    }
}
