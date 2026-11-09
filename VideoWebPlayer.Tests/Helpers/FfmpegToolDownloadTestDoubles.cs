using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Serves a fixed response for the <c>"FfmpegTools"</c>-HttpClient in
/// <see cref="FfmpegToolDownloadService"/> tests: either the configured package bytes or an
/// HTTP error status.
/// </summary>
internal sealed class BytesHandler : HttpMessageHandler
{
    private readonly byte[] _payload;
    private readonly HttpStatusCode _status;

    public BytesHandler(byte[] payload, HttpStatusCode status = HttpStatusCode.OK)
    {
        _payload = payload;
        _status = status;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_status);
        if (_status == HttpStatusCode.OK)
            response.Content = new ByteArrayContent(_payload);
        return Task.FromResult(response);
    }
}

/// <summary>
/// Serves an <see cref="HttpContent"/> produced by a factory — used for the progress test
/// where the content must be a <see cref="StreamContent"/> over a throttled stream (only
/// stream content is returned to the caller without upfront buffering).
/// </summary>
internal sealed class ContentFactoryHandler : HttpMessageHandler
{
    private readonly Func<HttpContent> _contentFactory;

    public ContentFactoryHandler(Func<HttpContent> contentFactory) => _contentFactory = contentFactory;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = _contentFactory() });
}

/// <summary>
/// Read-only stream that yields at most <c>chunkSize</c> bytes per read with a small delay
/// and reports every read to a callback — lets the progress test observe growing
/// <c>BytesReceived</c> values mid-download.
/// </summary>
internal sealed class ThrottledStream : Stream
{
    private readonly byte[] _data;
    private readonly int _chunkSize;
    private readonly int _delayMilliseconds;
    private readonly Action? _onChunk;
    private int _position;

    public ThrottledStream(byte[] data, int chunkSize, int delayMilliseconds, Action? onChunk = null)
    {
        _data = data;
        _chunkSize = chunkSize;
        _delayMilliseconds = delayMilliseconds;
        _onChunk = onChunk;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _data.Length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_position >= _data.Length)
            return 0;

        await Task.Delay(_delayMilliseconds, cancellationToken);
        var n = Math.Min(_chunkSize, Math.Min(count, _data.Length - _position));
        Array.Copy(_data, _position, buffer, offset, n);
        _position += n;
        _onChunk?.Invoke();
        return n;
    }
}

/// <summary>
/// Handler that fails the request with the exception produced by the factory — simulates
/// transport-level failures (DNS, refused connection, …) for the error-mapping tests.
/// </summary>
internal sealed class ExceptionHandler : HttpMessageHandler
{
    private readonly Func<Exception> _errorFactory;

    public ExceptionHandler(Func<Exception> errorFactory) => _errorFactory = errorFactory;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromException<HttpResponseMessage>(_errorFactory());
}

/// <summary>
/// Read-only stream that yields its <c>prefix</c> bytes and then never answers again: the
/// simulated server keeps the connection open but sends nothing — reads only end through
/// the caller's cancellation token (the job's stall watchdog).
/// </summary>
internal sealed class StalledStream : Stream
{
    private readonly byte[] _prefix;
    private int _position;

    public StalledStream(byte[] prefix) => _prefix = prefix;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _prefix.Length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_position < _prefix.Length)
        {
            var n = Math.Min(count, _prefix.Length - _position);
            Array.Copy(_prefix, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }
}

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> stub returning a fresh <see cref="HttpClient"/>
/// over the same handler on every call.
/// </summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

/// <summary>
/// Scope factory stub: the produced scope resolves the shared <see cref="ApplicationDbContext"/>
/// instance the <see cref="FfmpegToolDownloadService"/> needs for its admin check.
/// </summary>
internal sealed class StubScopeFactory : IServiceScopeFactory
{
    private readonly ApplicationDbContext _db;

    public StubScopeFactory(ApplicationDbContext db) => _db = db;

    public IServiceScope CreateScope() => new StubScope(new StubServiceProvider(_db));

    private sealed class StubScope : IServiceScope
    {
        public StubScope(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;
        public IServiceProvider ServiceProvider { get; }
        public void Dispose() { }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly ApplicationDbContext _db;
        public StubServiceProvider(ApplicationDbContext db) => _db = db;
        public object? GetService(Type serviceType)
            => serviceType == typeof(ApplicationDbContext) ? _db : null;
    }
}

/// <summary>Locator stub that never resolves — simulates a missing binary.</summary>
internal sealed class AbsentFfprobeLocator : IFfprobeLocator
{
    public string? Resolve() => null;
}

/// <summary>Locator stub that never resolves — simulates a missing binary.</summary>
internal sealed class AbsentFfmpegLocator : IFfmpegLocator
{
    public string? Resolve() => null;
}

/// <summary>Locator stub that always resolves — simulates an installed ffmpeg.</summary>
internal sealed class FakeFfmpegLocator : IFfmpegLocator
{
    public string? Resolve() => "ffmpeg";
}

/// <summary>
/// Locator stub whose result can be switched mid-test — simulates a binary that appears
/// while the page is already open (e.g. a download finished by another admin/tab).
/// </summary>
internal sealed class SettableFfprobeLocator : IFfprobeLocator
{
    private string? _path;

    public SettableFfprobeLocator(string? path = null) => _path = path;

    public void SetPath(string? path) => _path = path;

    public string? Resolve() => _path;
}

/// <summary>
/// Configurable <see cref="IFfmpegToolDownloadService"/> double for component tests:
/// returns a preset snapshot and records the user id passed to
/// <see cref="IFfmpegToolDownloadService.StartDownloadAsync"/>.
/// </summary>
internal sealed class FakeFfmpegToolDownloadService : IFfmpegToolDownloadService
{
    private FfmpegToolDownloadJobSnapshot _snapshot;

    public FakeFfmpegToolDownloadService(FfmpegToolDownloadJobSnapshot? snapshot = null)
        => _snapshot = snapshot ?? FfmpegToolDownloadJobSnapshot.Idle;

    public string? ReceivedUserId { get; private set; }
    public int StartCalls { get; private set; }

    /// <summary>Every snapshot id passed to <see cref="AcknowledgeTerminalState"/>.</summary>
    public List<Guid> AcknowledgedIds { get; } = [];

    /// <summary>Lets a test move the snapshot forward (e.g. to Failed) before rendering.</summary>
    public void SetSnapshot(FfmpegToolDownloadJobSnapshot snapshot) => _snapshot = snapshot;

    public FfmpegToolDownloadJobSnapshot GetSnapshot() => _snapshot;

    public void AcknowledgeTerminalState(Guid snapshotId)
    {
        AcknowledgedIds.Add(snapshotId);
        if (!_snapshot.IsActive && _snapshot.Id == snapshotId)
            _snapshot = FfmpegToolDownloadJobSnapshot.Idle;
    }

    public Task<FfmpegToolDownloadStartResult> StartDownloadAsync(string userId, CancellationToken cancellationToken = default)
    {
        ReceivedUserId = userId;
        StartCalls++;
        if (_snapshot.IsActive)
            return Task.FromResult(new FfmpegToolDownloadStartResult(false, _snapshot));

        _snapshot = new FfmpegToolDownloadJobSnapshot(
            Guid.NewGuid(), FfmpegToolDownloadJobStatus.Queued, DateTimeOffset.UtcNow, null,
            "Download wurde gestartet.", null, 0, null);
        return Task.FromResult(new FfmpegToolDownloadStartResult(true, _snapshot));
    }
}

/// <summary>
/// Shared helpers for <see cref="FfmpegToolDownloadService"/> tests: service construction
/// with an injectable base directory and platform override, snapshot waiting and small
/// archive fixtures.
/// </summary>
internal static class FfmpegToolDownloadTestSupport
{
    public static string NewTempBaseDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vwp-ffmpegdl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static FfmpegToolDownloadService CreateService(
        ApplicationDbContext db,
        HttpMessageHandler handler,
        string baseDirectory,
        Configuration.MediaProbeOptions? options = null,
        Func<(string Rid, string ArchiveKind)?>? platformSelector = null,
        TimeSpan? downloadStallTimeout = null,
        long? maxExtractedEntryBytes = null)
        => new(
            new StubScopeFactory(db),
            new StubHttpClientFactory(handler),
            Options.Create(options ?? new Configuration.MediaProbeOptions()),
            NullLogger<FfmpegToolDownloadService>.Instance,
            baseDirectory,
            platformSelector,
            downloadStallTimeout,
            maxExtractedEntryBytes);

    /// <summary>
    /// Waits until the current job leaves the active state (or the timeout expires) and
    /// returns the last snapshot.
    /// </summary>
    public static async Task<FfmpegToolDownloadJobSnapshot> AwaitCompletionAsync(
        FfmpegToolDownloadService service, int timeoutMilliseconds = 30_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        FfmpegToolDownloadJobSnapshot snapshot;
        do
        {
            snapshot = service.GetSnapshot();
            if (!snapshot.IsActive)
                return snapshot;
            await Task.Delay(25);
        } while (DateTime.UtcNow < deadline);
        return service.GetSnapshot();
    }

    /// <summary>
    /// Builds an in-memory zip archive from (entry name, content) pairs — mirrors the
    /// gyan.dev package layout (<c>…/bin/ffmpeg.exe</c>) but also usable for malicious
    /// entry names.
    /// </summary>
    public static byte[] CreateZipBytes(params (string Name, string Content)[] entries)
        => CreateZipBytes(CompressionLevel.Optimal, entries);

    /// <summary>
    /// <paramref name="level"/> steuert die Zip-Kompression — <see cref="CompressionLevel.NoCompression"/>
    /// liefert vorhersehbare Payload-Groessen fuer Fortschritts-/Chunking-Tests (gleichfoermiger
    /// Inhalt wuerde sonst auf wenige hundert Bytes schrumpfen).
    /// </summary>
    public static byte[] CreateZipBytes(CompressionLevel level, params (string Name, string Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, level);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return memory.ToArray();
    }

    /// <summary>
    /// Locates the checked-in <c>TestData</c> directory by walking up from the test
    /// assembly's base directory (pattern from <c>JpegIntegrityCheckerTests</c>).
    /// </summary>
    public static string FindTestDataDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "VideoWebPlayer.Tests", "TestData");
            if (Directory.Exists(candidate))
                return candidate;
            candidate = Path.Combine(directory.FullName, "TestData");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("TestData-Verzeichnis nicht gefunden.");
    }

    public static string FixturePath(string fileName)
        => Path.Combine(FindTestDataDirectory(), "FfmpegPackages", fileName);
}
