using System;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.Service.Library.Scanner.Classification
{

    public class MediaClassifier: SourceTimerService, 
        IMediaClassifier
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly BaseClassifier[] _Classifier;

        public MediaClassifier(IMediaLibrary mediaLibrary, IMediaClassifierSettings settings)
            : base()
        {
            _MediaLibrary = mediaLibrary;
            _Classifier = new BaseClassifier[] { new VideoClassifier(mediaLibrary) };
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
                case "ScanCompleted":
                    ForceExecute();
                    break;
            }
        }
        public event EventHandler<BaseServiceModelEventArgs> MediaItemClassified;
        public override IEnumerable<IEventSubscriber> GetSubscribers()
        {
            return base.GetSubscribers()
                .Concat(_Classifier.OfType<IEventSubscriber>());
        }
        public override IEnumerable<IEventPublisher> GetPublishers()
        {
            return base.GetPublishers()
                .Concat(_Classifier.OfType<IEventPublisher>());
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
                foreach (var mediaItem in _MediaLibrary
                    .GetUnclassifiedMediaItems()
                    .Where(mi => mi is not null))
                {
                    if (!processing)
                    {
                        StartProcess($"Klassifiziere nächste Elemente");
                        processing = true;
                    }
                    await Classify(mediaItem);
                }

                foreach (var entry in _MediaLibrary.GetMediaItemsThatNeedsPictureUpdate())
                {
                    if (!processing)
                    {
                        StartProcess($"Klassifiziere nächste Elemente");
                        processing = true;
                    }
                    await UpdatePictures(entry);
                }
            }
            finally
            {
                if (processing)
                    FinishProcess();
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

        private async Task Classify(MediaItem mediaItem)
        {
            try
            {
                NotifyStatus($"Klassifiziere: {mediaItem.Path}");
                foreach (var classifier in _Classifier)
                    if (await classifier.Classify(mediaItem))
                        break;
                mediaItem.Classified = true;
                _MediaLibrary.AddOrUpdateMediaItem(mediaItem);
                MediaItemClassified?.Invoke(this, new BaseServiceModelEventArgs(mediaItem));
            }
            catch(Exception ex)
            {
                mediaItem.LastClassificationTry = DateTime.Now;
                _MediaLibrary.AddOrUpdateMediaItem(mediaItem);
                NotifyError(ex);
            }
        }

    }

}
