using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoWebPlayer.Services;

/// <summary>
/// Probes the playback duration of a single media file via an external ffprobe process.
/// </summary>
public interface IMediaDurationProbe
{
    /// <summary>
    /// Probes the duration of a file that is directly accessible on the local file system.
    /// </summary>
    /// <param name="filePath">Absolute path of the media file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The probe outcome; <c>null</c> duration marks a failed attempt.</returns>
    Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Probes the duration of a media file supplied as a stream (e.g. for remote SFTP sources);
    /// the stream is piped into ffprobe's standard input.
    /// </summary>
    /// <param name="input">Readable stream containing the media file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The probe outcome; <c>null</c> duration marks a failed attempt.</returns>
    Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken);
}
