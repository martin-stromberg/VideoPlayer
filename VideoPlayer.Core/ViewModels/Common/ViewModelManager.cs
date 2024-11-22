using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service;
using VideoPlayer.ViewModels.MediaOverview;

namespace VideoPlayer.ViewModels.Common
{
    public interface IViewModelManager
    {
        BaseViewModel Get<T>() where T : BaseViewModel;
    }
    public class ViewModelManager : IViewModelManager
    {
        private readonly IApplicationManager applicationManager;
        private ConcurrentDictionary<Type, BaseViewModel> _Cache = new ConcurrentDictionary<Type, BaseViewModel>();

        public ViewModelManager(IApplicationManager applicationManager)
        {
            this.applicationManager = applicationManager;
        }

        public BaseViewModel Get<T>() where T:BaseViewModel
        {
            var vmType = typeof(T);
            if (!_Cache.ContainsKey(vmType))
            {
                var vm = applicationManager.ResolveService<T>();
                _Cache.AddOrUpdate(vmType, vm, (key, existing) => existing);
            }
            (_Cache[vmType] as IReusableViewModel)?.Reuse();
            return _Cache[vmType];
        }
    }
}
