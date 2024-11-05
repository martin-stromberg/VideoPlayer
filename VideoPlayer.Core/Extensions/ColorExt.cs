using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.Extensions
{
    public static class ColorExt
    {
        public static Color GetComplementaryColor(this Color color)
        {
            byte r = (byte)(255 - color.Red * 255);
            byte g = (byte)(255 - color.Green * 255);
            byte b = (byte)(255 - color.Blue * 255);
            return new Color(r, g, b);
        }

        public static Color GetContrastingTextColor(this Color color)
        {
            double luminance = (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
            return luminance > 0.5 ? Colors.Black : Colors.White;
        }
    }
}
