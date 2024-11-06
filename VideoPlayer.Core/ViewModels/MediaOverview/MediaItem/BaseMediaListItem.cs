using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.ViewModels.MediaOverview.MediaItem
{
    public class BaseMediaListItem : BaseViewModel
    {
        public BaseMediaListItem(ClassifiedEntry item)
        {
            Item = item;
            Title = item.Name;
            Id = item.Id;
            Subtitle = item.ReleaseDate.ToString("dd.MM.yyyy");
            Tapped = new Command((sender) => ExecuteTapped(sender));
        }

        public long Id { get; }
        public ClassifiedEntry Item { get; }

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
    }
}
