using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.ErrorHandling;

namespace VideoPlayer.ViewModels.HomePage
{
    public class ErrorViewModel: BaseHomePageViewModel
    {
        private readonly IErrorLogManager errorLogManager;

        public ErrorViewModel(IErrorLogManager errorLogManager)
        {
            this.errorLogManager = errorLogManager;
            ErrorMessages = string.Join("\r\n", errorLogManager.ReadErrors());
        }
    }
}
