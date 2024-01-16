using FolderAPI.Services;
using ImageMagick;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Security.Cryptography;

namespace FolderAPI.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("[controller]")]
    public class ThumbnailController: ControllerBase
    {

        private readonly FileManager fileManager;
        private static ConcurrentQueue<string> thumbnails = new ConcurrentQueue<string>();
        private static BackgroundWorker _cleaner = null;
        private static int lastCleanCounter = 0;

        private static void StartCleaner(string tempFile)
        {
            thumbnails.Enqueue(tempFile);
            if (_cleaner != null)
                return;
            _cleaner = new BackgroundWorker();
            _cleaner.DoWork += _cleaner_DoWork;
            _cleaner.RunWorkerCompleted += async (sender, e) =>
            {
                await Task.Delay(5000);
                _cleaner.RunWorkerAsync();
            };
            _cleaner.RunWorkerAsync();
        }

        private static void _cleaner_DoWork(object? sender, DoWorkEventArgs e)
        {
            int currentCounter = thumbnails.Count;
            if (lastCleanCounter != currentCounter)
            {
                lastCleanCounter = currentCounter;
                return;
            }

            if (thumbnails.TryDequeue(out var filePath))
                try
                {
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    thumbnails.Enqueue(filePath);
                }
        }

        public ThumbnailController(FileManager fileManager)
        {
            this.fileManager = fileManager;
        }

        [HttpGet(Name = "GetThumbnail")]
        public IActionResult Index([FromQuery] string imagePath, [FromQuery]int maxWidth, [FromQuery]int maxHeight)
        {
            var filePath = fileManager.GetFilePath(imagePath);
            try
            {
                var returnFilePath = filePath;
                var tempFile = Path.ChangeExtension(Path.GetTempFileName(), Path.GetExtension(filePath));
                using (var image = new MagickImage(filePath))
                {
                    var widthFactor = (1.0 / image.Width) * ((double)maxWidth);
                    var heightFactor = (1.0 / image.Height) * ((double)maxHeight);
                    var minFactor = (widthFactor > heightFactor) ? heightFactor : widthFactor;
                    var newWidth = (int)(image.Width * minFactor);
                    var newHeight = (int)(image.Height * minFactor);

                    if ((image.Width > maxWidth) || (image.Height > maxHeight))
                    {
                        image.Format = image.Format; // Get or Set the format of the image.
                        image.Resize(newWidth, newHeight); // fit the image into the requested width and height. 
                        image.Quality = 100; // This is the Compression level.
                        image.Write(tempFile);
                        returnFilePath = tempFile;
                        StartCleaner(tempFile);
                    }
                }

                var fileName = Path.GetFileName(returnFilePath);
                var fileType = $"application/{Path.GetExtension(returnFilePath).Remove(0, 1)}";
                var hashValue = string.Empty;
                using (Stream hashStream = new FileStream(returnFilePath, FileMode.Open))
                    using (var hash = SHA512.Create())
                        hashValue = BitConverter.ToString(hash.ComputeHash(hashStream)).Replace("-", string.Empty);
                Response.Headers["X-Hash-SHA512"] = hashValue;
                var stream = new FileStream(returnFilePath, FileMode.Open);
                return File(stream, fileType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }
}
