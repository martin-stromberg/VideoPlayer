using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;


namespace VideoPlayer.Extensions
{
    public static class ImageSourceExt
    {
        public static async Task<Microsoft.Maui.Graphics.Color> GetPixelColorAsync(this ImageSource imageSource, int left, int top)
        {
            if (imageSource is StreamImageSource streamImageSource)
            {
                var stream = await streamImageSource.Stream(CancellationToken.None);
                using (MemoryStream memoryStream = new MemoryStream())
                    return memoryStream.GetPixelColorAsync(left, top);
            }
            else if (imageSource is FileImageSource)
            {
                using (var image = await Image.LoadAsync<Rgba32>(((FileImageSource)imageSource).File))
                    return image.GetPixelColor(left, top);
            }
            else
            {
                return Colors.Transparent;
            }
        }

        public static Microsoft.Maui.Graphics.Color GetPixelColorAsync(this MemoryStream imageStream, int left, int top)
        {
            imageStream.Position = 0;
            using (var image = Image.Load<Rgba32>(imageStream))
                return image.GetPixelColor(left, top);
        }

        public static Microsoft.Maui.Graphics.Color GetPixelColor(this Image<Rgba32> image, int left, int top)
        {
            var color = image[left, top];
            return new Microsoft.Maui.Graphics.Color(color.R, color.G, color.B, color.A);
        }
    }
}
