using Microsoft.AspNetCore.Mvc;
using System.IO;
using WebPlayer.Client.Services;
using WebPlayer.Data;

namespace WebPlayer.Controllers
{
    [ApiController]
    [Route("api/mediadirectories/access")]
    public class MediaDirectoryAccessController : ControllerBase
    {
        private readonly IUserCollection _userCollection;
        private readonly IServiceAPIClient aPIClient;

        public MediaDirectoryAccessController(IUserCollection userCollection, IServiceAPIClient aPIClient)
        {
            _userCollection = userCollection;
            this.aPIClient = aPIClient;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetAccessUsersAsync([FromQuery] string sourceId = "")
        {
            var users = _userCollection.GetAll();
            if (!string.IsNullOrWhiteSpace(sourceId))
                try
                {
                    var sources = await aPIClient.GetSourcesAsync();
                    var source = sources.FirstOrDefault(s => s.Id == sourceId);
                    users = users.Where(user => _userCollection.HasAccess(user, source)).ToArray();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> SetAccessUsersAsync([FromQuery]string directoryId, [FromBody] string[] userIds)
        {
            try
            {
                var sources = await aPIClient.GetSourcesAsync();
                var source = sources.FirstOrDefault(s => s.Id == directoryId);
                var users = _userCollection.GetAll().Where(u => userIds.Contains(u.Id)).ToArray();
                _userCollection.ChangeAccess(source, users);
                return Ok();
            }
            catch(Exception ex)
            {
                return BadRequest(ex);
            }
        }
    }
}
