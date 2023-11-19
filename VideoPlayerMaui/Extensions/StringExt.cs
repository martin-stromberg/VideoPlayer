using System;
using System.Linq;

namespace VideoPlayer.Extensions
{
    public static class StringExt
    {

        public static DateTime ToDateTime(this string str)
        {
            if (DateTime.TryParse(str, out var dt))
                return dt;
            return default(DateTime);
        }

        public static int ToInt32(this string str)
        {
            if (int.TryParse(str, out var value))
                return value;
            return default(int);
        }

    }
}
