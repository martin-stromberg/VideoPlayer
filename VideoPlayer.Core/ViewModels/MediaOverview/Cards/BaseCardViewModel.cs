using Microsoft.Extensions.Logging;
using System.Collections.Specialized;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    public class BaseCardViewModel: BaseViewModel, IEventPublisher, IMultiEventCollection
    {
        private readonly INavigationManager navigationManager;

        public BaseCardViewModel(IMediaLibrary mediaLibrary, INavigationManager navigationManager, ILogger logger)
            :base(logger)
        {
            MediaLibrary = mediaLibrary;
            this.navigationManager = navigationManager;
            CollectionContext = new MediaCollectionViewModel(logger);
            CollectionContext.Selected += CollectionContext_Selected;
            CollectionContext.Items.CollectionChanged += CollectionContext_Items_CollectionChanged;
        }
        protected IMediaLibrary MediaLibrary { get; }
        #region IEventPublisher, IMultiEventCollection
        public IEnumerable<IEventSubscriber> GetSubscribers()
        {
            return new IEventSubscriber[] { MediaLibrary as IEventSubscriber };
        }
        public IEnumerable<IEventPublisher> GetPublishers()
        {
            return new IEventPublisher[] { MediaLibrary as IEventPublisher };
        }
        #endregion

        #region Navigation
        protected virtual void Close()
        {
            navigationManager.CloseCurrentPage();
        }
        protected virtual void OpenCard(BaseListItem listItem)
        {
            navigationManager.OpenCard(listItem, false);
        }
        protected virtual void OpenProtocol(ClassifiedEntry entry)
        {
            navigationManager.OpenProtocol(entry.GetType().Name, entry.Id);
        }
        #endregion
        #region CollectionContext
        private void CollectionContext_Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var isEmpty = CollectionContext.Items.Count == 0;
            SetCollectionVisible(!isEmpty);
        }

        private void CollectionContext_Selected(object sender, BaseViewModelEventArgs e)
        {
            Select((e.ViewModel as BaseListItem));
        }

        public MediaCollectionViewModel CollectionContext { get; }
        
        protected virtual void Select(BaseListItem listItem)
        {
            
        }
        public bool CollectionVisible { get => GetProperty<bool>(); set { CollectionContext.Visible = value; SetProperty<bool>(value); } }
        protected virtual void SetCollectionVisible(bool visible)
        {
            CollectionVisible = visible;
        }
        #endregion

    }
}
