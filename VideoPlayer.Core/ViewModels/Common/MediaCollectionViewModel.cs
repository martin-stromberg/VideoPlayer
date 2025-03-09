using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;
using VideoPlayer.Views.MediaOverview;

namespace VideoPlayer.ViewModels.Common
{
    public interface IMediaCollectionViewModel
    {
        event EventHandler<BaseViewModelEventArgs> Selected;
        ObservableCollection<BaseListItem> Items { get; }
        bool Visible { get; set; }
    }
    public class MediaCollectionViewModel: ViewModels.BaseViewModel, IMediaCollectionViewModel
    {
        public MediaCollectionViewModel(ILogger logger)
            :base(logger)
        {
            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
                foreach (var item in e.NewItems.Cast<BaseListItem>())
                    Added(item);
            if (e.OldItems is not null)
                foreach (var item in e.OldItems.Cast<BaseListItem>())
                    Removed(item);
        }

        private void Removed(BaseListItem item)
        {
            item.Selected -= Item_Selected;
        }

        private void Added(BaseListItem item)
        {
            item.Selected += Item_Selected;
        }

        private void Item_Selected(object sender, EventArgs e)
        {
            var listItem = sender as BaseListItem;
            Selected?.Invoke(this, new BaseViewModelEventArgs(listItem));
        }
        public event EventHandler<BaseViewModelEventArgs> Selected;
        public ObservableCollection<BaseListItem> Items { get; } = new ObservableCollection<BaseListItem>();
        public bool Visible { get => GetProperty<bool>(); set => SetProperty(value); }
    }
}
