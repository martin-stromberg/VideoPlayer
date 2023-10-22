using System;
using System.Linq;

namespace VideoPlayer.Services.MediaLibrary.Demo
{
    public interface IUserSecrets
    {

        string RespberryPiPassword { get; }

        string SyncfusionLicenseKey { get; }

    }
}
