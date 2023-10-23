using System;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.Global;
using VideoPlayer.ViewModels.Management.Sources;

namespace VideoPlayer.ViewModels.Management
{
    public class ManagementViewModel: BaseViewModel
    {

        public ManagementViewModel(
            SettingsViewModel settingsViewModel,
            AdministrativeToolsViewModel adminTasksViewModel,
            SourcesViewModel sourcesViewModel,
            GlobalStatusViewModel statusViewModel,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager)
            : base(statusPublisher, navigationManager)
        {
            Settings = settingsViewModel;
            Tools = adminTasksViewModel;
            Sources = sourcesViewModel;
            Title = "Verwaltung";
            StatusViewModel = statusViewModel;
        }

        public GlobalStatusViewModel StatusViewModel
        {
            get
            {
                return GetProperty<GlobalStatusViewModel>();
            }
            set
            {
                SetProperty<GlobalStatusViewModel>(value);
            }
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            CheckShowFirstTime();
        }

        private void CheckShowFirstTime()
        {
            bool hasVisibleViewModel = GetType()
                                       .GetProperties()
                                       .Where(p => p.PropertyType.IsAssignableTo(typeof(BaseManagementContentViewModel)))
                                       .Where(p => p.Name.EndsWith("Content"))
                                       .Any(p => p.GetValue(this) != null);
            if (!hasVisibleViewModel)
                ChangeView(Sources);
        }

        public BaseManagementContentViewModel CurrentContent
        {
            get
            {
                return GetProperty<BaseManagementContentViewModel>();
            }
            set
            {
                SetProperty<BaseManagementContentViewModel>(value);
            }
        }

        public SourcesViewModel Sources
        {
            get
            {
                return GetProperty<SourcesViewModel>();
            }
            set
            {
                SetProperty<SourcesViewModel>(value);
            }
        }

        public SourcesViewModel SourcesContent
        {
            get
            {
                return GetProperty<SourcesViewModel>();
            }
            set
            {
                SetProperty<SourcesViewModel>(value);
                SourcesVisible = value != null;
            }
        }

        public bool SourcesVisible
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
                {
                    var oldValue = prop.GetValue(this) as BaseManagementContentViewModel;
                    if (oldValue != null)
                        oldValue.OnDisappeared(false);
                    prop.SetValue(this, null);
                    if (oldValue != null)
                        oldValue.OnDisappeared(false);
                }
                else
                {
                    var sourceProp = thisType.GetProperty(prop.Name.Substring(0, prop.Name.Length - "Content".Length));
                    prop.SetValue(this, sourceProp.GetValue(this));
                    CurrentContent = viewModel;
                    viewModel.OnAppeared();
                }
            }
        }

    }
}
