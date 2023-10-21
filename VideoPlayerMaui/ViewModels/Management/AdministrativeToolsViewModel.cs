using System;
using System.Linq;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Management
{
    public class AdministrativeToolsViewModel: BaseManagementContentViewModel
    {

        public AdministrativeToolsViewModel(IStatusPublisher statusPublisher)
            : base(statusPublisher)
        {
            Title = $"Administrative Aufgaben";
        }

    }
}
