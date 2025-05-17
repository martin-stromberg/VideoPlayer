using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;

namespace VideoPlayer.Service.Library.Tenants
{
    public interface ITenantSelection
    {
        string[] AllTenants { get; }
        string CurrentTenant { get;  }
        void ChangeTenant(string tenant);
        event EventHandler<string> TenantChanged;
    }
    public class TenantSelection : BaseService, ITenantSelection
    {
        private readonly IMediaLibrary mediaLibrary;
        public const string DefaultTenantName = "Standard";

        public TenantSelection(IMediaLibrary mediaLibrary, ILogger<TenantSelection> logger) : base(logger)
        {
            this.mediaLibrary = mediaLibrary;
            LoadTenants();
        }

        private void LoadTenants()
        {
            AllTenants = mediaLibrary.GetSources()
                .Select(s => s.Tenant ?? string.Empty).Distinct().OrderBy(t => t).ToArray()
                .Select(t => string.IsNullOrWhiteSpace(t) ? DefaultTenantName : t).ToArray()
                .Distinct().ToArray();
            if (string.IsNullOrWhiteSpace(CurrentTenant))
                CurrentTenant = AllTenants.FirstOrDefault();
        }

        public string[] AllTenants { get; private set; }
        private string _CurrentTenant;
        public string CurrentTenant
        {
            get => _CurrentTenant;
            set
            {
                var changed = _CurrentTenant != value;
                _CurrentTenant = value;
                if (changed)
                    TenantChanged?.Invoke(this, value);
            }
        }
        public void ChangeTenant(string tenant)
        {
            if (!AllTenants.Contains(tenant))
                throw new ArgumentException(nameof(tenant));
            CurrentTenant = tenant;
        }
        public event EventHandler<string> TenantChanged;
    }
}
