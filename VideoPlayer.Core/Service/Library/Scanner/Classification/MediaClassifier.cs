using System;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.Scanner.Classification
{

    public class MediaClassifier: SourceTimerService, IMediaClassifier
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

        public event EventHandler<BaseServiceModelEventArgs> MediaItemClassified;

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
            }
            finally
            {
                if (processing)
                    FinishProcess();
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
