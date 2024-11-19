using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public enum CardItemApplicationArea { Single, InCollection }
    public class BaseMediaListItem : BaseListItem
    {
        public BaseMediaListItem(ClassifiedEntry item)
            :base(item)
        {   
            UpdateMediaInformation(Item);
            ApplicationArea = CardItemApplicationArea.InCollection;
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

        protected virtual void UpdateMediaInformation(ClassifiedEntry item)
        {
            Title = item.Name;
            Subtitle = GetDateTimeInfo(item.ReleaseDate, item.PremieredAt);
            Watched = item.LastWatched != DateTime.MinValue;
        }
        protected string GetDateTimeInfo(params DateTime[] dates)
        {
            var actualDate = dates.FirstOrDefault(d => d != DateTime.MinValue);
            if (actualDate != DateTime.MinValue)
                return actualDate.ToString("dd.MM.yyyy");
            else
                return string.Empty;
        }

        

        protected void LoadImage(string path)
        {
            Picture = ImageSource.FromFile(path);
        }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public bool IsCollection { get => GetProperty<bool>(); set => SetProperty(value); }

        public bool Watched { get => GetProperty<bool>(); set => SetProperty(value); }
    }
}
