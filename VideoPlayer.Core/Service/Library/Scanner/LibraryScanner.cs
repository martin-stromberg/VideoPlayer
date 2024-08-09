using System;
using System.Linq;
using System.Reflection;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Service.Library.Scanner
{
    public interface ILibraryScanner
    {

        void Start();

    }

    public class LibraryScanner: TimerService, ILibraryScanner
    {

        private readonly IMediaLibrary _MediaLibrary;
        private Dictionary<Type, ISourceReader> _SourceReaderTypes = new Dictionary<Type, ISourceReader>();

        public LibraryScanner(IMediaLibrary mediaLibrary)
            : base()
        {
            _MediaLibrary = mediaLibrary;
            DueTime = TimeSpan.FromSeconds(10);
            Period = TimeSpan.FromSeconds(60);
        }

        protected override async Task ExecuteTimerAsync()
        {
            await ScanNextSourceAsync();
        }

        private async Task ScanNextSourceAsync()
        {
            var source = _MediaLibrary.GetNextScanSource();
            if (source is null)
                return;
            await ScanSourceAsync(source);
        }

        private ISourceReader CreateReader(MediaSource source)
        {
            var sourceType = source.GetType();
            var destType = typeof(ISourceReader);
            if (!_SourceReaderTypes.ContainsKey(sourceType))
            {
                destType = sourceType.Assembly
                                     .GetTypes()
                                     .Where(t => !t.IsAbstract)
                                     .Where(t => t.IsAssignableTo(destType))
                                     .Where(t =>
                                     {
                                         var attr = t.GetCustomAttribute(typeof(ServiceModelReferenceAttribute)) as ServiceModelReferenceAttribute;
                                         if (attr is null)
                                             return false;
                                         if (attr.ServiceModelType != sourceType)
                                             return false;
                                         return true;
                                     })
                                     .FirstOrDefault();
                if (destType is null)
                    return null;
                _SourceReaderTypes.Add(sourceType, Activator.CreateInstance(destType, source) as ISourceReader);
            }
            return _SourceReaderTypes[sourceType];
        }

        private async Task ScanSourceAsync(MediaSource source)
        {
            var reader = CreateReader(source);
            var root = reader.GetRoot();
            await ScanAsync(reader, root);
        }

        private async Task ScanAsync(ISourceReader reader, SourceFolder parentFolder)
        {
            ProcessFolder(parentFolder);

            var folders = await reader.ReadFoldersAsync(parentFolder);
            foreach (var folder in folders)
                await ScanAsync(reader, folder);

            var files = await reader.ReadFilesAsync(parentFolder);
            foreach (var file in files)
                ProcessFile(file);
        }

        private void ProcessFolder(SourceFolder folder)
        {
            throw new NotImplementedException();
        }

        private void ProcessFile(SourceFile file)
        {
            throw new NotImplementedException();
        }

    }
}
