using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Library.SourceReader;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests
{
    public abstract class BaseTest: INotifyPropertyChanged
    {

        public BaseTest() { }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private ConcurrentDictionary<string, object> _Properties = new ConcurrentDictionary<string, object>();

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!_Properties.ContainsKey(name))
                return default(T);
            return (T)_Properties[name];
        }

        protected void SetProperty<T>(T value, [CallerMemberName] string name = "")
        {
            SetProperty((object)value, name);
        }

        protected void SetProperty(object value, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(nameof(name));
            _Properties.AddOrUpdate(name, value, (name, oldValue) => value);
            OnPropertyChanged(name);
        }
        #endregion

        protected MediaLibraryDatabase Database { get; private set; }

        protected MediaLibrary MediaLibrary { get; private set; }

        protected Dictionary<string, DummySourceReader> DummySources { get; private set; } = new Dictionary<string, DummySourceReader>();

        protected MediaClassifier MediaClassifier { get; private set; }

        private bool _MediaClassifierWorking = false;

        protected LibraryScanner Scanner { get; private set; }

        private bool _ScannerWorking = false;
        protected DownloadManager DownloadManager { get; private set; }

        protected virtual void Init(object loopArgument)
        {
            Clear();
        }

        protected IPlaylistManager PlaylistManager { get; set; }
        protected void InitializePlaylistManager()
        {
            IMediaCollectionSelector mediaCollectionSelector = new MediaCollectionSelector(MediaLibrary);
            PlaylistManager = new PlaylistManager(MediaLibrary, DownloadManager, mediaCollectionSelector, null);
        }

        protected void InitializeEmptyDatabase()
        {
            var settings = new DatabaseSettings();
            Database = new MediaLibraryDatabase(settings);
            Database.UpdateSchema();
            if (!Database.IsEmpty())
                throw new ApplicationException($"Database is not new.");
            MediaLibrary = new MediaLibrary(Database, null);
        }

        protected void InitializeDownloadManager()
        {
            DownloadManager = new DownloadManager(MediaLibrary, new ApplicationEnvironment(), null, null);
            DownloadManager.CreatingSourceReader += DownloadManager_CreatingSourceReader;
        }

        protected virtual void DownloadManager_CreatingSourceReader(object sender, SourceReaderEventArgs e)
        {
            if (DummySources is not null)
                if (DummySources.ContainsKey(e.Source?.Name))
                    e.Reader = DummySources[e.Source.Name];
        }

        protected void InitializeClassifier(bool autoStart)
        {
            MediaClassifierSettings settings = new MediaClassifierSettings()
            {
                FirstCheck = TimeSpan.FromSeconds(1),
                CheckInterval = TimeSpan.FromMinutes(30)
            };
            MediaClassifier = new MediaClassifier(MediaLibrary, settings, null);
            MediaClassifier.ExecutionStarted += MediaClassifier_ExecutionStarted;
            MediaClassifier.ExecutionFinished += MediaClassifier_ExecutionFinished;
            MediaClassifier.CreatingSourceReader += MediaClassifier_CreatingSourceReader;
            MediaClassifier.OnEvent += MediaClassifier_OnEvent;
            MediaClassifier.MediaItemClassified += MediaClassifier_MediaItemClassified;
            if (autoStart)
                MediaClassifier.Start();
        }

        protected virtual void MediaClassifier_MediaItemClassified(object sender, BaseServiceModelEventArgs e) { }

        protected virtual void MediaClassifier_OnEvent(object sender, NotificationEventArgs e) { }

        protected void InitializeScanner(bool autoStart)
        {
            LibraryScannerSettings settings = new LibraryScannerSettings()
            {
                SourceScanInterval = TimeSpan.FromDays(1),
                CheckInterval = TimeSpan.FromHours(1),
                FirstCheck = TimeSpan.FromSeconds(1),
            };
            Scanner = new LibraryScanner(MediaLibrary, settings, null);
            Scanner.CreatingSourceReader += MediaClassifier_CreatingSourceReader;
            Scanner.ExecutionStarted += Scanner_ExecutionStarted;
            Scanner.ExecutionFinished += Scanner_ExecutionFinished;

            if (autoStart)
                Scanner.Start();
        }

        private void Scanner_ExecutionFinished(object sender, EventArgs e)
        {
            _ScannerWorking = false;
        }

        private void Scanner_ExecutionStarted(object sender, EventArgs e)
        {
            _ScannerWorking = true;
        }

        protected async Task WaitForScannerStarted(TimeSpan timeout)
        {
            var endTime = DateTime.Now.Add(timeout);
            while (!_ScannerWorking && (DateTime.Now < endTime))
                await Task.Delay(200).ConfigureAwait(false);
            if (!_ScannerWorking)
                throw new ApplicationException($"Scanner not started in time.");
        }

        protected async Task WaitForScannerFinished(TimeSpan timeout)
        {
            var endTime = DateTime.Now.Add(timeout);
            while (_ScannerWorking && (DateTime.Now < endTime))
                await Task.Delay(200).ConfigureAwait(false);
            if (_ScannerWorking)
                throw new ApplicationException($"Scanner not finished in time.");
        }

        protected virtual void MediaClassifier_CreatingSourceReader(object sender, SourceReaderEventArgs e)
        {
            if (DummySources is not null)
                if (DummySources.ContainsKey(e.Source?.Name))
                    e.Reader = DummySources[e.Source.Name];
        }

        protected async Task WaitForClassificationStarted(TimeSpan timeout)
        {
            var endTime = DateTime.Now.Add(timeout);
            while (!_MediaClassifierWorking && (DateTime.Now < endTime))
                await Task.Delay(200).ConfigureAwait(false);
            if (!_MediaClassifierWorking)
                throw new ApplicationException($"Classification not started in time.");
        }

        protected async Task WaitForClassificationFinished(TimeSpan timeout)
        {
            var endTime = DateTime.Now.Add(timeout);
            while (_MediaClassifierWorking && (DateTime.Now < endTime))
                await Task.Delay(200).ConfigureAwait(false);
            if (_MediaClassifierWorking)
                throw new ApplicationException($"Classification not finished in time.");
        }

        protected virtual void MediaClassifier_ExecutionFinished(object sender, EventArgs e)
        {
            _MediaClassifierWorking = false;
        }

        protected virtual void MediaClassifier_ExecutionStarted(object sender, EventArgs e)
        {
            _MediaClassifierWorking = true;
        }

        protected MediaSource AddMediaSource(string name, bool createDummySource)
        {
            name = string.IsNullOrWhiteSpace(name) ? ($"Quelle {DateTime.Now}") : name;
            if (createDummySource)
                DummySources[name] = new DummySourceReader(name);
            return MediaLibrary.AddOrUpdateSource(new HttpMediaSource()
                {
                    Name = name,
                    Uri = $"http://127.0.0.1/{name}"
                });
        }

        private MediaCollection GetRootCollection(MediaSource source)
        {
            var collection = MediaLibrary.GetMediaCollectionByPath(source.Id, "/");
            if (collection is null)
                collection = MediaLibrary.AddOrUpdateMediaCollection(new MediaCollection()
                    {
                        Name = source.Name,
                        ParentId = 0,
                        Path = $"/",
                        SourceId = source.Id
                    });
            return collection;
        }

        protected async Task ExecuteScanAndClassification(Action scannerFinishedCallback)
        {
            Scanner.Start();
            await WaitForScannerStarted(TimeSpan.FromSeconds(20));
            await WaitForScannerFinished(TimeSpan.FromSeconds(10 * 60));
            Scanner.Stop();

            scannerFinishedCallback.Invoke();
            await ExecuteClassification();
        }

        protected async Task ExecuteClassification()
        {
            MediaClassifier.Start();
            await WaitForClassificationStarted(TimeSpan.FromSeconds(20));
            await WaitForClassificationFinished(TimeSpan.FromSeconds(10 * 60));
            MediaClassifier.Stop();
        }

        protected async Task ExecuteDownloads(params ClassifiedEntry[] entries)
        {
            DownloadManager.Start();
            foreach (var entry in entries)
                DownloadManager.Enqueue(entry, null);
            await WaitForDownloadsFinished();
            DownloadManager.Stop();
        }
        protected async Task WaitForDownloadsFinished()
        {
            while (DownloadManager.HasJobs)
                await Task.Delay(1000);
        }

        protected MediaCollection AddMediaCollection(
            MediaSource source,
            MediaCollection parentCollection,
            string name,
            bool direct = true)
        {
            var collection = new MediaCollection()
            {
                Name = name,
                ParentId = (parentCollection is null) ? 0 : parentCollection.Id,
                Path = $"{parentCollection?.Path}/{name}",
                SourceId = source.Id
            };
            if (direct)
            {
                if (parentCollection is null)
                    parentCollection = GetRootCollection(source);
                collection.ParentId = parentCollection.Id;
                return MediaLibrary.AddOrUpdateMediaCollection(collection);
            }
            else
            {
                DummySources[source.Name].AddFolder($"{parentCollection?.Path}/{name}");
                return collection;
            }
        }

        protected MediaItem AddMediaItem(
            MediaSource source,
            MediaCollection collection,
            string fileName,
            bool direct = true)
        {
            if (direct)
            {
                if (collection is null)
                    collection = GetRootCollection(source);
                return MediaLibrary.AddOrUpdateMediaItem(new MediaItem()
                    {
                        Name = fileName,
                        ParentCollectionId = collection.Id,
                        Path = $"{collection.Path}/{fileName}"
                    });
            }
            else
            {
                DummySources[source.Name].AddFile($"{collection.Path}/{fileName}");
                return null;
            }
        }
        protected void AddSingleMovie()
        {
            var source = AddMediaSource("Filme", true);
            var collection = AddMediaCollection(source, null, "(500) Days of Summer (2009)", false);
            var mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).mp4", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).mp4.hash", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).nfo", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).nfo.hash", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).tbn", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009)-fanart.jpg", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009)-fanart.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009)-poster.jpg", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009)-poster.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009)-trailer.mp4", false);
            mediaItem = AddMediaItem(source, collection, ".tbn", false);
        }
        protected void AddMultiMovie()
        {
            var source = AddMediaSource("MultiMovie", true);
            var collection = AddMediaCollection(source, null, "Bad Boys", false);
            var mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs.mp4", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs.nfo", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs.nfo.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs.tbn", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs-fanart.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs-fanart.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs-landscape.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs-poster.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs-poster.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys - Harte Jungs-trailer.mp4", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2.mp4", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2.nfo", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2.nfo.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2.tbn", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2-fanart.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2-fanart.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2-poster.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2-poster.jpg.hash", false);
            mediaItem = AddMediaItem(source, collection, "Bad Boys 2-trailer.mp4", false);
            mediaItem = AddMediaItem(source, collection, "folder.jpg", false);
            mediaItem = AddMediaItem(source, collection, "folder.jpg.hash", false);

            collection = AddMediaCollection(source, collection, ".actors", false);
            mediaItem = AddMediaItem(source, collection, "Anna_Thomson.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Buddy_Bolton.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Ed_Amatrudo.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Emmanuel_Xuereb.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Fawn_Reed.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Frank_John_Hughes.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Joe_Pantoliano.jpg", false);
            mediaItem = AddMediaItem(source, collection, "John_Salley.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Juan_F._Cejas.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Julio_Oscar_Mechoso.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Kevin_Corrigan.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Kim_Coates.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Lisa_Boyle.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Manny_Perry.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Marc_Macaulay.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Marg_Helgenberger.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Mario_Ernesto_Sánchez.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Martin_Lawrence.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Michael_Imperioli.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Michael_Taliferro.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Nestor_Serrano.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Sam_Ayers.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Saverio_Guerra.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Scott_Cumberbatch.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Shaun_Toub.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Tchéky_Karyo.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Téa_Leoni.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Theresa_Randle.jpg", false);
            mediaItem = AddMediaItem(source, collection, "Will_Smith.jpg", false);

            collection = AddMediaCollection(source, collection, "Bad Boys - Harte Jungs", false);
            mediaItem = AddMediaItem(source, collection, ".tbn", false);

            collection = AddMediaCollection(source, collection, "Bad Boys 2", false);
            mediaItem = AddMediaItem(source, collection, ".tbn", false);
        }
        protected void AddTVShow()
        {
            var seasonCount = 3;
            var source = AddMediaSource("Serien", true);
            var collection = AddMediaCollection(source, null, "How I met your mother", false);
            var mediaItem = AddMediaItem(source, collection, "banner.jpg", false);
            mediaItem = AddMediaItem(source, collection, "fanart.jpg", false);
            mediaItem = AddMediaItem(source, collection, "folder.jpg", false);
            mediaItem = AddMediaItem(source, collection, "poster.jpg", false);
            for (int idx = 1; idx <= seasonCount; idx++)
            {
                mediaItem = AddMediaItem(source, collection, $"season0{idx}-banner.jpg", false);
                mediaItem = AddMediaItem(source, collection, $"season0{idx}-fanart.jpg", false);
                mediaItem = AddMediaItem(source, collection, $"season0{idx}-poster.jpg", false);
            }
            mediaItem = AddMediaItem(source, collection, "tvshow.nfo", false);
            var subCollection = AddMediaCollection(source, collection, $".actors", false);
            mediaItem = AddMediaItem(source, subCollection, "Alyson_Hannigan.jpg", false);
            mediaItem = AddMediaItem(source, subCollection, "Cobie_Smulders.jpg", false);
            mediaItem = AddMediaItem(source, subCollection, "Cristin_Milioti.jpg", false);
            mediaItem = AddMediaItem(source, subCollection, "Jason_Segel.jpg", false);
            mediaItem = AddMediaItem(source, subCollection, "Josh_Radnor.jpg", false);
            mediaItem = AddMediaItem(source, subCollection, "Neil_Patrick_Harris.jpg", false);
            subCollection = AddMediaCollection(source, collection, $"extrafanart", false);
            mediaItem = AddMediaItem(source, subCollection, "1LHtWeFVOHcIHOvxdYaQQfCDY0B.jpg", false);
            for (int idx = 1; idx <= seasonCount; idx++)
            {
                var seasonCollection = AddMediaCollection(source, collection, $"Staffel 0{idx}", false);
                foreach (var name in new string[][] {
                    new string[] {"S01E01 Verliebt, Verlobt, Versagt",
                    "S01E02 Die Lila Giraffe",
                    "S01E03 Frauen, Flieger, Freiheit",
                    "S01E04 Gutes Altes Hemd" },
                    new string[] {"S02E01 Das Grosse Baby",
                    "S02E02 Neues Leben, Alte Fehler",
                    "S02E03 Brunch",
                    "S02E04 Ted Mosby, Architekt" },
                    new string[] {"S03E01 Der Adonis",
                    "S03E02 Wir Sind Nicht Von Hier",
                    "S03E03 Angst Vorm Dreirad" }
                }[idx - 1])
                {

                    mediaItem = AddMediaItem(source, seasonCollection, $"How I Met Your Mother {name}.mp4", false);
                    mediaItem = AddMediaItem(source, seasonCollection, $"How I Met Your Mother {name}.nfo", false);
                    mediaItem = AddMediaItem(source, seasonCollection, $"How I Met Your Mother {name}.nfo.hash", false);
                    mediaItem = AddMediaItem(source, seasonCollection, $"How I Met Your Mother {name}-thumb.jpg", false);
                    mediaItem = AddMediaItem(source, seasonCollection, $"How I Met Your Mother {name}-thumb.jpg.hash", false);
                }

                seasonCollection = AddMediaCollection(source, seasonCollection, $".actors", false);

                mediaItem = AddMediaItem(source, seasonCollection, "Alyson_Hannigan.jpg", false);
                mediaItem = AddMediaItem(source, seasonCollection, "Cobie_Smulders.jpg", false);
                mediaItem = AddMediaItem(source, seasonCollection, "Cristin_Milioti.jpg", false);
                mediaItem = AddMediaItem(source, seasonCollection, "Jason_Segel.jpg", false);
                mediaItem = AddMediaItem(source, seasonCollection, "Josh_Radnor.jpg", false);
                mediaItem = AddMediaItem(source, seasonCollection, "Neil_Patrick_Harris.jpg", false);
            }
        }

        protected virtual void Clear()
        {
            ClearScanner();
            ClearClassifier();
            ClearDummySource();
            DeleteDatabase();
        }

        private void DeleteDatabase()
        {
            try
            {
                if (MediaLibrary is not null)
                    MediaLibrary = null;
                if (Database is not null)
                    Database = null;
                GC.Collect();

                var settings = new DatabaseSettings();
                if (File.Exists(settings.DatabasePath))
                    File.Delete(settings.DatabasePath);
            }
            catch { }
        }

        private void ClearDummySource()
        {
            foreach (var source in DummySources)
                source.Value.Clear();
            DummySources.Clear();
        }

        private void ClearClassifier()
        {
            if (MediaClassifier is not null)
            {
                MediaClassifier.Stop();
                MediaClassifier = null;
            }
        }

        private void ClearScanner()
        {
            if (Scanner is not null)
            {
                Scanner.Stop();
                Scanner = null;
            }
        }
        public DateTime LastExecutionBegin {  get; set; }
        protected virtual object[] LoopArguments { get; } = new object[1] { null };
        public async Task Run()
        {            
            try
            {
                LastExecutionBegin = DateTime.Now;
                Message = string.Empty;
                Status = TestStatus.Running;
                foreach (var loopArgument in LoopArguments)
                    try
                    {
                        Clear();
                        Init(loopArgument);
                        await ExecuteAsync(loopArgument);
                    }
                    catch (Exception ex)
                    {
                        if (LoopArguments.Length == 1)
                            throw;
                        throw new ApplicationException($"{ex.Message} with argument = {loopArgument}", ex);
                    }
                Status = TestStatus.Success;
            }
            catch (Exception ex)
            {
                Status = TestStatus.Failed;
                Message = $"{ex.Message}";
            }
            finally
            {
                try
                {
                    Clear();
                }
                catch { }
            }
        }

        public TestStatus Status
        {
            get
            {
                return GetProperty<TestStatus>();
            }
            set
            {
                SetProperty<TestStatus>(value);
            }
        }

        public string Message
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        protected abstract Task ExecuteAsync(object loopArgument);

        protected MediaItem GetPlayableMediaItem(ClassifiedEntry entry)
        {
            IMediaItemCollectionEntry collectionEntry = entry as IMediaItemCollectionEntry;
            if (collectionEntry is null)
                return null;
            return collectionEntry.MediaItemIds.Select(id => MediaLibrary.GetMediaItem(id))
                .Where(mi => mi.CopyType != MediaItemCopyType.Trailer)
                .OrderByDescending(mi => mi.CopyType)
                .FirstOrDefault();
        }

        protected void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new ApplicationException(message);
        }
        protected void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new ApplicationException(message);
        }

        protected void AssertRecordCount(IEnumerable<object> recordSet, int expectedRecordCount)
        {
            AssertTrue(recordSet.Count() == expectedRecordCount, $"{expectedRecordCount} records were expected. {recordSet.Count()} where found.");
        }
        protected void AssertRecordsEqual(IEnumerable<object> actual, IEnumerable<object> expected)
        {
            var recordArray = actual.ToArray();
            var compareToArray = expected.ToArray();
            AssertRecordCount(actual, expected.Count());
            for (int idx = recordArray.GetLowerBound(0); idx <= recordArray.GetUpperBound(0); idx++)
                try
                {
                    var record = recordArray[idx];
                    var compareTo = compareToArray[idx];
                    AssertObjectsEqual(record, compareTo);
                }
                catch (Exception ex)
                {
                  throw new ApplicationException($"Record not equal at offset {idx}. {ex.Message}");
                }
        }
        protected void AssertObjectsEqual(object actual, object expected)
        {
            if (actual is null && expected is null)
                return;
            var sourceProps = actual
                .GetType()
                .GetProperties()
                .Where(p => p.CanRead);
            foreach (var sourceProp in sourceProps)
            {
                var destProp = expected.GetType().GetProperty(sourceProp.Name);
                var sourceValue = sourceProp.GetValue(actual, null);
                var destValue = destProp.GetValue(expected, null);
                if (sourceValue is null && destValue is null)
                    continue;
                if (sourceProp.PropertyType.IsArray)
                    AssertArraysEqual((Array)sourceValue, (Array)destValue);
                else if (sourceProp.PropertyType.IsAssignableTo(typeof(IList)))
                    AssertListsEqual((IList)sourceValue, (IList)destValue);
                else if (sourceProp.PropertyType.IsAssignableTo(typeof(BaseServiceModel)))
                    AssertObjectsEqual(sourceValue, destValue);
                else
                    AssertTrue(sourceValue.Equals(destValue), $"Objects are not equal. {sourceProp.Name}: {sourceValue} <> {destValue}");
            }
        }
        protected void AssertListsEqual(IList actual, IList expected)
        {
            AssertRecordsEqual(actual.Cast<object>(), expected.Cast<object>());
        }
        private void AssertArraysEqual(Array sourceValue, Array destValue)
        {
            AssertTrue(sourceValue.Length == destValue.Length, $"Array lengths differ: {sourceValue.Length} <> {destValue.Length}");
            for (int idx = sourceValue.GetLowerBound(0); idx <= destValue.GetUpperBound(0); idx++)
            {
                var sourceElem = sourceValue.GetValue(idx);
                var destElem = destValue.GetValue(idx);
                AssertTrue(sourceElem.Equals(destElem), $"Objects in array not equal. {idx}: {sourceElem} <> {destElem}");
            }
        }
    }
}
