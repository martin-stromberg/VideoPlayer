using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public class RoleListItem : BaseListItem
    {
        private readonly IResourceManager _ResourceManager;

        public RoleListItem(BaseServiceModel element, IResourceManager resourceManager) 
            : base(element)
        {
            _ResourceManager = resourceManager;
            var role = ((Role)element);
            Title = role.Name;
            Subtitle = role.Actor.Name;            
            if (!string.IsNullOrWhiteSpace(role.Actor.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, role.Actor.PicturePath));
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
