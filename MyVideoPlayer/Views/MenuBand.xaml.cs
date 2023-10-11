
using MyVideoPlayer.ViewModels.Menu;

namespace MyVideoPlayer.Views
{
    public partial class MenuBand: ContentView
    {

        public MenuBand()
        {
            InitializeComponent();
        }

        protected MenuViewModel ViewModel
        {
            get
            {
                return BindingContext as MenuViewModel;
            }
        }

    }
}