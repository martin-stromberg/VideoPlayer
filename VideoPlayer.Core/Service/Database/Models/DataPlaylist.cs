using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.Database.Models
{
    public class DataPlaylist: BaseDataModel
    {
        public enum PlaylistType
        {

            General,
            User,
            TVShowCollection

        }
        public PlaylistType Type
        {
            get
            {
                return GetProperty<PlaylistType>();
            }
            set
            {
                SetProperty<PlaylistType>(value);
            }
        }

        public bool AutoDownload {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }
        public int CurrentPosition {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(value);
            }
        }
        public bool BagMode {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }
    }
}
