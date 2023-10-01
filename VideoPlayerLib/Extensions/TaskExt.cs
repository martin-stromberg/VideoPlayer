using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayerLib
{
    public static class TaskExt
    {
        public static T Wait<T>(this Task<T> t)
        {
            t.Wait();
            return t.Result;
        }
    }
}
