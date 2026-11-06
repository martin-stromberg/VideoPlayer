using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoWebPlayer.Services;

/// <summary>
/// The ffmpeg input specification for one broadcast slot: a local file path handed to ffmpeg
/// directly (fast <c>-ss</c> seeking, also for MP4 files whose moov atom sits at the end), an
/// <c>http(s)</c> URL of the server's own range-capable media-stream endpoint (seekable via
/// HTTP range requests for remote sources) or a stream pumped into the process's standard
/// input (<c>pipe:0</c>) as the fallback for non-local sources. Instances are only created
/// through the factory methods so the input kind stays internally consistent — a pipe
/// factory delegate can never coexist with a file path or URL.
/// </summary>
public sealed record LiveStreamInput
{
    /// <param name="filePath">The local file path, or <see langword="null"/> for other inputs.</param>
    /// <param name="openStreamAsync">Factory that opens the media stream for pipe input.</param>
    /// <param name="httpUrl">The range-capable stream URL, or <see langword="null"/>.</param>
    /// <param name="sftpUrl">A direct <c>sftp://</c> URI for libssh-capable ffmpeg builds
    /// (seekable like the HTTP input), or <see langword="null"/>. Carries credentials in
    /// the userinfo — never log it unmasked.</param>
    /// <param name="resolutionNote">Optional human-readable reason why no preferred input was
    /// chosen — carried into the coordinator's <c>Started</c> detail so the pipe fallback is
    /// diagnosable instead of silent.</param>
    private LiveStreamInput(
        string? filePath,
        Func<CancellationToken, Task<Stream?>>? openStreamAsync,
        string? httpUrl = null,
        string? sftpUrl = null,
        string? resolutionNote = null)
    {
        FilePath = filePath;
        OpenStreamAsync = openStreamAsync;
        HttpUrl = httpUrl;
        SftpUrl = sftpUrl;
        ResolutionNote = resolutionNote;
    }

    /// <summary>Gets the local file path, or <see langword="null"/> for other inputs.</summary>
    public string? FilePath { get; }

    /// <summary>Gets the factory that opens the media stream for pipe input, or
    /// <see langword="null"/> for file/URL inputs.</summary>
    public Func<CancellationToken, Task<Stream?>>? OpenStreamAsync { get; }

    /// <summary>Gets the range-capable stream URL, or <see langword="null"/>.</summary>
    public string? HttpUrl { get; }

    /// <summary>Gets the direct <c>sftp://</c> URI for libssh-capable ffmpeg builds
    /// (credentials in the userinfo — never log unmasked), or <see langword="null"/>.</summary>
    public string? SftpUrl { get; }

    /// <summary>Gets the human-readable reason why no preferred input was chosen, or
    /// <see langword="null"/>.</summary>
    public string? ResolutionNote { get; }

    /// <summary>Creates a file-path input.</summary>
    public static LiveStreamInput ForFile(string filePath) => new(filePath, null);

    /// <summary>Creates a standard-input stream input.</summary>
    /// <param name="openStreamAsync">Factory that opens the media stream to pump.</param>
    /// <param name="resolutionNote">Optional reason why the seekable inputs were skipped.</param>
    public static LiveStreamInput ForStream(
        Func<CancellationToken, Task<Stream?>> openStreamAsync, string? resolutionNote = null)
        => new(null, openStreamAsync, resolutionNote: resolutionNote);

    /// <summary>Creates a range-capable HTTP URL input (internal media-stream endpoint).</summary>
    /// <param name="httpUrl">The URL handed to ffmpeg's <c>-i</c> argument.</param>
    public static LiveStreamInput ForHttp(string httpUrl) => new(null, null, httpUrl);

    /// <summary>Creates a direct <c>sftp://</c> URL input (libssh, seekable).</summary>
    /// <param name="sftpUrl">The URI handed to ffmpeg's <c>-i</c> argument (contains
    /// credentials in the userinfo).</param>
    /// <param name="resolutionNote">Optional reason why the HTTP input was skipped.</param>
    public static LiveStreamInput ForSftp(string sftpUrl, string? resolutionNote = null)
        => new(null, null, sftpUrl: sftpUrl, resolutionNote: resolutionNote);

    /// <summary>Gets a value indicating whether the input is piped rather than a file or URL.</summary>
    public bool IsPipe => OpenStreamAsync is not null;

    /// <summary>Short input-kind label for log lines and the <c>Started</c> detail.</summary>
    internal string InputKindLabel => FilePath is not null
        ? "Datei"
        : HttpUrl is not null ? "HTTP"
        : SftpUrl is not null ? "sftp" : "pipe:0";

    /// <inheritdoc />
    public override string ToString()
    {
        var maskedHttpUrl = InternalMediaStreamTokenService.MaskStreamTokenInText(HttpUrl);
        var maskedSftpUrl = SftpProbeUriBuilder.MaskCredentialsInText(SftpUrl);
        return $"LiveStreamInput {{ FilePath = {FilePath}, OpenStreamAsync = {(OpenStreamAsync is null ? "" : nameof(OpenStreamAsync))}, HttpUrl = {maskedHttpUrl}, SftpUrl = {maskedSftpUrl}, ResolutionNote = {ResolutionNote} }}";
    }
}
