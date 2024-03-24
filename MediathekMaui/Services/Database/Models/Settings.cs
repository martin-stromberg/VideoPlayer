using System;
using System.Linq;

namespace Mediathek.Services.Database.Models
{
    public class Settings: BaseDataModel
    {

        private const int Default_PlaybackHistory_IgnoreSecondsAtVideoEnding = 30;
        private const int Default_PlaybackHistory_IgnoreSecondsAtVideoStart = 5;
        private const int Default_PlaybackHistory_SavePositionIntervallSeconds = 5;
        private const bool Default_LibraryScan_AutomaticScan = true;
        private const int Default_LibraryScan_ScanIntervalHours = 24;
        private const int Default_Player_ControlStyle = 1;
        private static TimeSpan Default_Download_KeepingDuration = TimeSpan.FromDays(7);

        public int DataVersion { get; set; } = 0;

        public int PlaybackHistory_SavePositionIntervallSeconds { get; set; } = Default_PlaybackHistory_SavePositionIntervallSeconds;

        public int PlaybackHistory_IgnoreSecondsAtVideoStart { get; set; } = Default_PlaybackHistory_IgnoreSecondsAtVideoStart;

        public int PlaybackHistory_IgnoreSecondsAtVideoEnding { get; set; } = Default_PlaybackHistory_IgnoreSecondsAtVideoEnding;

        public bool LibraryScan_AutomaticScan { get; set; } = Default_LibraryScan_AutomaticScan;

        public int LibraryScan_ScanIntervalHours { get; set; } = Default_LibraryScan_ScanIntervalHours;

        public int Player_ControlStyle { get; set; } = Default_Player_ControlStyle;

        public TimeSpan Download_KeepingDuration { get; set; } = Default_Download_KeepingDuration;

    }
}
