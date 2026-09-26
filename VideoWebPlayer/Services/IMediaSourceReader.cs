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
    /// <param name="source">Die Medienquelle, deren Root gelesen wird.</param>
    /// <returns>Die Root-Collection der Medienquelle.</returns>
    IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source);

    /// <summary>
    /// Liefert die direkten Unterverzeichnisse und Dateien einer MediaCollection.
    /// </summary>
    /// <param name="collection">Die MediaCollection, deren Inhalt gelesen wird.</param>
    /// <returns>Die direkten Unterverzeichnisse und Dateien.</returns>
    IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection);

    /// <summary>
    /// Checks whether a file exists in the given media collection.
    /// </summary>
    /// <param name="collection">The media collection.</param>
    /// <param name="fileName">The file name to look for.</param>
    /// <returns><c>true</c> if the file exists; otherwise <c>false</c>.</returns>
    Task<bool> FileExistsAsync(MediaCollection collection, string fileName);

    /// <summary>
    /// Reads the content of a file within the given media collection.
    /// </summary>
    /// <param name="collection">The media collection.</param>
    /// <param name="fileName">The file name to read.</param>
    /// <returns>The file content, or <c>null</c> if the file does not exist.</returns>
    Task<string?> ReadFileAsync(MediaCollection collection, string fileName);

    /// <summary>
    /// Reads a file as a stream from the given media collection.
    /// </summary>
    /// <param name="collection">The media collection.</param>
    /// <param name="fileName">The file name to read.</param>
    /// <returns>The file stream, or <c>null</c> if the file does not exist.</returns>
    Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName);

    /// <summary>
    /// Gibt einen Stream für eine Datei der Quelle zurück, oder null, wenn die Datei nicht existiert.
    /// </summary>
    /// <param name="collection">Die MediaCollection, die die Datei enthält.</param>
    /// <param name="fileName">Der Name der Datei.</param>
    /// <returns>Ein Stream für die Datei oder <c>null</c>, wenn die Datei nicht existiert.</returns>
    Stream? OpenFileStream(MediaCollection collection, string fileName);
}
