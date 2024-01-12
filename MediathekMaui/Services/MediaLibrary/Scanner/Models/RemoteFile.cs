using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.Scanner.Models
{
    public class RemoteFile
    {

        public string Name { get; internal set; }

        public string Path { get; internal set; }

        public DateTime LastWriteTime { get; internal set; }

    }
}
