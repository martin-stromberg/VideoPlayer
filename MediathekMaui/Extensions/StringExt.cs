using System;
using System.Linq;

namespace Mediathek.Extensions
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

        public static string Shorten(this string str, int maxLength)
        {
            if (str.Length < maxLength)
                return str;
            int offset = str.IndexOfAny(new char[] { ' ', ',', '.', '!', '?', ':', ';' }, 250);
            if (offset > 0)
                return $"{str.Substring(0, offset)}...";
            else
                return $"{str.Substring(0, 250)}...";
        }

    }
}
