using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Resources;

namespace VideoPlayer.ViewModels.MediaOverview.Genres
{
    public class GenreSelectionViewModel : BaseViewModel
    {
        private readonly IMediaLibrary mediaLibrary;
        private readonly IResourceManager resourceManager;

        public GenreSelectionViewModel(IMediaLibrary mediaLibrary, IResourceManager resourceManager)
            : base()
        {
            this.mediaLibrary = mediaLibrary;
            this.resourceManager = resourceManager;
        }
        public ObservableCollection<GenreViewModel> Items { get; } = new ObservableCollection<GenreViewModel>();

        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
            Reload();
        }

        private void Reload()
        {
            var genres = mediaLibrary.GetGenres()
                .OrderBy(g => g.Name);
            BaseServiceModelEventArgs args = null;
            foreach (var genre in genres)
                if (!Items.Any(i => genre.Name == i.Title))
                {
                    if (args is null)
                        args = new BaseServiceModelEventArgs(genre);
                    args.ModelObject = genre;
                    GenreLoaded?.Invoke(this, args);
                    if (args.ModelObject is null)
                        continue;
                    var vm = new GenreViewModel(args.ModelObject as Genre)
                    {
                        Icon = resourceManager.GetGenreIcon(args.ModelObject as Genre)
                    };
                    vm.Selected += GenreViewModel_Selected;
                    Items.Add(vm);
                }
        }
        public event EventHandler<string> GenreSelected;
        public event EventHandler<BaseServiceModelEventArgs> GenreLoaded;

        private void GenreViewModel_Selected(object sender, EventArgs e)
        {
            GenreSelected?.Invoke(this, (sender as GenreViewModel).Title);
        }
    }
}
