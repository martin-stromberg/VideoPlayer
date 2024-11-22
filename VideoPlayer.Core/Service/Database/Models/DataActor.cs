using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.Database.Models
{
    public class DataActor : BaseDataModel
    {
        public string PicturePath { get => GetProperty<string>(); set => SetProperty(value); }
        public string ThumbUri { get => GetProperty<string>(); set => SetProperty(value); }
        public bool NeedsPictureUpdate { get => GetProperty<bool>(); set => SetProperty(value); }
        public string PictureBackgroundColor { get => GetProperty<string>(); set => SetProperty(value); }

        public string BannerPath { get => GetProperty<string>(); set => SetProperty(value); }

        public string BannerBackgroundColor { get => GetProperty<string>(); set => SetProperty(value); }
        public DateTime LastPictureUpdateTry { get => GetProperty<DateTime>(); set => SetProperty(value); }
    }
}
