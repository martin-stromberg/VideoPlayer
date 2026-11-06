using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VideoWebPlayer.Services;

/// <summary>
/// Builds <c>sftp://</c> URIs that are handed to ffprobe directly, so remote media files can be
/// probed through libssh's seekable SFTP access instead of streaming the whole file through
/// <c>pipe:0</c>. Also masks the credentials embedded in such URIs for log output.
/// </summary>
internal static class SftpProbeUriBuilder
{
    /// <summary>
    /// Matches the <c>scheme://userinfo@</c> part of an absolute URI so the userinfo (username
    /// and password) can be masked. Userinfo never contains <c>/</c>, <c>@</c> or whitespace
    /// because those characters are percent-encoded.
    /// </summary>
    private static readonly Regex UserInfoPattern =
        new(@"([a-zA-Z][a-zA-Z0-9+.\-]*://)[^/\s@]*@", RegexOptions.Compiled);

    /// <summary>
    /// Builds an <c>sftp://user:pass@host:port/path</c> URI for the given connection data.
    /// Username, password and each path segment are percent-encoded, so special characters
    /// (<c>:</c>, <c>@</c>, <c>/</c>, <c>%</c>, spaces, …) cannot break the URI structure.
    /// </summary>
    /// <param name="host">SFTP host name or address (IPv6 literals are bracketed automatically).</param>
    /// <param name="port">SFTP port; values &lt;= 0 omit the port from the URI.</param>
    /// <param name="username">Login user name.</param>
    /// <param name="password">Login password.</param>
    /// <param name="remotePath">Absolute path of the file on the SFTP server (must start with <c>/</c>).</param>
    /// <returns>
    /// The URI string, or <see langword="null"/> when host, credentials or an absolute remote
    /// path are missing — callers should then fall back to streaming via <c>pipe:0</c>.
    /// </returns>
    public static string? TryBuildUri(
        string? host, int port, string? username, string? password, string? remotePath)
    {
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrEmpty(username)
            || string.IsNullOrEmpty(password)
            || string.IsNullOrEmpty(remotePath)
            || remotePath[0] != '/')
        {
            return null;
        }

        var hostPart = host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[')
            ? $"[{host}]"
            : host;
        var portPart = port > 0
            ? ":" + port.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        return $"sftp://{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@{hostPart}{portPart}{EncodePath(remotePath)}";
    }

    /// <summary>
    /// Resolves the absolute path of <paramref name="item"/> on its SFTP server:
    /// <see cref="Data.MediaEntry.Path"/> already carries the full remote path recorded at scan
    /// time; otherwise collection path and file name are combined like
    /// <see cref="IMediaSourceReader.OpenFileStream"/> does. Returns <see langword="null"/>
    /// when no absolute path can be formed.
    /// </summary>
    /// <param name="collection">The item's media collection (path source).</param>
    /// <param name="item">The media item to resolve.</param>
    internal static string? ResolveRemotePath(Data.MediaCollection collection, Data.MediaItem item)
    {
        var path = item.Path;
        if (!string.IsNullOrEmpty(path) && path[0] == '/')
            return path;

        var combined = CombinePath(collection.Path, string.IsNullOrEmpty(path) ? item.Name : path);
        return combined.Length > 0 && combined[0] == '/' ? combined : null;
    }

    /// <summary>
    /// Combines a remote directory path and file name the way
    /// <see cref="SftpMediaSourceReader"/> resolves streamed files.
    /// </summary>
    /// <param name="directoryPath">Directory path on the SFTP server (may be empty).</param>
    /// <param name="fileName">File name (may be empty).</param>
    /// <returns>The combined path.</returns>
    public static string CombinePath(string? directoryPath, string? fileName)
    {
        if (string.IsNullOrEmpty(directoryPath)) return fileName ?? string.Empty;
        if (string.IsNullOrEmpty(fileName)) return directoryPath;
        return directoryPath.TrimEnd('/') + "/" + fileName.TrimStart('/');
    }

    /// <summary>
    /// Masks the userinfo (username and password) embedded in a URI, e.g. for log output:
    /// <c>sftp://user:pass@host/path</c> becomes <c>sftp://***@host/path</c>.
    /// </summary>
    /// <param name="uri">URI that may contain a userinfo part.</param>
    /// <returns>The URI with its userinfo replaced by <c>***</c>.</returns>
    public static string MaskCredentials(string uri)
        => MaskCredentialsInText(uri);

    /// <summary>
    /// Masks the userinfo of every absolute URI inside an arbitrary text — used to sanitize
    /// ffprobe stderr before logging, because ffprobe echoes the input URI (including the
    /// password) in error messages.
    /// </summary>
    /// <param name="text">Text that may contain URIs with credentials.</param>
    /// <returns>The text with all URI userinfo parts replaced by <c>***</c>.</returns>
    public static string MaskCredentialsInText(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : UserInfoPattern.Replace(text, "$1***@");

    private static string EncodePath(string remotePath)
    {
        var builder = new StringBuilder(remotePath.Length + 8);
        foreach (var segment in remotePath.TrimStart('/').Split('/'))
        {
            builder.Append('/');
            builder.Append(Uri.EscapeDataString(segment));
        }

        return builder.ToString();
    }
}
