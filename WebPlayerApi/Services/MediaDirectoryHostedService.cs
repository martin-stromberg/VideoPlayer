using WebPlayerApi.Models;

namespace WebPlayerApi.Services
{
    public class MediaDirectoryHostedService : BackgroundService
    {
        private readonly ILogger<MediaDirectoryHostedService> _logger;
        private readonly ISourceService sourceService;

        public MediaDirectoryHostedService(ILogger<MediaDirectoryHostedService> logger, ISourceService sourceService)
        {
            _logger = logger;
            this.sourceService = sourceService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MediaDirectoryHostedService gestartet.");

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); 

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Durchsuche Media-Verzeichnisse...");

                    ScanSources(stoppingToken);

                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler im MediaDirectoryHostedService");
                }
            }

            _logger.LogInformation("MediaDirectoryHostedService gestoppt.");
        }

        private void ScanSources(CancellationToken stoppingToken)
        {
            foreach (var source in sourceService.Items.Where(source => source.LastScan.Add(TimeSpan.FromHours(24)) < DateTime.Now))
                ScanSource(source, stoppingToken);
        }

        private void ScanSource(MediaDirectory source, CancellationToken stoppingToken)
        {
            var previousScan = source.LastScan;
            var scanner = new SourceScanner(source, _logger);
            scanner.ItemScanned += Scanner_ItemFound;
            scanner.Scan(stoppingToken);

            source.LastScan = DateTime.Now;
            sourceService.Update(source.Id, source);

            if (previousScan != DateTime.MinValue)
            {
                var mediaService = sourceService.GetMediaService(source);
                foreach (var item in mediaService.Items.Where(mi => mi.LastUpdate < previousScan))
                    mediaService.Remove(item.Id);
            }
        }

        private void Scanner_ItemFound(object? sender, MediaItem mediaItem)
        {
            var mediaService = sourceService.GetMediaService(mediaItem.Source);
            var existing = mediaService.Items.FirstOrDefault(mi => mi.FilePath == mediaItem.FilePath);
            if (existing is null)
                mediaService.Add(mediaItem);
            else
            {
                if (existing.Id is null)
                    existing.Id = Guid.NewGuid().ToString();
                mediaService.Update(existing.Id, mediaItem);
            }
        }
    }
}
