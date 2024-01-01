using FolderAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace FolderAPI.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("[controller]")]
    public class FileController : ControllerBase
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
                var fileData = System.IO.File.ReadAllBytes(filePath);
                var fileName = Path.GetFileName(filePath);
                var fileType = $"application/{Path.GetExtension(filePath).Remove(0,1)}";
                return File(fileData, fileType, fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost(Name = "SetFile")]
        public IActionResult Upload([FromQuery]string path, [FromQuery]bool overwrite, [FromQuery]bool isTextFile, IFormFile file)
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
