using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public enum CardItemApplicationArea { Single, InCollection }
    
    public class BaseMediaListItem : BaseListItem
    {
        private readonly IResourceManager resourceManager;

        public BaseMediaListItem(ClassifiedEntry item, IResourceManager resourceManager)
            :base(item)
        {   
            UpdateMediaInformation(Item);
            ApplicationArea = CardItemApplicationArea.InCollection;
            this.resourceManager = resourceManager;
            UpdatePicture(Item as IPicturedEntry);
        }
        public ClassifiedEntry Item { get => base.Element as ClassifiedEntry; }
        public CardItemApplicationArea ApplicationArea
        {
            get => GetProperty<CardItemApplicationArea>();
            set
            {
                SetProperty(value);
                UpdateMediaInformation(Item);
            }
        }
        protected override void ElementPropertyChanged(PropertyChangedEventArgs e)
        {
            base.ElementPropertyChanged(e);
            UpdateMediaInformation(Item);
            switch(e.PropertyName)
            {
                case nameof(IPicturedEntry.PicturePath):
                    UpdatePicture(Item as IPicturedEntry);
                    break;
            }
        }

        protected virtual void UpdatePicture(IPicturedEntry item)
        {
            if (item is null)
                LoadDefaultImage();
            else if (CheckPictureExistance(item, item.PicturePath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, item.PicturePath));
            else if (CheckPictureExistance(item, item.BannerPath))
                LoadImage(PathTools.Combine(FileSystem.Current.AppDataDirectory, item.BannerPath));
            else
                LoadDefaultImage();
        }

        private bool CheckPictureExistance(IPicturedEntry item, string picturePath)
        {
            var result = !string.IsNullOrWhiteSpace(picturePath);
            if (!result) return result;
            result = File.Exists(PathTools.Combine(FileSystem.Current.AppDataDirectory, picturePath));
            return result;
        }

        protected virtual void UpdateMediaInformation(ClassifiedEntry item)
        {
            if (item is null)
            {
                Title = string.Empty;
                Subtitle = string.Empty;
                Watched = false;
            }
            else
            {
                Title = item.Name;
                Subtitle = GetDateTimeInfo(item.ReleaseDate, item.PremieredAt);
                Watched = item.LastWatched != DateTime.MinValue;
            }
        }
        protected string GetDateTimeInfo(params DateTime[] dates)
        {
            var actualDate = dates.FirstOrDefault(d => d != DateTime.MinValue);
            if (actualDate != DateTime.MinValue)
                return actualDate.ToString("dd.MM.yyyy");
            else
                return string.Empty;
        }


        protected void LoadDefaultImage()
        {
            Picture = resourceManager.GetDefaultItemPicture();
        }
        protected void LoadImage(string path)
        {
            if (File.Exists(path))
                Picture = ImageSource.FromFile(path);
            else 
                LoadDefaultImage();
        }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public bool IsCollection { get => GetProperty<bool>(); set => SetProperty(value); }

        public bool Watched { get => GetProperty<bool>(); set => SetProperty(value); }
    }
}
