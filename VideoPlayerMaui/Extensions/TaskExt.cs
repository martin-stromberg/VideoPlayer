using System;
using System.Linq;

namespace VideoPlayer.Extensions
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
