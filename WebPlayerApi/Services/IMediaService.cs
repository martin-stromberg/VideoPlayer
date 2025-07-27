using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebPlayerApi.Models;

namespace WebPlayerApi.Services
{
    public interface IMediaService
    {
        CardResult<MediaItemDetailsDto> GetMediaItem(string id);
        PagedResult<MediaItemDto> GetMediaItems(string directory, int page, int pageSize);
        Stream GetMediaStream(string parentId, string id);
        Task ReloadAsync(string source);
    }

}
