using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Tools
{
    public static class PathTools
    {
        private static char[] _PathDelimiters = new char[] { '/', '\\'};
        public static string Combine(params string[] parts)
        {            
            var slash = parts.FirstOrDefault().Contains('/');
            var backslash = parts.FirstOrDefault().Contains('/');
            if (!backslash || slash)
            {
                for (int idx = parts.GetLowerBound(0) + 1; idx <= parts.GetUpperBound(0); idx++)
                    parts[idx] = parts[idx].TrimStart('/');
                return string.Join('/', parts.Select(p => p.TrimEnd('/')));
            }
            else
                return string.Join('\\', parts);
        }

        public static string IncludeTrailingPathDelimiter(this string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return _PathDelimiters.First().ToString();

            var offset = path.LastIndexOfAny(_PathDelimiters);
            var delimiter = (offset >= 0) ? path[offset] : _PathDelimiters.First();
            if (path.EndsWith(delimiter))
                return path;
            else
                return $"{path}{delimiter}";

        }
    }
}
