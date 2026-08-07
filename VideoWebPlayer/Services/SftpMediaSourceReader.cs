using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Renci.SshNet;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Liest Verzeichnisse und Dateien einer MediaSource per SFTP aus und gibt sie als MediaCollection/MediaItem aus.
    /// </summary>
    public class SftpMediaSourceReader
    {
        /// <summary>
        /// Liest das Rootverzeichnis der angegebenen MediaSource aus und liefert nur die Root-Collection.
        /// </summary>
        public virtual IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
        {
            // Root-Collection erzeugen
            var rootCollection = new MediaCollection
            {
                Name = string.IsNullOrEmpty(source.Path) ? "/" : source.Path.TrimEnd('/').Split('/')[^1],
                Path = source.Path,
                CreatedAt = DateTime.UtcNow,
                MediaSourceId = (int)source.Id,
                ParentMediaCollectionId = null
            };
            yield return rootCollection;
        }

        /// <summary>
        /// Liest die erste Ebene eines Verzeichnisses und liefert direkte Unterverzeichnisse und Dateien.
        /// </summary>
        /// <param name="collection">Die MediaCollection, deren Inhalt gelesen wird.</param>
        /// <returns>Direkte Unterverzeichnisse und Dateien.</returns>
        public virtual IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
        {
            using var client = new SftpClient(
                collection.MediaSource.Host,
                collection.MediaSource.Port,
                collection.MediaSource.Username,
                collection.MediaSource.Password);

            client.Connect();

            var entries = client.ListDirectory(collection.Path);
            foreach (var entry in entries)
            {
                if (IsIgnoredEntry(entry.Name))
                    continue;

                if (entry.IsDirectory)
                {
                    yield return new MediaCollection
                    {
                        Name = entry.Name,
                        Path = entry.FullName,
                        CreatedAt = entry.LastWriteTimeUtc,
                        MediaSource = collection.MediaSource,
                        MediaSourceId = (int)collection.MediaSourceId,
                        ParentMediaCollectionId = collection.Id
                    };
                }
                else if (entry.IsRegularFile)
                {
                    yield return new MediaItem
                    {
                        Name = entry.Name,
                        Path = entry.FullName,
                        CreatedAt = entry.LastWriteTimeUtc,
                        MediaCollectionId = (int)collection.Id
                    };
                }
            }

            client.Disconnect();
        }

        /// <summary>
        /// Liest rekursiv alle Unterverzeichnisse und Dateien ab einer MediaCollection (Teilbaum).
        /// </summary>
        public IEnumerable<MediaEntry> ReadSubtree(MediaCollection collection)
        {
            using var client = new SftpClient(
                collection.MediaSource.Host,
                collection.MediaSource.Port,
                collection.MediaSource.Username,
                collection.MediaSource.Password);

            client.Connect();

            foreach (var entry in ReadDirectoryInternal(client, collection.Path, collection))
                yield return entry;

            client.Disconnect();
        }

        /// <summary>
        /// Interne rekursive Methode zum Auslesen eines Verzeichnisses.
        /// </summary>
        private IEnumerable<MediaEntry> ReadDirectoryInternal(SftpClient client, string path, MediaCollection parentCollection)
        {
            var entries = client.ListDirectory(path);

            foreach (var entry in entries)
            {
                if (IsIgnoredEntry(entry.Name))
                    continue;

                if (entry.IsDirectory)
                {
                    var collection = new MediaCollection
                    {
                        Name = entry.Name,
                        Path = entry.FullName,
                        CreatedAt = entry.LastWriteTimeUtc,
                        MediaSource = parentCollection.MediaSource,
                        MediaSourceId = (int)parentCollection.MediaSourceId,
                        ParentMediaCollectionId = parentCollection.Id
                    };
                    yield return collection;

                    // Rekursiv Unterverzeichnisse auslesen
                    if (!collection.Skip)
                        foreach (var subEntry in ReadDirectoryInternal(client, entry.FullName, collection))
                            yield return subEntry;
                }
                else if (entry.IsRegularFile)
                {
                    yield return new MediaItem
                    {
                        Name = entry.Name,
                        Path = entry.FullName,
                        CreatedAt = entry.LastWriteTimeUtc,
                        MediaCollectionId = (int)parentCollection.Id
                    };
                }
            }
        }

        /// <summary>
        /// Checks whether a file exists in the given media collection.
        /// </summary>
        /// <param name="collection">The media collection.</param>
        /// <param name="fileName">The file name to look for.</param>
        /// <returns><c>true</c> if the file exists; otherwise <c>false</c>.</returns>
        public virtual async Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
        {
            using var client = new SftpClient(
                collection.MediaSource.Host,
                collection.MediaSource.Port,
                collection.MediaSource.Username,
                collection.MediaSource.Password);

            client.Connect();

            var files = client.ListDirectory(collection.Path);
            foreach (var file in files)
            {
                if (file.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the content of a file within the given media collection.
        /// </summary>
        /// <param name="collection">The media collection.</param>
        /// <param name="fileName">The file name to read.</param>
        /// <returns>The file content, or <c>null</c> if the file does not exist.</returns>
        public virtual async Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
        {
            using var client = new SftpClient(
                collection.MediaSource.Host,
                collection.MediaSource.Port,
                collection.MediaSource.Username,
                collection.MediaSource.Password);

            client.Connect();

            var files = client.ListDirectory(collection.Path);
            foreach (var file in files)
            {
                if (file.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = new MemoryStream();
                    client.DownloadFile(file.FullName, stream);
                    return System.Text.Encoding.UTF8.GetString(stream.ToArray());
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a file as a stream from the given media collection.
        /// </summary>
        /// <param name="collection">The media collection.</param>
        /// <param name="fileName">The file name to read.</param>
        /// <returns>The file stream, or <c>null</c> if the file does not exist.</returns>
        public virtual async Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
        {
            using var client = new SftpClient(
                collection.MediaSource.Host,
                collection.MediaSource.Port,
                collection.MediaSource.Username,
                collection.MediaSource.Password);

            client.Connect();

            var fullPath = CombineSftpPath(collection.Path, fileName);
            if (!client.Exists(fullPath))
                return null;

            var ms = new MemoryStream();
            await Task.Run(() => client.DownloadFile(fullPath, ms));
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Gibt einen Stream für eine Datei auf dem SFTP-Server zurück.
        /// Der Stream liest direkt von der SFTP-Verbindung.
        /// </summary>
        /// <param name="collection">Die MediaCollection, die die Datei enthält.</param>
        /// <param name="fileName">Der Name der Datei.</param>
        /// <returns>Ein Stream-Objekt, das die Datei repräsentiert, oder null, wenn die Datei nicht existiert.</returns>
        public SftpStreamWrapper? GetSftpFileStream(MediaCollection collection, string fileName)
        {
            var client = new SftpClient(
                collection.MediaSource.Host,
                collection.MediaSource.Port,
                collection.MediaSource.Username,
                collection.MediaSource.Password);

            client.Connect();

            var fullPath = CombineSftpPath(collection.Path, fileName);
            if (!client.Exists(fullPath))
            {
                client.Dispose();
                return null;
            }

            var stream = client.OpenRead(fullPath);
            return new SftpStreamWrapper(stream, client);
        }

        /// <summary>
        /// Prüft, ob ein Verzeichniseintrag beim Einlesen übergangen wird (Navigationseinträge und versteckte Einträge wie '.actors').
        /// </summary>
        private static bool IsIgnoredEntry(string name)
        {
            return name.StartsWith('.');
        }

        private static string CombineSftpPath(string part1, string part2)
        {
            if (string.IsNullOrEmpty(part1)) return part2;
            if (string.IsNullOrEmpty(part2)) return part1;
            return part1.TrimEnd('/') + "/" + part2.TrimStart('/');
        }
    }
}