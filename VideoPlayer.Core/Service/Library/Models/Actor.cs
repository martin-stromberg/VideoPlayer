using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataActor))]
    public class Actor : BaseServiceModel, IPicturedEntry
    {
        public Actor(BaseDataModel dataModel) : base(dataModel)
        {
            if (dataModel is not null)
            {
                PicturePath = ((DataActor)dataModel).PicturePath;
                ThumbUri = ((DataActor)DataModel).ThumbUri;
                NeedsPictureUpdate = ((DataActor)DataModel).NeedsPictureUpdate;
                PictureBackgroundColor = ((DataActor)DataModel).PictureBackgroundColor;
                BannerPath = ((DataActor)DataModel).BannerPath;
                BannerBackgroundColor = ((DataActor)DataModel).BannerBackgroundColor;
                LastPictureUpdateTry = ((DataActor)DataModel).LastPictureUpdateTry;
                RoleCount = ((DataActor)DataModel).RoleCount;
                RoleCountUpdated = ((DataActor)DataModel).RoleCountUpdated;
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataActor)DataModel).PicturePath = PicturePath;
                ((DataActor)DataModel).ThumbUri = ThumbUri;
                ((DataActor)DataModel).NeedsPictureUpdate = NeedsPictureUpdate;
                ((DataActor)DataModel).PictureBackgroundColor = PictureBackgroundColor;
                ((DataActor)DataModel).BannerPath = BannerPath;
                ((DataActor)DataModel).BannerBackgroundColor = BannerBackgroundColor;
                ((DataActor)DataModel).LastPictureUpdateTry = LastPictureUpdateTry;
                ((DataActor)DataModel).RoleCount = RoleCount;
                ((DataActor)DataModel).RoleCountUpdated = RoleCountUpdated;
                
            }
        }
        public string PicturePath { get => GetProperty<string>(); set => SetProperty(value); }
        public string ThumbUri { get => GetProperty<string>(); set => SetProperty(value); }
        public bool NeedsPictureUpdate { get => GetProperty<bool>(); set => SetProperty(value); }

        public string PictureBackgroundColor { get => GetProperty<string>(); set => SetProperty(value); }

        public string BannerPath { get => GetProperty<string>(); set => SetProperty(value); }

        public string BannerBackgroundColor { get => GetProperty<string>(); set => SetProperty(value); }
        public DateTime LastPictureUpdateTry { get => GetProperty<DateTime>(); set => SetProperty(value); }
        public int RoleCount { get => GetProperty<int>(); set => SetProperty(value); }
        public bool RoleCountUpdated { get => GetProperty<bool>(); set => SetProperty(value); }
    }
}
