using System;
using System.Linq;

namespace Mediathek.Extensions
{
    public static  class ArrayExt
    {

        public static int IndexOf(this Array arr, object elem)
        {
            for (int idx = arr.GetLowerBound(0); idx < arr.GetLength(0); idx++)
            {
                if (arr.GetValue(idx).Equals(elem))
                    return idx;
            }
            return arr.GetLowerBound(0) - 1;
        }

    }
}
