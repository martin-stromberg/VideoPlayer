using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Wählt pro Aufruf anhand von <see cref="MediaSource.SourceType"/> den passenden Reader aus.
    /// </summary>
    public class MediaSourceReaderDispatcher : IMediaSourceReader
    {
        private readonly SftpMediaSourceReader _sftpReader;
        private readonly LocalMediaSourceReader _localReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaSourceReaderDispatcher"/> class.
        /// </summary>
        /// <param name="sftpReader">Reader für SFTP-Quellen.</param>
        /// <param name="localReader">Reader für lokale Verzeichnis-Quellen.</param>
        public MediaSourceReaderDispatcher(SftpMediaSourceReader sftpReader, LocalMediaSourceReader localReader)
        {
            _sftpReader = sftpReader;
            _localReader = localReader;
        }

        /// <inheritdoc />
        public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
            => GetReader(source).ReadRootDirectory(source);

        /// <inheritdoc />
        public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
            => GetReader(collection).ReadDirectoryEntries(collection);

        /// <inheritdoc />
        public Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
            => GetReader(collection).FileExistsAsync(collection, fileName);

        /// <inheritdoc />
        public Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
            => GetReader(collection).ReadFileAsync(collection, fileName);

        /// <inheritdoc />
        public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
            => GetReader(collection).ReadFileStreamAsync(collection, fileName);

        /// <inheritdoc />
        public Stream? OpenFileStream(MediaCollection collection, string fileName)
            => GetReader(collection).OpenFileStream(collection, fileName);

        private IMediaSourceReader GetReader(MediaCollection collection)
        {
            if (collection?.MediaSource is null)
            {
                throw new InvalidOperationException(
                    "MediaCollection.MediaSource ist nicht geladen. Die MediaCollection muss eager geladen werden (z. B. per Include(mc => mc.MediaSource)).");
            }

            return GetReader(collection.MediaSource);
        }

        private IMediaSourceReader GetReader(MediaSource? source)
            => source?.SourceType == MediaSourceType.LocalDirectory ? _localReader : _sftpReader;
    }
}
