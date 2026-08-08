using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebPlayerApi.Models;
using WebPlayerApi.Services;

namespace WebPlayerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class MediaSourcesController : Controller
    {
        private readonly ISourceService _mediaService;

        public MediaSourcesController(ISourceService mediaService)
        {
            _mediaService = mediaService;
        }

        [HttpGet("")]
        public ActionResult<List<MediaDirectory>> GetAll()
        {
            return _mediaService.Items.ToList();
        }

        [HttpGet("{id}")]
        public ActionResult<MediaDirectory> GetById(string id)
        {
            var item = _mediaService.Get(id);
            if (item == null)
                return NotFound();
            return Ok(item);
        }


        [HttpPost]
        public ActionResult<MediaDirectory> Create(MediaDirectory directory)
        {            
            _mediaService.Add(directory);
            return CreatedAtAction(nameof(GetById), new { id = directory.Id }, directory);
        }

        [HttpPut("{id}")]
        public IActionResult Update(string id, MediaDirectory updated)
        {
            var existing = _mediaService.Get(id);
            if (existing == null)
                return NotFound();
            _mediaService.Update(id, updated);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(string id)
        {
            var existing = _mediaService.Get(id);
            if (existing == null)
                return NotFound();

            _mediaService.Remove(id);
            return NoContent();
        }

        [HttpPost("{id}/reload")]
        public IActionResult ReloadSource(string id)
        {
            var existing = _mediaService.Get(id);
            if (existing is null)
                return NotFound();
            existing.LastScan = DateTime.MinValue;
            _mediaService.Update(id, existing);
            return Ok();
        }
    }
}
