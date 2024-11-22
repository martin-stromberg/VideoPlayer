using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.MediaOverview.Cards
{
    public class BaseCardPage: BaseContentPage
    {
        private IMediaLibrary mediaLibrary;
        private IServiceProvider serviceProvider;

        public BaseCardPage()
            :base()
        {            
            
        }

        public virtual long ElementId { get; set; }
        protected ClassifiedEntry Entry { get; private set; }
        protected override void OnLoadingContent(IApplicationManager applicationManager)
        {
            base.OnLoadingContent(applicationManager);
            mediaLibrary = applicationManager.ResolveService<IMediaLibrary>();
            serviceProvider = applicationManager.ResolveService<IServiceProvider>();
            Entry = mediaLibrary.GetClassifiedEntry(ElementId);
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();            
        }

        protected T1 CreateCardViewModel<T1, T2>(T2 movie) 
            where T1: BaseMediaItemCardViewModel
        {
            object[] args = null;
            var method = typeof(T1).GetConstructors()
                .Select(c =>
                {
                    args = c.GetParameters()
                        .Select(p =>
                        {
                            if (p.ParameterType == typeof(T2))
                                return movie;
                            var arg = serviceProvider.GetService(p.ParameterType);                            
                            return arg;
                        }).ToArray();
                    return args.Contains(null) ? null : c;
                })
                .Where(a => a is not null)
                .FirstOrDefault();
            //return (T1)method.Invoke(args);
            return (T1)Activator.CreateInstance(typeof(T1), args);
        }
    }
}
