using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public class ActorListItem: BaseListItem
    {
        private readonly IResourceManager _ResourceManager;

        public ActorListItem(BaseServiceModel element, IResourceManager resourceManager)
            : base(element)
        {
            _ResourceManager = resourceManager;
            var actor = ((Actor)element);
            Title = actor.Name;
            Counter = actor.RoleCount > 1 ? actor.RoleCount : 0;
            if (!string.IsNullOrWhiteSpace(actor.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, actor.PicturePath));
            else
                LoadDefaultImage();
        }
        protected void LoadDefaultImage()
        {
            Picture = _ResourceManager.GetDefaultItemPicture();
        }
        protected void LoadImage(string path)
        {
            Picture = ImageSource.FromFile(path);
        }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public bool IsCollection { get => false; }
        public bool Watched { get => false; }
    }
}
