using System;
using System.Linq;

namespace VideoPlayer.Services.Database.Models
{
    public class Settings: BaseDataModel
    {

        private const int Default_PlaybackHistory_IgnoreSecondsAtVideoEnding = 30;
        private const int Default_PlaybackHistory_IgnoreSecondsAtVideoStart = 5;
        private const int Default_PlaybackHistory_SavePositionIntervallSeconds = 5;
        private const bool Default_LibraryScan_AutomaticScan = true;
        private const int Default_LibraryScan_ScanIntervalHours = 24;

        public int PlaybackHistory_SavePositionIntervallSeconds { get; set; } = Default_PlaybackHistory_SavePositionIntervallSeconds;

        public int PlaybackHistory_IgnoreSecondsAtVideoStart { get; set; } = Default_PlaybackHistory_IgnoreSecondsAtVideoStart;

        public int PlaybackHistory_IgnoreSecondsAtVideoEnding { get; set; } = Default_PlaybackHistory_IgnoreSecondsAtVideoEnding;

        public bool LibraryScan_AutomaticScan { get; set; } = Default_LibraryScan_AutomaticScan;

        public int LibraryScan_ScanIntervalHours { get; set; } = Default_LibraryScan_ScanIntervalHours;

    }
}
