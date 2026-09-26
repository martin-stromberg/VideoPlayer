using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Liest Verzeichnisse und Dateien einer lokalen MediaSource per System.IO aus und gibt sie als MediaCollection/MediaItem aus.
    /// </summary>
    public class LocalMediaSourceReader : IMediaSourceReader
    {
        private readonly ILogger<LocalMediaSourceReader> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMediaSourceReader"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public LocalMediaSourceReader(ILogger<LocalMediaSourceReader> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Liefert die Root-Collection der angegebenen lokalen MediaSource.
        /// </summary>
        /// <param name="source">Die lokale MediaSource.</param>
        /// <returns>Die Root-Collection oder eine leere Folge, wenn der Quellpfad ungültig ist.</returns>
        public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
        {
            var rootCollection = CreateRootCollection(source);
            if (rootCollection is not null)
                yield return rootCollection;
        }

        /// <summary>
        /// Erstellt die Root-Collection der MediaSource oder null, wenn der Quellpfad ungültig ist.
        /// </summary>
        /// <param name="source">Die lokale MediaSource.</param>
        /// <returns>Die Root-Collection oder <c>null</c>, wenn der Quellpfad ungültig ist.</returns>
        private MediaCollection? CreateRootCollection(MediaSource source)
        {
            if (string.IsNullOrWhiteSpace(source.Path))
            {
                _logger.LogWarning("MediaSource '{Name}' hat keinen gültigen Pfad und wird beim Root-Scan übersprungen.", source.Name);
                return null;
            }

            string name;
            DateTime createdAt;
            try
            {
                name = new DirectoryInfo(source.Path).Name;
                createdAt = Directory.GetLastWriteTimeUtc(source.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Pfad '{Path}' der MediaSource '{Name}' ist ungültig; die Quelle wird beim Root-Scan übersprungen.", source.Path, source.Name);
                return null;
            }

            return new MediaCollection
            {
                Name = string.IsNullOrEmpty(name) ? source.Path : name,
                Path = source.Path,
                CreatedAt = createdAt,
                MediaSourceId = (int)source.Id,
                ParentMediaCollectionId = null
            };
        }

        /// <summary>
        /// Liest die erste Ebene eines lokalen Verzeichnisses und liefert direkte Unterverzeichnisse und Dateien.
        /// </summary>
        /// <param name="collection">Die MediaCollection, deren Inhalt gelesen wird.</param>
        /// <returns>Direkte Unterverzeichnisse und Dateien.</returns>
        public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
        {
            foreach (var entryPath in Directory.EnumerateFileSystemEntries(collection.Path))
            {
                var name = Path.GetFileName(entryPath);
                if (MediaEntryFilter.IsIgnoredEntry(name))
                    continue;

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entryPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    _logger.LogWarning(ex, "Eintrag '{EntryPath}' in '{CollectionPath}' konnte nicht gelesen werden und wird übersprungen.", entryPath, collection.Path);
                    continue;
                }
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    yield return new MediaCollection
                    {
                        Name = name,
                        Path = entryPath,
                        CreatedAt = Directory.GetLastWriteTimeUtc(entryPath),
                        MediaSource = collection.MediaSource,
                        MediaSourceId = (int)collection.MediaSourceId,
                        ParentMediaCollectionId = collection.Id
                    };
                }
                else
                {
                    yield return new MediaItem
                    {
                        Name = name,
                        Path = entryPath,
                        CreatedAt = File.GetLastWriteTimeUtc(entryPath),
                        MediaCollectionId = (int)collection.Id
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
        public Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
        {
            return Task.FromResult(ResolveFilePath(collection, fileName) is not null);
        }

        /// <summary>
        /// Reads the content of a file within the given media collection.
        /// </summary>
        /// <param name="collection">The media collection.</param>
        /// <param name="fileName">The file name to read.</param>
        /// <returns>The file content, or <c>null</c> if the file does not exist.</returns>
        public Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
        {
            var path = ResolveFilePath(collection, fileName);
            return Task.FromResult(path is null ? null : File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>
        /// Reads a file as a stream from the given media collection.
        /// </summary>
        /// <param name="collection">The media collection.</param>
        /// <param name="fileName">The file name to read.</param>
        /// <returns>The file stream, or <c>null</c> if the file does not exist.</returns>
        public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
        {
            var path = ResolveFilePath(collection, fileName);
            if (path is null)
                return Task.FromResult<Stream?>(null);

            var ms = new MemoryStream(File.ReadAllBytes(path));
            ms.Position = 0;
            return Task.FromResult<Stream?>(ms);
        }

        /// <summary>
        /// Gibt einen Stream für eine Datei im lokalen Verzeichnis zurück.
        /// </summary>
        /// <param name="collection">Die MediaCollection, die die Datei enthält.</param>
        /// <param name="fileName">Der Name der Datei.</param>
        /// <returns>Ein Stream-Objekt, das die Datei repräsentiert, oder null, wenn die Datei nicht existiert.</returns>
        public Stream? OpenFileStream(MediaCollection collection, string fileName)
        {
            var path = ResolveFilePath(collection, fileName);
            if (path is null)
                return null;

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// Löst eine Dateiangabe relativ zum Collection-Pfad auf und liefert den tatsächlichen Dateipfad.
        /// Liefert null bei Pfaden außerhalb des Quell-Roots oder bei ReparsePoint-/Nicht-Datei-Einträgen.
        /// </summary>
        /// <param name="collection">Die MediaCollection, relativ zu deren Pfad die Dateiangabe aufgelöst wird.</param>
        /// <param name="fileName">Die aufzulösende Dateiangabe.</param>
        /// <returns>Der tatsächliche Dateipfad oder <c>null</c>, wenn die Datei nicht aufgelöst werden kann.</returns>
        private string? ResolveFilePath(MediaCollection collection, string fileName)
        {
            var root = collection.MediaSource?.Path;
            if (string.IsNullOrEmpty(root))
                root = collection.Path;

            string resolvedPath;
            string normalizedRoot;
            try
            {
                resolvedPath = Path.GetFullPath(Path.Combine(collection.Path, fileName));
                normalizedRoot = Path.GetFullPath(root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _logger.LogDebug(ex, "Dateipfad '{FileName}' in Collection '{CollectionPath}' konnte nicht aufgelöst werden.", fileName, collection.Path);
                return null;
            }

            var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;

            if (!resolvedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                && !resolvedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = resolvedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : resolvedPath.Substring(rootWithSeparator.Length);

            return ResolveSegmentsCaseInsensitive(normalizedRoot, relative, fileName);
        }

        /// <summary>
        /// Löst einen relativen Pfad segmentweise case-insensitiv unterhalb von <paramref name="normalizedRoot"/> auf.
        /// ReparsePoints und Nicht-Datei-Endpunkte liefern null.
        /// </summary>
        /// <param name="normalizedRoot">Der normalisierte Quell-Root.</param>
        /// <param name="relativePath">Der relative Pfad unterhalb des Roots.</param>
        /// <param name="fileName">Die ursprüngliche Dateiangabe (für Logmeldungen).</param>
        /// <returns>Der tatsächliche Dateipfad oder <c>null</c>, wenn der Pfad nicht zu einer regulären Datei führt.</returns>
        private string? ResolveSegmentsCaseInsensitive(string normalizedRoot, string relativePath, string fileName)
        {
            var current = normalizedRoot;
            var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                string? match = null;
                try
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                    {
                        if (Path.GetFileName(entry).Equals(segments[i], StringComparison.OrdinalIgnoreCase))
                        {
                            match = entry;
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    _logger.LogDebug(ex, "Verzeichnis '{DirectoryPath}' konnte beim Auflösen von '{FileName}' nicht gelesen werden.", current, fileName);
                    return null;
                }

                if (match is null)
                    return null;

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(match);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    _logger.LogDebug(ex, "Attribute von '{EntryPath}' konnten nicht gelesen werden.", match);
                    return null;
                }
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    return null;

                var isLast = i == segments.Length - 1;
                if (isLast)
                {
                    if (attributes.HasFlag(FileAttributes.Directory))
                        return null;
                    return match;
                }

                if (!attributes.HasFlag(FileAttributes.Directory))
                    return null;

                current = match;
            }

            return null;
        }
    }
}
