using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Abstraktion des Dateizugriffs auf eine Medienquelle (SFTP oder lokales Verzeichnis).
/// </summary>
public interface IMediaSourceReader
{
    /// <summary>
    /// Liefert die Root-Collection der angegebenen Medienquelle.
    /// </summary>
    IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source);

    /// <summary>
    /// Liefert die direkten Unterverzeichnisse und Dateien einer MediaCollection.
    /// </summary>
    IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection);

    /// <summary>
    /// Checks whether a file exists in the given media collection.
    /// </summary>
    Task<bool> FileExistsAsync(MediaCollection collection, string fileName);

    /// <summary>
    /// Reads the content of a file within the given media collection.
    /// </summary>
    Task<string?> ReadFileAsync(MediaCollection collection, string fileName);

    /// <summary>
    /// Reads a file as a stream from the given media collection.
    /// </summary>
    Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName);

    /// <summary>
    /// Gibt einen Stream für eine Datei der Quelle zurück, oder null, wenn die Datei nicht existiert.
    /// </summary>
    Stream? OpenFileStream(MediaCollection collection, string fileName);
}
