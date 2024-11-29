using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.ViewModels.Setup
{
    public class ElementProperty: INotifyPropertyChanged
    {
        public ElementProperty(string name, string value) {
            Name = name;
            Value = value;
        }
        public string Name { get; private set; }
        public string Value { get => GetProperty<string>(); set => SetProperty(value); }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private ConcurrentDictionary<string, object> _Properties = new ConcurrentDictionary<string, object>();

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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    public class MediaSourceViewModel: BaseViewModel
    {
        public MediaSourceViewModel(MediaSource source)
        {
            Source = source ?? new HttpMediaSource()
            {
                Name = "Neue Quelle",
            };
            Title = Source?.Name;
            LoadProperties();
            Action = new Command((arg) => { ExecuteAction((string)arg); });
        }

        private void ExecuteAction(string arg)
        {
            try
            {
                switch(arg)
                {
                    case "Save":
                        Save();
                        break;
                    case "Cancel":
                        Cancel();
                        break;
                }
            }
            catch
            {

            }
        }

        private void Cancel()
        {
            if (Source.Id != 0)
                LoadProperties();
            else
                RemoveRequest.Invoke(this, EventArgs.Empty);
        }

        private void Save()
        {
            Check();
            AssignChanges();
        }

        private void AssignChanges()
        {
            Source.Name = Properties.First(p => p.Name == "Name").Value;
            if (HttpSource is not null)
            {
                HttpSource.Uri = Properties.First(p => p.Name == "Adresse").Value;
            }
            SaveRequest.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler SaveRequest;
        public event EventHandler RemoveRequest;

        private void Check()
        {
            foreach (var prop in Properties)
            {
                if (string.IsNullOrWhiteSpace(prop.Value))
                    throw new ArgumentException($"Bitte gib {prop.Name} an.");
            }
        }

        private void LoadProperties()
        {
            Properties.Clear();
            OriginalProperties.Clear();
            AddProperty(new ElementProperty("Name", Source.Name));
            if (HttpSource is not null)
            {
                AddProperty(new ElementProperty("Adresse", HttpSource.Uri));
            }
            HasChanged = Source.Id == 0;
        }

        private void AddProperty(ElementProperty elementProperty)
        {            
            Properties.Add(elementProperty);
            OriginalProperties.Add(new ElementProperty(elementProperty.Name, elementProperty.Value));
            elementProperty.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HasChanged = Source.Id == 0 || OriginalProperties.Any(oP => oP.Value != Properties.First(p => p.Name == oP.Name).Value);
        }

        public bool HasChanged { get => GetProperty<bool>(); set => SetProperty(value); }
        protected  HttpMediaSource HttpSource { get => Source as HttpMediaSource; }
        public MediaSource Source { get; }
        public ObservableCollection<ElementProperty> Properties { get; } = new ObservableCollection<ElementProperty>();
        public ObservableCollection<ElementProperty> OriginalProperties { get; } = new ObservableCollection<ElementProperty>();
        public Command Action { get; }
    }
}
