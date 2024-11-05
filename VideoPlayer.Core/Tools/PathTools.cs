using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Tools
{
    public static class PathTools
    {
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
    }
}
