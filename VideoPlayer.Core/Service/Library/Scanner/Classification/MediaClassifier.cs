using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;

namespace VideoPlayer.Service.Library.Scanner.Classification
{

    public class MediaClassifier: SourceTimerService, 
        IMediaClassifier
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly IMediaClassifierSettings settings;
        private readonly IApplicationSettings applicationSettings;
        private readonly BaseClassifier[] _Classifier;

        public MediaClassifier(
            IMediaLibrary mediaLibrary, 
            IMediaClassifierSettings settings, 
            IApplicationSettings applicationSettings,
            IProcessorCollection processorCollection,
            ILogger<MediaClassifier> logger)
            : base(nameof(MediaClassifier), processorCollection, logger)
        {
            _MediaLibrary = mediaLibrary;
            this.settings = settings;
            this.applicationSettings = applicationSettings;
            _Classifier = new BaseClassifier[] { new VideoClassifier(mediaLibrary, logger) };
            foreach (var classifier in _Classifier)
                classifier.SourceReaderRequest += (sender, e) => { e.Reader = CreateReader(e.MediaSource); };
            DueTime = settings.FirstCheck;
            Period = settings.CheckInterval;
        }
        private void NotifyClassificationCompleted()
        {
            Notify(this, new NotificationEventArgs("ClassificationCompleted", null));
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
        protected override void ExecuteTimerSync()
        {
            if (applicationSettings.ClassificationEnabled)
                ClassifyNextItems();
        }

        private void ClassifyNextItems()
        {
            var processing = false;            
            try
            {
                var found = false;
                var lastNotification = DateTime.Now;
                var checkNotify = () => {
                    if (lastNotification.AddSeconds(10) < DateTime.Now)
                    {
                        NotifyClassificationCompleted();
                        lastNotification = DateTime.Now;
                        found = false;
                    }
                    else
                        found = true;
                };
                foreach (var mediaItem in _MediaLibrary
                    .GetUnclassifiedMediaItems()
                    .Where(mi => mi is not null))
                    try
                    {
                        if (!processing)
                        {
                            StartProcess($"Klassifiziere nächste Elemente");
                            processing = true;
                        }
                        Classify(mediaItem);
                        checkNotify();
                    }
                    finally
                    {
                        _MediaLibrary.Release(mediaItem);
                    }

                foreach (var collection in _MediaLibrary.GetUnclassifiedMediaCollections())
                    try
                    {
                        if (!processing)
                        {
                            StartProcess($"Klassifiziere nächste Elemente");
                            processing = true;
                        }                        
                        Classify(collection);
                        checkNotify();
                    }
                    finally
                    {
                        _MediaLibrary.Release(collection);
                    }
                if (found)
                    NotifyClassificationCompleted();
            }
            finally
            {
                if (processing)
                    FinishProcess();                
            }
        }

        private Task Classify(MediaCollection collection)
        {
            collection.Classified = true;
            _MediaLibrary.AddOrUpdateMediaCollection(collection);
            return Task.CompletedTask;
        }

        
        private void Classify(MediaItem mediaItem)
        {
            if (!applicationSettings.ClassificationEnabled) return;
            try
            {
                NotifyStatus($"Klassifiziere: {mediaItem.Path}");
                foreach (var classifier in _Classifier)
                    if (classifier.Classify(mediaItem))
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
