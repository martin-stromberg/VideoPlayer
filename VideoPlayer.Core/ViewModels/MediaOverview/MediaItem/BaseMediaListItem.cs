using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public enum CardItemApplicationArea { Single, InCollection }
    public class BaseMediaListItem : BaseViewModel
    {
        public BaseMediaListItem(ClassifiedEntry item)
        {
            Item = item;            
            Id = item.Id;            
            Tapped = new Command((sender) => ExecuteTapped(sender));
            UpdateMediaInformation(Item);
            ApplicationArea = CardItemApplicationArea.InCollection;
        }

        public long Id { get; }
        public ClassifiedEntry Item { get; }
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
        }
        protected string GetDateTimeInfo(params DateTime[] dates)
        {
            var actualDate = dates.FirstOrDefault(d => d != DateTime.MinValue);
            if (actualDate != DateTime.MinValue)
                return actualDate.ToString("dd.MM.yyyy");
            else
                return string.Empty;
        }

        private void ExecuteTapped(object sender)
        {
            try
            {
                Selected?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }
        public event EventHandler Selected;

        protected void LoadImage(string path)
        {
            Picture = ImageSource.FromFile(path);
        }
        public string Subtitle { get => GetProperty<string>(); set => SetProperty(value); }
        public Command Tapped { get; }
        public ImageSource Picture { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public bool IsCollection { get => GetProperty<bool>(); set => SetProperty(value); }
    }
}
