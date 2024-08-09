
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Scanner;

namespace VideoPlayer.Service
{
    public class ApplicationManager: BaseService, IApplicationManager
    {

        private static ApplicationManager _Current;
        private readonly IServiceProvider _ServiceProvider;
        private bool _Initializing = false;
        private readonly IEventController _EventController;

        public event EventHandler InitializationCompleted;

        public ApplicationManager(IServiceProvider serviceProvider, IEventController eventController)
        {
            _EventController = eventController;
            _ServiceProvider = serviceProvider;
            _Current = this;
        }

        public static T GetService<T>()
        {
            return _Current.RegisterForEvents(_Current._ServiceProvider.GetService<T>());
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

        private void RunInitialization()
        {
            if (_Initializing)
                return;
            _Initializing = true;
            try
            {
                if (Initialized)
                    return;
                NotifyStatus($"Initialisiere Datenbank.", true);
                var database = GetService<IMediaLibraryDatabase>();
                database.UpdateSchema();
                if (database.IsEmpty())
                {
                    NotifyStatus($"Lade Demodaten.");
                    var library = GetService<IMediaLibrary>();
                    library.CreateDemoData();
                }
                NotifyStatus($"Starte Hintergrundaktivitäten.");
                GetService<ILibraryScanner>().Start();
                InitializationCompleted?.Invoke(this, EventArgs.Empty);
                NotifyStatus($"Initialisierung erfolgt.");
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
