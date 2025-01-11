using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.ViewModels.MediaOverview.Cards
{
    public class ActorCardViewModel: BaseCardViewModel
    {
        private readonly IResourceManager resourceManager;

        public ActorCardViewModel(
            IMediaLibrary mediaLibrary, 
            IResourceManager resourceManager, 
            INavigationManager navigationManager,
            Actor actor) 
            :base(mediaLibrary, navigationManager)
        {
            this.resourceManager = resourceManager;
            Actor = actor;
            Action = new Command((args) =>ExecuteAction(args));
        }

        private void ExecuteAction(object args)
        {
            switch (args)
            {
                case "rescan":
                    Notify(this, new Service.Events.NotificationEventArgs("ReloadPictures", Actor));
                    break;
            }
        }

        ~ActorCardViewModel()
        {

        }
        public Actor Actor { get; }
        public Command Action { get; }
        public ImageSource Picture { get => GetProperty<ImageSource>(); private set => SetProperty(value); }

        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            UpdateInformation(this.Actor);
        }
        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();
            var entries = MediaLibrary.GetActorsRoles(Actor.Id)
                .Select(role =>
                {
                    MediaLibrary.Release(role);
                    return role.EntryId;
                })
                .Distinct()
                .Select(entryId => MediaLibrary.GetClassifiedEntry(entryId));
            CollectionContext.Items.Clear();
            foreach (var entry in entries)
                if (entry is Movie)
                    CollectionContext.Items.Add(new MovieMediaListItem(entry, resourceManager));
                else if (entry is MovieCollection)
                    CollectionContext.Items.Add(new MovieCollectionMediaListItem(entry, resourceManager));
                else if (entry is TVShow)
                    CollectionContext.Items.Add(new TVShowMediaListItem(entry, resourceManager));
                else if (entry is TVShowSeason)
                    CollectionContext.Items.Add(new TVShowSeasonMediaListItem(entry, resourceManager));
                else if (entry is TVShowEpisode)
                    CollectionContext.Items.Add(new TVShowEpisodeMediaListItem(entry, resourceManager));
                else
                    CollectionContext.Items.Add(new BaseMediaListItem(entry, resourceManager));
        }
        private bool _IgnoreDisapper = false;
        public override void ExecuteDisappeared()
        {
            base.ExecuteDisappeared();
            if (!_IgnoreDisapper)
                CollectionContext.Items.Clear();
            _IgnoreDisapper = false;
        }
        private void UpdateInformation(Actor actor)
        {
            Title = actor.Name;
            SetPicture(actor as IPicturedEntry);
        }
        protected void SetPicture(IPicturedEntry picturedEntry)
        {
            if (picturedEntry is null) return;
            var cacheFolder = FileSystem.Current.AppDataDirectory;
            string picturePath = string.Empty;
            if (!string.IsNullOrWhiteSpace(picturedEntry.PicturePath))
            {
                picturePath = PathTools.Combine(cacheFolder, picturedEntry.PicturePath);
            }
            else if (!string.IsNullOrWhiteSpace(picturedEntry.BannerPath))
            {
                picturePath = PathTools.Combine(cacheFolder, picturedEntry.BannerPath);
            }
            if (File.Exists(picturePath))
                Picture = ImageSource.FromFile(picturePath);
            else
                Picture = null;
        }
        protected override void Select(BaseListItem listItem)
        {
            base.Select(listItem);
            _IgnoreDisapper = true;
            OpenCard(listItem);
        }
    }
}
