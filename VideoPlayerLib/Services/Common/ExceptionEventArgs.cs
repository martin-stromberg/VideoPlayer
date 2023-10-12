using System;
using System.Linq;

namespace VideoPlayerLib.Services.Common
{
    public class ExceptionEventArgs: EventArgs
    {

        public ExceptionEventArgs(Exception error)
            : base()
        {
            Error = error;
        }

        public Exception Error { get; }

    }

}
