using System;
using System.ComponentModel;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Services.Settings
{

    public class SettingsService: ISettingsService
    {

        private readonly ISettingsDataSource _SettingsDataStore;

        public SettingsService(ISettingsDataSource settingsDataStore)
        {
            _SettingsDataStore = settingsDataStore;
        }

        public async Task InitializeAsync()
        {
            _Current = BaseModel.FromDataModel(await _SettingsDataStore.GetSettingsAsync()) as Models.Settings;
            _Current.PropertyChanged += _Current_PropertyChanged;
        }

        private Models.Settings _Current = null;

        public Models.Settings Current
        {
            get
            {
                return _Current;
            }
        }

        private async void _Current_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var model = Current.ToDataModelAsync() as Database.Models.Settings;
            await _SettingsDataStore.UpdateSettingsAsync(model);
        }

    }
}
