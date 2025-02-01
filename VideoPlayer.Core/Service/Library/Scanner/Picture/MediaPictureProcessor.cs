using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;

namespace VideoPlayer.Service.Library.Scanner.Picture
{
    public interface IMediaPictureProcessor: ITimerService
    {

    }
    public class MediaPictureProcessor : SourceTimerService, IMediaPictureProcessor
    {
        private readonly IMediaLibrary _MediaLibrary;
        private readonly IApplicationSettings _ApplicationSettings;
        private readonly BaseClassifier[] _Classifier;

        public MediaPictureProcessor(
            IMediaLibrary mediaLibrary,
            IMediaClassifierSettings settings,
            IApplicationSettings applicationSettings,
            IProcessorCollection processorCollection,
            ILogger<MediaPictureProcessor> logger) 
            : base(nameof(SourceTimerService), processorCollection, logger)
        {
            _MediaLibrary = mediaLibrary;
            this._ApplicationSettings = applicationSettings;
            _Classifier = new BaseClassifier[] { new VideoClassifier(mediaLibrary, logger) };
            foreach (var classifier in _Classifier)
                classifier.SourceReaderRequest += (sender, e) => { e.Reader = CreateReader(e.MediaSource); };
            DueTime = settings.FirstCheck;
            Period = settings.CheckInterval;
        }
        protected override void ProcessNotification(NotificationEventArgs e)
        {
            base.ProcessNotification(e);
            switch (e.Name)
            {
                case "ClassificationCompleted":
                    ForceExecute();
                    break;
                case "ReloadPictures":
                    EnqueueForceScan(e.Data as BaseServiceModel);
                    ForceExecute();
                    break;
            }
        }
        private ConcurrentQueue<BaseServiceModel> _ForceReloadEntries = new ConcurrentQueue<BaseServiceModel>();
        private void EnqueueForceScan(BaseServiceModel entry)
        {
            if (!_ForceReloadEntries.Any(e => e.Id == entry.Id))
            {
                _MediaLibrary.Hold(entry);
                _ForceReloadEntries.Enqueue(entry);
            }
        }
        private bool CheckReloadNextForcedEntries()
        {
            CheckActive();
            if (!_ForceReloadEntries.TryDequeue(out BaseServiceModel entry))
                return false;
            try
            {
                ForceReload(entry as Actor);                
                return true;
            }
            finally
            {
                _MediaLibrary.Release(entry);
            }
        }
        private void ForceReload(Actor actor)
        {
            actor.NeedsPictureUpdate = true;
            UpdatePictures(actor);
        }

        protected override void ExecuteTimerSync()
        {
            if (_ApplicationSettings.ImageScrappingEnabled)
            {
                ClassifyNextItems();
                DeleteOrpahnedPictures();
            }
        }

        private TimeSpan OrphanedPictureInterval = TimeSpan.FromDays(1);
        private bool DeleteOrpahnedPicturesRunning = false;
        private async void DeleteOrpahnedPictures()
        {
            if (DeleteOrpahnedPicturesRunning) return;
            DeleteOrpahnedPicturesRunning = true;
            try
            {
                if (_ApplicationSettings.LastPictureOrphanagesCheck.Add(OrphanedPictureInterval) > DateTime.Now)
                    return;
                StartProcess($"Bereinige Grafikspeicher.");
                try
                {
                    foreach (var classifier in _Classifier)
                        await classifier.DeleteOrpahnedPictures();
                    foreach (var classifier in _Classifier)
                        await classifier.RecaptureInvalidPictures();
                }
                finally
                {
                    _ApplicationSettings.LastPictureOrphanagesCheck = DateTime.Now;
                    FinishProcess();
                }
            }
            finally
            {
                DeleteOrpahnedPicturesRunning = false;
            }
        }

        private void ClassifyNextItems()
        {
            var processing = false;
            try
            {
                while (CheckReloadNextForcedEntries());
                List<Exception> errors = new List<Exception>();
                var mediaItemsFound = true;
                while (mediaItemsFound)
                {
                    mediaItemsFound = false;
                    foreach (var entry in _MediaLibrary.GetMediaItemsThatNeedsPictureUpdate().ToList())
                        try
                        {
                            mediaItemsFound = true;
                            if (!processing)
                            {
                                StartProcess($"Bereite Grafiken auf.");
                                processing = true;
                            }
                            UpdatePictures(entry);
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                        finally
                        {
                            _MediaLibrary.Release(entry);
                            while (CheckReloadNextForcedEntries()) ;
                        }
                }

                var actorFound = true;
                while (actorFound)
                {
                    actorFound = false;
                    foreach (var actor in _MediaLibrary.GetActorsThatNeedsPictureUpdate().ToList())
                        try
                        {
                            actorFound = true;
                            if (!processing)
                            {
                                StartProcess($"Bereite Grafiken auf.");
                                processing = true;
                            }
                            UpdatePictures(actor);
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                        finally
                        {
                            _MediaLibrary.Release(actor);
                        }
                }

                if (errors.Any())
                    throw new AggregateException("Some picture items could not be updated.", errors);
            }
            finally
            {
                if (processing)
                    FinishProcess();
            }
        }

        private void UpdatePictures(Actor actor)
        {
            if (actor is not null)
                try
                {
                    NotifyStatus($"Bereite Grafiken auf für: {actor.Name}");
                    foreach (var classifier in _Classifier)
                        if (classifier.UpdatePictures(actor))
                            break;
                    actor.NeedsPictureUpdate = false;
                    _MediaLibrary.AddOrUpdateActor(actor);
                }
                catch(UnknownImageFormatException ex)
                {
                    actor.NeedsPictureUpdate = false;
                    _MediaLibrary.AddOrUpdateActor(actor);
                    NotifyError(ex);
                }
                catch (Exception ex)
                {
                    actor.LastPictureUpdateTry = DateTime.Now;
                    _MediaLibrary.AddOrUpdateActor(actor);
                    NotifyError(ex);
                }
        }

        private void UpdatePictures(MediaItem item)
        {
            if (item is not null)
                try
                {
                    NotifyStatus($"Bereite Grafiken auf für: {item.Name}");
                    foreach (var classifier in _Classifier)
                        if (classifier.UpdatePictures(item))
                            break;
                    item.NeedsPictureUpdate = false;
                    _MediaLibrary.AddOrUpdateMediaItem(item);
                }
                catch (Exception ex)
                {
                    item.LastPictureUpdateTry = DateTime.Now;
                    _MediaLibrary.AddOrUpdateMediaItem(item);
                    NotifyError(ex);
                }
        }

    }
}
