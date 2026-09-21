namespace VideoWebPlayer.Utils
{
    /// <summary>
    /// Helper utilities for formatting byte counts in a human readable form.
    /// </summary>
    public static class ByteSizeHelper
    {
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
    }
}
