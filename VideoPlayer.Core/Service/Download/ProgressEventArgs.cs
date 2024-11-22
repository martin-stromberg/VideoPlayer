using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Service.Download
{
    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(decimal progress)
            :base()
        {
            Progress = progress;
        }

        public decimal Progress { get; internal set; }
    }
}
