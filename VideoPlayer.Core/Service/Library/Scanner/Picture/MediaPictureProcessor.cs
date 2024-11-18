using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Scanner.Classification;

namespace VideoPlayer.Service.Library.Scanner.Picture
{
    public interface IMediaPictureProcessor: ITimerService
    {

    }
    public class MediaPictureProcessor : SourceTimerService, IMediaPictureProcessor
    {
        private readonly IMediaLibrary _MediaLibrary;
        private readonly BaseClassifier[] _Classifier;

        public MediaPictureProcessor(
            IMediaLibrary mediaLibrary,
            IMediaClassifierSettings settings,
            ILogger<MediaPictureProcessor> logger) 
            : base(logger)
        {
            _MediaLibrary = mediaLibrary;
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
            }
        }
        protected override async Task ExecuteTimerAsync()
        {
            await ClassifyNextItems();
        }

        private async Task ClassifyNextItems()
        {
            var processing = false;
            try
            {
                List<Exception> errors = new List<Exception>();
                foreach (var entry in _MediaLibrary.GetMediaItemsThatNeedsPictureUpdate())
                    try
                    {
                        if (!processing)
                        {
                            StartProcess($"Bereite Grafiken auf.");
                            processing = true;
                        }
                        await UpdatePictures(entry);
                    }
                    catch(Exception ex)
                    {
                        errors.Add(ex);
                    }
                    finally
                    {
                        _MediaLibrary.Release(entry);
                    }

                foreach (var actor in _MediaLibrary.GetActorsThatNeedsPictureUpdate())
                    try
                    {
                        if (!processing)
                        {
                            StartProcess($"Bereite Grafiken auf.");
                            processing = true;
                        }
                        await UpdatePictures(actor);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                    finally
                    {
                        _MediaLibrary.Release(actor);
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

        private async Task UpdatePictures(Actor actor)
        {
            if (actor is not null)
                try
                {
                    NotifyStatus($"Bereite Grafiken auf für: {actor.Name}");
                    foreach (var classifier in _Classifier)
                        if (await classifier.UpdatePictures(actor))
                            break;
                    actor.NeedsPictureUpdate = false;
                    _MediaLibrary.AddOrUpdateActor(actor);
                }
                catch (Exception ex)
                {
                    actor.LastPictureUpdateTry = DateTime.Now;
                    _MediaLibrary.AddOrUpdateActor(actor);
                    NotifyError(ex);
                }
        }

        private async Task UpdatePictures(MediaItem item)
        {
            if (item is not null)
                try
                {
                    NotifyStatus($"Bereite Grafiken auf für: {item.Name}");
                    foreach (var classifier in _Classifier)
                        if (await classifier.UpdatePictures(item))
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
