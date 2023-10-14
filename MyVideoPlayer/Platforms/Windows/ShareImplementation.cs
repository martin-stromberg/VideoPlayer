using System;
using System.Linq;

namespace MyVideoPlayer.Platforms.Windows
{
    public class OwnShareImplementation: IShare
    {

        public Task RequestAsync(ShareTextRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task RequestAsync(ShareFileRequest request)
        {
            throw new NotImplementedException();
        }

        public Task RequestAsync(ShareMultipleFilesRequest request)
        {
            throw new NotImplementedException();
        }

    }
}
