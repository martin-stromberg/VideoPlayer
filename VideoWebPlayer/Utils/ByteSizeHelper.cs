namespace VideoWebPlayer.Utils
{
    /// <summary>
    /// Helper utilities for formatting byte counts in a human readable form.
    /// </summary>
    public static class ByteSizeHelper
    {
        /// <summary>Unit label for mebibytes used by <see cref="SplitBytes"/>/<see cref="ToBytes"/>.</summary>
        public const string UnitMegabytes = "MB";

        /// <summary>Unit label for gibibytes used by <see cref="SplitBytes"/>/<see cref="ToBytes"/>.</summary>
        public const string UnitGigabytes = "GB";

        private const decimal BytesPerMegabyte = 1024m * 1024m;
        private const decimal BytesPerGigabyte = 1024m * 1024m * 1024m;

        /// <summary>
        /// Formats a byte count using the largest fitting unit (B, KB, MB, GB)
        /// with up to two decimal places.
        /// </summary>
        /// <param name="bytes">The byte count to format.</param>
        /// <returns>A string such as <c>512 B</c> or <c>1.5 GB</c>.</returns>
        public static string FormatBytes(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB"];
            var value = (double)bytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }

        /// <summary>
        /// Splits a byte count into a value and a unit (MB or GB) suitable for
        /// editing — GB once the count reaches one gibibyte, otherwise MB.
        /// </summary>
        public static (decimal Value, string Unit) SplitBytes(long bytes)
        {
            return bytes >= (long)BytesPerGigabyte
                ? ((decimal)bytes / BytesPerGigabyte, UnitGigabytes)
                : ((decimal)bytes / BytesPerMegabyte, UnitMegabytes);
        }

        /// <summary>
        /// Converts a value in the given unit (<see cref="UnitMegabytes"/> or
        /// <see cref="UnitGigabytes"/>, defaulting to MB) back into bytes,
        /// clamped to the <see cref="long"/> range.
        /// </summary>
        public static long ToBytes(decimal value, string? unit)
        {
            var factor = unit == UnitGigabytes ? BytesPerGigabyte : BytesPerMegabyte;
            if (value <= 0)
                return 0;
            if (value > long.MaxValue / factor)
                return long.MaxValue;
            return (long)decimal.Round(value * factor);
        }
    }
}
