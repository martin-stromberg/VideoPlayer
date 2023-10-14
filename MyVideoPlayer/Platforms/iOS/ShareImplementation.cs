using Foundation;
using System;
using System.Linq;
using UIKit;

namespace MyVideoPlayer.Platforms.iOS
{
    public class OwnShareImplementation: IShare
    {

        public Task RequestAsync(ShareTextRequest request)
        {
            throw new NotImplementedException();
        }

        public async Task RequestAsync(ShareFileRequest request)
        {
            var items = new NSObject[] { NSObject.FromObject(request.Title), NSUrl.FromFilename(request.File.FullPath) };
            var activityController = new UIActivityViewController(items, null);
            var rootController = UIApplication.SharedApplication.KeyWindow.RootViewController;

            NSString[] excludedActivityTypes = null;
            excludedActivityTypes = new NSString[]
            {
                UIActivityType.AssignToContact,
                UIActivityType.AddToReadingList,
                UIActivityType.CopyToPasteboard,
                UIActivityType.SaveToCameraRoll
            };

            if ((excludedActivityTypes != null) && (excludedActivityTypes.Length > 0))
                activityController.ExcludedActivityTypes = excludedActivityTypes;

            await rootController.PresentViewControllerAsync(activityController, true);
        }

        public Task RequestAsync(ShareMultipleFilesRequest request)
        {
            throw new NotImplementedException();
        }

    }
}
