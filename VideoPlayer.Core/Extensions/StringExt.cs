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


        public static string LongestCommonPrefix(this string[] strs)
        {
            if (strs == null || strs.Length == 0) return "";
            string prefix = strs[0];
            for (int i = 1; i < strs.Length; i++)
            {
                while (strs[i].IndexOf(prefix) != 0)
                {
                    prefix = prefix.Substring(0, prefix.Length - 1);
                    if (string.IsNullOrEmpty(prefix)) return "";
                }
            }
            return prefix;
        }
    }
}
