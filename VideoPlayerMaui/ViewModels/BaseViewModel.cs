using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels
{

    public class BaseViewModel: INotifyPropertyChanged, IViewModelAppearance
    {

        public BaseViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settings)
        {
            Settings = settings;
            StatusPublisher = statusPublisher;
            NavigationManager = navigationManager;
        }

        #region Status
        protected IStatusPublisher StatusPublisher { get; }

        protected virtual void AddStatusMessage(string message, bool direct = true)
        {
            StatusPublisher?.AddStatus(message, direct);
        }
        #endregion

        #region INotifyPropertyChanged
        private Dictionary<string, object> properties = new Dictionary<string, object>();
        private PropertyChangedEventHandler _PropertyChanged;

        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                _PropertyChanged += value;
            }
            remove
            {
                _PropertyChanged -= value;
            }
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            _PropertyChanged?.Invoke(this, e);
        }

        protected T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!properties.ContainsKey(name))
                return default(T);
            return (T)properties[name];
        }

        protected void SetProperty<T>(object value, [CallerMemberName] string name = "")
        {
            var oldValue = GetProperty<T>(name);
            if (!properties.ContainsKey(name))
                properties.Add(name, value);
            else
                properties[name] = value;
            OnPropertyChanged(new PropertyChangedEventArgs(name));
        }
        #endregion

        #region IViewModelAppearance
        /// <summary>
        /// Wird aufgerufen, wenn die View mit diesem Viewmodel angezeigt wird.
        /// </summary>
        public virtual void OnAppeared() { }

        /// <summary>
        /// Wird aufgerufen, wenn die View mit dem Viewmodel ausgeblendet wird.
        /// </summary>
        /// <param name="closing">
        /// Gibt an, ob die Ansicht geschlossen wird (true), oder nur in den Hintergrund gelegt wird, weil eine neue
        /// Ansicht geöffnet wird. (false)
        /// </param>
        public virtual void OnDisappeared(bool closing) { }
        #endregion

        #region Navigation
        protected INavigationManager NavigationManager { get; }
        #endregion 

        public string Title
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

        public ISettingsService Settings { get; }

    }
}
