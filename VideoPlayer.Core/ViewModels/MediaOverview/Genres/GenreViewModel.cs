using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.ViewModels.MediaOverview.Genres
{
    public class GenreViewModel : BaseViewModel
    {
        private readonly Genre genre;

        public GenreViewModel(Genre genre, ILogger logger)
            :base(logger)
        {
            this.genre = genre;
            Title = genre is null ? "Alle" : genre.Name;
            Tapped = new Command(() => { OnSelected(); });
        }

        
        public ImageSource Icon { get => GetProperty<ImageSource>(); set => SetProperty(value); }
        public Command Tapped { get; }

        public event EventHandler Selected;
        private void OnSelected()
        {
            try
            {
                Selected?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }
    }
}
