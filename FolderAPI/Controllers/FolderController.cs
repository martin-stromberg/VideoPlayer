using FolderAPI.Models;
using FolderAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FolderAPI.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("[controller]")]
    public class FolderController : ControllerBase
    {
        private readonly FileManager fileManager;

        public FolderController(FileManager fileManager)
        {
            this.fileManager = fileManager;
        }

        //[HttpGet(Name = "GetFolder")]
        //public Folder Index()
        //{
        //    return fileManager.GetFolder();
        //}

        [HttpGet(Name = "GetFolder2")]
        public Folder GetFolder([FromQuery]string path)
        {
            return fileManager.GetFolder(path);
        }

    }
}
