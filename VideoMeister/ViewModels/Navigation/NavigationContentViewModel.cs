using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoMeister.ViewModels.Navigation
{
    public class NavigationContentViewModel : BaseViewModel
    {
        public NavigationContentViewModel()
            :base()
        {
            Items.CollectionChanged += Items_CollectionChanged;
        }

        public string Title { get; set; }
        public ObservableCollection<BaseMediaElementBoxViewModel> Items { get; set; } = new ObservableCollection<BaseMediaElementBoxViewModel>();
        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var item in e.NewItems.Cast<BaseMediaElementBoxViewModel>())
                    item.Tapped -= Item_Tapped;
            if (e.NewItems != null)
                foreach (var item in e.NewItems.Cast<BaseMediaElementBoxViewModel>())
                    item.Tapped += Item_Tapped;            
        }

        private void Item_Tapped(object sender, EventArgs e)
        {
            BaseMediaElementBoxViewModel item = sender as BaseMediaElementBoxViewModel;
            if (!Items.Contains(item))
                return;
            OnItemTapped(new MediaElementBoxViewModelEventArgs(item));
        }
        public event EventHandler<MediaElementBoxViewModelEventArgs> ItemTapped;
        protected virtual void OnItemTapped(MediaElementBoxViewModelEventArgs e)
        {
            ItemTapped?.Invoke(this, e);
        }
    }
}
