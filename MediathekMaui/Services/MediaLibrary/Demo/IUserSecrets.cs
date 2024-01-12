using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.Demo
{
    public interface IUserSecrets
    {

        Task Initialize();

        string RespberryPiPassword { get; }

        string SyncfusionLicenseKey { get; }

    }
}
