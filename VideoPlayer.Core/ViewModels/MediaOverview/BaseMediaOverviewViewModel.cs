using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.Genres;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview
{
    public interface IReusableViewModel
    {
        void Reuse();
    }
    public class BaseMediaOverviewViewModel: BaseViewModel, IReusableViewModel
    {
        private readonly EntryType[] entryTypes;
        private readonly INavigationManager navigationManager;
        protected IResourceManager ResourceManager { get; }

        public BaseMediaOverviewViewModel(
            GenreSelectionViewModel genreSelectionViewModel,
            EntryType[] entryTypes,
            IMediaLibrary mediaLibrary,
            INavigationManager navigationManager,
            IProcessorCollection processorCollection,
            IResourceManager resourceManager)
            :base()
        {
            this.entryTypes = entryTypes;
            MediaLibrary = mediaLibrary;
            this.navigationManager = navigationManager;
            ResourceManager = resourceManager;
            genreSelectionViewModel.GenreLoaded += GenreSelectionViewModel_GenreLoaded;
            GenreSelectionContext = genreSelectionViewModel;
            Items.CollectionChanged += Items_CollectionChanged;
            MemoryInfo = new MemoryInformation(processorCollection);
        }
        protected virtual bool CheckViewGenre(Genre genre)
        {
            return true;
        }
        private void GenreSelectionViewModel_GenreLoaded(object sender, BaseServiceModelEventArgs e)
        {
            if (!CheckViewGenre(e.ModelObject as Genre))
                e.ModelObject = null;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
                foreach (var item in e.NewItems)
                    (item as BaseListItem).Selected += BaseMediaOverviewViewModel_Selected;
            if (e.OldItems is not null)
                foreach (var item in e.OldItems)
                {
                    (item as BaseListItem).Selected -= BaseMediaOverviewViewModel_Selected;
                    MediaLibrary.Release((item as BaseListItem)?.Element);
                }
        }

        private void BaseMediaOverviewViewModel_Selected(object sender, EventArgs e)
        {
            try
            {
                var vm = (BaseListItem)sender;
                navigationManager.OpenCard(vm, false);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        public void Reuse()
        {
            //foreach (var item in Items.Skip(10).ToList())
                //Items.Remove(item);
        }
        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            if (!Items.Any())
                LoadMedia();

            GenreSelectionContext?.ExecuteAppeared();
        }
        public override void ExecuteDisappeared()
        {
            base.ExecuteDisappeared();
            GenreSelectionContext?.ExecuteDisappeared();
        }
        public GenreSelectionViewModel GenreSelectionContext 
        { 
            get => GetProperty<GenreSelectionViewModel>(); 
            set 
            {
                var oldValue = GenreSelectionContext;
                if (oldValue is not null)
                    oldValue.GenreSelected -= GenreSelection_GenreSelected;
                SetProperty(value);
                if (value is not null)
                    value.GenreSelected += GenreSelection_GenreSelected;
            } 
        }

        private void GenreSelection_GenreSelected(object sender, string e)
        {
            currentGenre = e;
            Title = currentGenre;
            LoadNextMediaAsync(0);
        }

        public IMediaLibrary MediaLibrary { get; }
        public ObservableCollection<BaseListItem> Items { get; } = new ObservableCollection<BaseListItem>();
        private void LoadMedia()
        {
            LoadNextMediaAsync(0);
            
        }
        private string currentGenre = string.Empty;
        private bool loadNext = true;
        private bool loading = false;
        private object loadingBlock = new object();
        private Type[] mediaItemTypes = null;
        protected virtual void LoadNextMediaAsync(int offset, int count = 10)
        {
            lock (loadingBlock)
            {
                if (loading)
                {
                    loadNext = true;
                    return;
                }
                loading = true;
            }
            if (mediaItemTypes is null)
                mediaItemTypes = GetType().Assembly
                    .GetTypes()
                    .Where(t => t.IsAssignableTo(typeof(BaseMediaListItem)))
                    .ToArray();
            var itemsFound = false;
            try
            {                
                var items = MediaLibrary.GetOverview(offset, count, currentGenre, entryTypes);
                if (offset == 0)
                    Items.Clear();
                foreach (var item in items)
                {
                    itemsFound = true;
                    var itemType = item.GetType();
                    var mediaItemType = mediaItemTypes.FirstOrDefault(t =>
                    {
                        var attr = t.GetCustomAttribute(typeof(ServiceModelReferenceAttribute)) as ServiceModelReferenceAttribute;
                        if (attr is null)
                            return false;
                        return (attr.ServiceModelType == itemType);
                    });
                    var vm = mediaItemType is null 
                        ? new BaseMediaListItem(item, ResourceManager) 
                        : Activator.CreateInstance(mediaItemType, item, ResourceManager) as BaseMediaListItem;
                    Items.Add(vm);
                }
            }
            catch (Exception ex) 
            { 
                OnStatusReceived(ex.Message); 
            }            
            lock (loadingBlock)
            {
                loading = false;
                if (!loadNext)
                    return;
                loadNext = false;                
            }
            if (!itemsFound)
                return;
            LoadNextMediaAsync(Items.Count, count);
        }

        internal void LoadNextItems()
        {
            LoadNextMediaAsync(Items.Count);
        }

        #region Memory Info
        public IMemoryInformation MemoryInfo { get; }
        #endregion
    }
}
