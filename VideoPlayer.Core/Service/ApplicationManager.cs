
using Microsoft.Extensions.Logging;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Device;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Export;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Library.Scanner.Classification;
using VideoPlayer.Service.Library.Scanner.Picture;
using VideoPlayer.Service.Log;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Resources;
using VideoPlayer.Service.Status;

namespace VideoPlayer.Service
{
    public class ApplicationManager: BaseService, IApplicationManager
    {

        private static ApplicationManager _Current;
        private readonly IServiceProvider _ServiceProvider;
        private bool _Initializing = false;
        private readonly IEventController _EventController;
        private IProcessorCollection _ProcessorCollection;
        private IStatusManager _StatusManager;
        private IMediaLibrary _MediaLibrary;

        public event EventHandler InitializationCompleted;

        public ApplicationManager(
            IServiceProvider serviceProvider, IEventController eventController,
            ILogger<ApplicationManager> logger)
            :base(logger)
        {
            _EventController = eventController;
            _ServiceProvider = serviceProvider;
            _Current = this;
        }

        public static T GetService<T>()
        {
            return _Current.RegisterForEvents(_Current._ServiceProvider.GetService<T>());
        }

        public T ResolveService<T>()
        {
            return RegisterForEvents(_ServiceProvider.GetService<T>());
        }

        public void DisposeService(object service)
        {
            _EventController.Unregister(service);
        }

        private T RegisterForEvents<T>(T service)
        {
            _EventController.Register(service);
            return service;
        }

        public void Initialize()
        {
            if (Initialized)
                return;
            Thread Worker = new Thread(new ThreadStart(() => { RunInitialization(); }))
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            Worker.Start();
        }

        private void StartPostInitialization()
        {
            Thread Worker = new Thread(new ThreadStart(() => { RunPostInitialization(); }))
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            Worker.Start();
        }
        private void RunPostInitialization()
        {
            try
            {
                NotifyStatus($"Bereinige alte Daten.");
                _MediaLibrary.ClearLogs();
                (GetService<ILoggerProvider>() as DatabaseLoggerProvider).Init(_MediaLibrary, _ProcessorCollection);

                var downloadManager = GetService<IDownloadManager>();
                downloadManager.ClearTempFolder();

                NotifyStatus($"Starte Hintergrundaktivitäten.");
                GetService<ILibraryScanner>().Start();
                GetService<IMediaClassifier>().Start();
                GetService<IMediaPictureProcessor>().Start();
                downloadManager.Start();

                NotifyStatus($"Initialisierung erfolgt.");
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }
        }
        private void RunInitialization()
        {
            if (_Initializing)
                return;
            _Initializing = true;
            try
            {
                if (Initialized)
                    return;
                DateTime startTime = DateTime.Now;                
                _StatusManager = GetService<IStatusManager>();
                _ProcessorCollection = GetService<IProcessorCollection>();
                var database = GetService<IMediaLibraryDatabase>();
                _MediaLibrary = GetService<IMediaLibrary>();                
                NotifyStatus($"Initialisiere Monitormanagement.", true);
                var displayManager = GetService<IDeviceDisplayManager>();
                NotifyStatus($"Initialisiere Datenbank.", true);                
                database.UpdateSchema();
                if (database.IsEmpty())
                {
                    NotifyStatus($"Lade Demodaten.");
                    _MediaLibrary.CreateDemoData();
                }

                NotifyStatus($"Initialisiere Benutzeroberfläche.");
                var resourceManager = GetService<IResourceManager>();

                GetService<IPlaylistManager>().Init();
                InitializationCompleted?.Invoke(this, EventArgs.Empty);                

                StartPostInitialization();
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }
            finally
            {
                _Initializing = false;
                Initialized = true;
            }
        }

        public bool Initialized { get; private set; }

    }
}
