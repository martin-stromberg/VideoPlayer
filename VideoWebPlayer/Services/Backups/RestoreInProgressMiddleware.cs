using System.Net;
using System.Text.Json;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Blocks content routes while a restore job is active.
/// </summary>
public sealed class RestoreInProgressMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    /// <summary>
    /// Creates a new restore blocker middleware.
    /// </summary>
    public RestoreInProgressMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Handles the current request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, RestoreBackupJobService restoreJobs)
    {
        var snapshot = restoreJobs.GetSnapshot();
        if (!snapshot.IsActive || IsAllowedDuringRestore(context.Request.Path))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "5";

        if (IsApiRequest(context.Request))
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, CreateResponse(snapshot), JsonOptions, context.RequestAborted);
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(CreateHtmlResponse(snapshot), context.RequestAborted);
    }

    private static bool IsAllowedDuringRestore(PathString path)
    {
        return path.StartsWithSegments("/admin/backups", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/bootstrap", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/favicon.png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static RestoreInProgressResponse CreateResponse(RestoreBackupJobSnapshot snapshot)
    {
        return new RestoreInProgressResponse(
            true,
            snapshot.Status.ToString(),
            snapshot.FileName,
            snapshot.Message ?? "Eine Wiederherstellung läuft.",
            snapshot.Progress.DataSetName,
            snapshot.Progress.DataSetNumber,
            snapshot.Progress.DataSetTotal,
            snapshot.Progress.RecordNumber,
            snapshot.Progress.RecordTotal);
    }

    private static string CreateHtmlResponse(RestoreBackupJobSnapshot snapshot)
    {
        var message = WebUtility.HtmlEncode(snapshot.Message ?? "Eine Wiederherstellung läuft.");
        var dataSetName = WebUtility.HtmlEncode(snapshot.Progress.DataSetName ?? "Datenbestand");
        var dataSetProgress = snapshot.Progress.DataSetTotal > 0
            ? $"{snapshot.Progress.DataSetNumber} von {snapshot.Progress.DataSetTotal}"
            : "wird vorbereitet";
        var recordProgress = snapshot.Progress.RecordTotal > 0
            ? $"{snapshot.Progress.RecordNumber} von {snapshot.Progress.RecordTotal}"
            : "wird vorbereitet";

        return $$"""
            <!doctype html>
            <html lang="de">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta http-equiv="refresh" content="5">
                <title>Wiederherstellung läuft</title>
                <style>
                    body { margin: 0; font-family: system-ui, sans-serif; background: #111827; color: #f9fafb; display: grid; min-height: 100vh; place-items: center; }
                    main { max-width: 42rem; padding: 2rem; }
                    h1 { font-size: 1.75rem; margin: 0 0 1rem; }
                    p { color: #d1d5db; line-height: 1.5; }
                    dl { display: grid; grid-template-columns: max-content 1fr; gap: .5rem 1rem; margin-top: 1.5rem; }
                    dt { color: #9ca3af; }
                    dd { margin: 0; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Wiederherstellung läuft</h1>
                    <p>{{message}}</p>
                    <dl>
                        <dt>Datenbestand</dt><dd>{{dataSetName}}: {{dataSetProgress}}</dd>
                        <dt>Datensatz</dt><dd>{{recordProgress}}</dd>
                    </dl>
                </main>
            </body>
            </html>
            """;
    }
}

/// <summary>
/// API response returned while a restore is active.
/// </summary>
public sealed record RestoreInProgressResponse(
    bool RestoreInProgress,
    string Status,
    string? FileName,
    string Message,
    string? DataSetName,
    int DataSetNumber,
    int DataSetTotal,
    int RecordNumber,
    int RecordTotal);
