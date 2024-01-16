using FolderAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace FolderAPI.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("[controller]")]
    public class FileController: ControllerBase
    {

        private readonly FileManager fileManager;

        public FileController(FileManager fileManager)
        {
            this.fileManager = fileManager;
        }

        [HttpGet(Name = "GetFile")]
        public IActionResult Index([FromQuery]string path)
        {
            var filePath = fileManager.GetFilePath(path);
            try
            {
                var fileWriteTime = System.IO.File.GetLastWriteTime(filePath);
                fileWriteTime = fileWriteTime.AddMilliseconds(-fileWriteTime.Millisecond);

                var fileName = Path.GetFileName(filePath);
                var fileType = $"application/{Path.GetExtension(filePath).Remove(0, 1)}";
                var hashFilePath = $"{filePath}.hash";
                var hashValue = string.Empty;
                if (System.IO.File.Exists(hashFilePath))
                {
                    var hashArgs = System.IO.File.ReadAllText(hashFilePath).Split("\r\n");
                    DateTime.TryParse(hashArgs[1], out var hashTime);
                    if (hashTime.AddMilliseconds(-hashTime.Millisecond) != fileWriteTime)
                        System.IO.File.Delete(hashFilePath);
                    else
                        hashValue = hashArgs[0];
                }
                if (!System.IO.File.Exists(hashFilePath))
                {
                    using (Stream hashStream = new FileStream(filePath, FileMode.Open))
                        using (var hash = SHA512.Create())
                            hashValue = BitConverter.ToString(hash.ComputeHash(hashStream)).Replace("-", string.Empty);
                    System.IO.File.WriteAllText(hashFilePath, $"{hashValue}\r\n{fileWriteTime}");
                }
                Response.Headers["X-Hash-SHA512"] = hashValue;
                var stream = new FileStream(filePath, FileMode.Open);
                return File(stream, fileType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost(Name = "SetFile")]
        public IActionResult Upload(
            [FromQuery]string path,
            [FromQuery]bool overwrite,
            [FromQuery]bool isTextFile,
            IFormFile file)
        {
            try
            {
                if (file == null)
                    throw new ArgumentNullException(nameof(file));
                using (Stream strm = file.OpenReadStream())
                    fileManager.SaveFile(strm, path, overwrite, isTextFile);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

    }
}
