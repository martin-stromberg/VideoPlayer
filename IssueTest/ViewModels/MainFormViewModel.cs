using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using IssueTest.Services;

namespace IssueTest.ViewModels
{
    internal class MainFormViewModel : INotifyPropertyChanged
    {
        private MainFormService service;

        public MainFormViewModel()
        {
            service = new MainFormService();
        }

        public string Title
        {
            get { return GetProperty<string>(); }
            set { SetProperty(value); }
        }
        public ObservableCollection<MainFormListItemViewModel> Items { get; } = new ObservableCollection<MainFormListItemViewModel>();

        #region INotifyPropertyChanged
        private Dictionary<string, object> properties = new Dictionary<string, object>();
        private T GetProperty<T>([CallerMemberName] string name = "")
        {
            if (!properties.ContainsKey(name))
                return default;
            return (T)properties[name];
        }
        private void SetProperty(string value, [CallerMemberName] string name = "")
        {
            if (properties.ContainsKey(name))
                properties[name] = value;
            else
                properties.Add(name, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        internal void ProcessClick()
        {
            Title = $"Clicked at {DateTime.Now.ToShortTimeString()}";
        }

        internal async void StartAsync()
        {
            try
            {
                if (await service.IsEmptyAsync())
                    await service.InitAsync();
                var items = await service.GetItems();
                foreach (var item in items)
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        Items.Add(new MainFormListItemViewModel()
                        {
                            Name = item.Name
                        });
                    });                    
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }
}