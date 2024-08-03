using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mediathek.Common
{
    public class PathTools
    {
        public static string GetDirectoryName(string path)
        {
            var name = path.Replace("\\", "/");
            while (name.Contains("/"))
                name = name.Remove(0, name.IndexOf("/") + 1);
            return name;
        }
        public static string GetDirectoryPath(string path)
        {
            var rootPath = path;
            var offsetSlash = rootPath.LastIndexOf("/");
            var offsetBackSlash = rootPath.LastIndexOf("\\");
            var offset = Math.Max(offsetSlash, offsetBackSlash);
            rootPath = rootPath.Substring(0, offset);
            return rootPath;
        }
    }
}
