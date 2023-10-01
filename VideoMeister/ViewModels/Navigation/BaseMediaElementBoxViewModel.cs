using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VideoMeister.ViewModels.Navigation
{
    public class BaseMediaElementBoxViewModel: BaseViewModel
    {
        public BaseMediaElementBoxViewModel()
        {
        }

        public string Name { get; set; }

        public void ProcessTapped()
        {
            OnTapped();
        }

        public event EventHandler Tapped;
        protected virtual void OnTapped()
        {
            Tapped?.Invoke(this, EventArgs.Empty);
        }
    }
}
