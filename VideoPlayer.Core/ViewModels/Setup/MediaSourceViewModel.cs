using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Extensions;
using VideoPlayer.Service.Library.Models.Sources;

namespace VideoPlayer.ViewModels.Setup
{
    public class ElementProperty: INotifyPropertyChanged
    {
        public ElementProperty(string name, string value) {
            Name = name;
            Value = value;
            IsFreeText = true;
        }
        public string Name { get; private set; }
        public string Value 
        { 
            get => GetProperty<string>();
            set
            {
                if (PossibleValues is not null)
                    if (!PossibleValues.Contains(value))
                        return;
                SetProperty(value);
            }
        }
        public bool IsFreeText { get => GetProperty<bool>(); private set => SetProperty(value); }
        public bool IsSelectionValue { get => GetProperty<bool>(); 
            private set
            {
                SetProperty(value);
                if (value)
                    IsFreeText = false;
            }
        }
        public string[] PossibleValues { get => GetProperty<string[]>(); 
            set 
            { 
                SetProperty(value);
                IsFreeText = value is null || value.Length == 0;
                IsSelectionValue = value != null && value.Length > 0; 
            } 
        }

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
            Source = source;
            Title = Source?.Name ?? "Neue Quelle";
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
                    case "scan":
                        Scan();
                        break;
                }
            }
            catch
            {

            }
        }

        private void Scan()
        {
            if (Source.Id != 0)
                ScanRequest.Invoke(this, EventArgs.Empty);
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
            if (Source.Id == 0)
            {
                Source.LastScan = DateTime.Now;
            }
            if (HttpSource is not null)
            {
                HttpSource.Uri = Properties.First(p => p.Name == "Adresse").Value;
            }
            else if (SmbSource is not null)
            {
                SmbSource.Servername = Properties.First(p => p.Name == "IP-Adresse").Value;
                SmbSource.Username = Properties.First(p => p.Name == "Benutzername").Value;
                SmbSource.Password = Properties.First(p => p.Name == "Passwort").Value;
                SmbSource.ShareName = Properties.First(p => p.Name == "Freigabename").Value;
                SmbSource.RootPath = Properties.First(p => p.Name == "Relativer Pfad").Value;
            }
            else if (SFTPSource is not null)
            {
                SFTPSource.Servername = Properties.First(p => p.Name == "IP-Adresse").Value;
                SFTPSource.Username = Properties.First(p => p.Name == "Benutzername").Value;
                SFTPSource.Password = Properties.First(p => p.Name == "Passwort").Value;
                SFTPSource.Port = Properties.First(p => p.Name == "Port").Value.ToInt16();
                SFTPSource.RootPath = Properties.First(p => p.Name == "Relativer Pfad").Value;
            }
            SaveRequest.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler SaveRequest;
        public event EventHandler RemoveRequest;
        public event EventHandler ScanRequest;

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
            AddProperty(new ElementProperty("Name", Source?.Name ?? "Neue Quelle"));
            if (Source is null || Source.Id == 0)
                AddProperty(new ElementProperty("Art", Source?.GetType()?.Name?.Replace("MediaSource", "") ?? string.Empty)
                {
                    PossibleValues = new string[] { "Http", "Smb", "SFTP" }
                });
            if (HttpSource is not null)
            {
                AddProperty(new ElementProperty("Adresse", HttpSource.Uri));
            }
            else if (SmbSource is not null)
            {
                AddProperty(new ElementProperty("IP-Adresse", SmbSource.Servername));
                AddProperty(new ElementProperty("Benutzername", SmbSource.Username));
                AddProperty(new ElementProperty("Passwort", SmbSource.Password));
                AddProperty(new ElementProperty("Freigabename", SmbSource.ShareName));
                AddProperty(new ElementProperty("Relativer Pfad", SmbSource.RootPath));                
            }
            else if (SFTPSource  is not null)
            {
                AddProperty(new ElementProperty("IP-Adresse", SFTPSource.Servername));
                AddProperty(new ElementProperty("Port", SFTPSource.Port.ToString()));
                AddProperty(new ElementProperty("Benutzername", SFTPSource.Username));
                AddProperty(new ElementProperty("Passwort", SFTPSource.Password));                
                AddProperty(new ElementProperty("Relativer Pfad", SFTPSource.RootPath));
            }
            HasChanged = Source is null || Source.Id == 0;
            IsStored = Source is not null && Source.Id != 0;
        }

        private void AddProperty(ElementProperty elementProperty)
        {            
            Properties.Add(elementProperty);
            OriginalProperties.Add(new ElementProperty(elementProperty.Name, elementProperty.Value));
            elementProperty.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            HasChanged = Source is null || Source.Id == 0 || OriginalProperties.Any(oP => oP.Value != Properties.First(p => p.Name == oP.Name).Value);
            ElementProperty prop = sender as ElementProperty;
            if (prop.Name == "Art")
            {
                switch (prop.Value)
                {
                    case "Http":
                        Source = new HttpMediaSource()
                        {

                        };
                        LoadProperties();
                        break;
                    case "Smb":
                        Source = new SmbMediaSource()
                        {

                        };
                        LoadProperties();
                        break;
                    case "SFTP":
                        Source = new SFTPMediaSource();
                        LoadProperties();
                        break;
                }                
            }            
        }

        public bool IsStored { get => GetProperty<bool>(); set => SetProperty(value); }
        public bool HasChanged { get => GetProperty<bool>(); set => SetProperty(value); }
        protected  HttpMediaSource HttpSource { get => Source as HttpMediaSource; }
        protected SmbMediaSource SmbSource { get => Source as SmbMediaSource; }
        protected SFTPMediaSource SFTPSource { get => Source as SFTPMediaSource; }
        public MediaSource Source { get; private set; }
        public ObservableCollection<ElementProperty> Properties { get; } = new ObservableCollection<ElementProperty>();
        public ObservableCollection<ElementProperty> OriginalProperties { get; } = new ObservableCollection<ElementProperty>();
        public Command Action { get; }
    }
}
