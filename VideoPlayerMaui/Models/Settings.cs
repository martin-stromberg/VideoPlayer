using System;
using System.Linq;
using VideoPlayer.Models.Attributes;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Models
{
    [DataModelReference(typeof(Services.Database.Models.Settings))]
    public class Settings: BaseModel
    {

        #region Abspielhistorie
        public int PlaybackHistory_IgnoreSecondsAtVideoStart
        {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(Math.Max(1, value));
            }
        }

        public int PlaybackHistory_SavePositionIntervallSeconds
        {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(Math.Max(1, value));
            }
        }

        public int PlaybackHistory_IgnoreSecondsAtVideoEnding
        {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(Math.Max(1, value));
            }
        }
        #endregion

        #region Quellscann
        public bool LibraryScan_AutomaticScan
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

        public int LibraryScan_ScanIntervalHours
        {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(Math.Max(1, value));
            }
        }
        #endregion

        #region Videowiedergabe
        public enum ControlStyle
        {

            [Translation("de", "System")]
            System,
            [Translation("de", "Eigenes")]
            Own

        }

        public ControlStyle Player_ControlStyle
        {
            get
            {
                return GetProperty<ControlStyle>();
            }
            set
            {
                SetProperty<ControlStyle>(value);
            }
        }
        #endregion

    }
}