using System;
using System.Linq;

namespace VideoPlayer.ViewModels
{
    public static class NavigationManagerExtensions
    {

        public static MauiAppBuilder RegisterViewModels(this MauiAppBuilder builder)
        {
            builder.Services.RegisterViewModels();
            return builder;
        }

        public static IServiceCollection RegisterViewModels(this IServiceCollection services)
        {
            var bvm = typeof(BaseViewModel);
            var asm = bvm.Assembly;
            foreach (var model in asm.GetTypes().Where(vm => vm.IsAssignableTo(bvm)).Where(vm => vm != bvm))
            {
                services.AddTransient(model);
            }
            return services;
        }

    }
}
