using System;
using System.Linq;

namespace VideoPlayer.Service
{
    public interface IApplicationManager
    {

        void Initialize();

        event EventHandler InitializationCompleted;

        bool Initialized { get; }

    }
}
