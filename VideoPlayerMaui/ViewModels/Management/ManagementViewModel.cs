using System;
using System.Linq;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class ManagementViewModel: BaseViewModel
    {

        public ManagementViewModel(IServiceProvider serviceProvider, IStatusPublisher statusPublisher)
            : base(statusPublisher)
        {
            Settings = serviceProvider.GetService<SettingsViewModel>();
            Tools = serviceProvider.GetService<AdministrativeToolsViewModel>();
        }

        public SettingsViewModel Settings
        {
            get
            {
                return GetProperty<SettingsViewModel>();
            }
            set
            {
                SetProperty<SettingsViewModel>(value);
            }
        }

        public SettingsViewModel SettingsContent
        {
            get
            {
                return GetProperty<SettingsViewModel>();
            }
            set
            {
                SetProperty<SettingsViewModel>(value);
                SettingsVisible = value != null;
            }
        }

        public bool SettingsVisible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public AdministrativeToolsViewModel Tools
        {
            get
            {
                return GetProperty<AdministrativeToolsViewModel>();
            }
            set
            {
                SetProperty<AdministrativeToolsViewModel>(value);
            }
        }

        public AdministrativeToolsViewModel ToolsContent
        {
            get
            {
                return GetProperty<AdministrativeToolsViewModel>();
            }
            set
            {
                SetProperty<AdministrativeToolsViewModel>(value);
                ToolsVisible = value != null;
            }
        }

        public bool ToolsVisible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public void ChangeView(BaseManagementContentViewModel viewModel)
        {
            var thisType = GetType();
            foreach (var prop in thisType
                                 .GetProperties()
                                 .Where(p => p.PropertyType.IsAssignableTo(typeof(BaseManagementContentViewModel)))
                                 .Where(p => p.Name.EndsWith("Content")))
            {
                bool isVisible = prop.PropertyType.Name.Equals(viewModel.GetType().Name);
                if (!isVisible)
                    prop.SetValue(this, null);
                else
                {
                    var sourceProp = thisType.GetProperty(prop.Name.Substring(0, prop.Name.Length - "Content".Length));
                    prop.SetValue(this, sourceProp.GetValue(this));
                }
            }
        }

    }
}
