using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.Service.Library.Scanner;
using VideoPlayer.Service.Processor;

namespace VideoPlayer.ViewModels.HomePage
{
    public class ErrorViewModel: BaseHomePageViewModel
    {
        private readonly IErrorLogManager errorLogManager;

        public ErrorViewModel(
            IErrorLogManager errorLogManager, 
            ILibraryScanner libraryScanner,
            IProcessorCollection processorCollection,
            ILogger<ErrorViewModel> logger)
            :base(processorCollection, libraryScanner, logger)
        {
            this.errorLogManager = errorLogManager;
            ErrorMessages = string.Join("\r\n", errorLogManager.ReadErrors());
        }
    }
}
