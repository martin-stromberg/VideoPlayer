using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Checks that <c>docs/API.md</c> describes every route external clients rely on. A pure file check —
/// it starts no host and touches no database; the runtime half of the contract lives in
/// <see cref="ApiDocumentationContractTests_Runtime"/>.
/// </summary>
public sealed class ApiDocumentationContractTests
{
    /// <summary>
    /// Every route <c>docs/API.md</c> must describe with its own section heading. Besides the
    /// long-standing Maui-relevant routes this covers the complete playlist area (management, entries,
    /// manual order, sort mode, genres, playback, cover) and the session endpoints of the QR bootstrap, so
    /// a route added to <c>PlaylistsController</c>/<c>AuthController</c> without documentation, or a
    /// documented route silently dropped from the document, fails the build.
    /// </summary>
    private static readonly IReadOnlyList<string> RequiredRoutes = new[]
    {
        "GET /api/health",
        "POST /api/auth/login",
        "POST /api/pairing/exchange",
        "POST /api/pairing/bootstrap",
        "POST /api/auth/refresh",
        "POST /api/auth/logout",
        "GET /api/Sources",
        "GET /api/SourceGenres/{sourceId}",
        "GET /api/items",
        "GET /api/items/recent",
        "GET /api/items/{type}/{id}",
        "GET /api/items/{type}/{id}/stream",
        "GET /api/pictures/{id}",
        "GET /api/sourceicons/{id}",
        "GET /api/favorites",
        "POST /api/favorites/toggle",
        "GET /api/continue-watching",
        "POST /api/continue-watching/progress",
        "POST /api/continue-watching/hide",
        "POST /api/continue-watching/skip",
        "GET /api/episodes/{episodeId}/background-image",
        "GET /api/playlists",
        "GET /api/playlists/public",
        "PUT /api/playlists/{id}/public",
        "GET /api/playlists/{id}",
        "POST /api/playlists",
        "PUT /api/playlists/{id}",
        "DELETE /api/playlists/{id}",
        "POST /api/playlists/{id}/entries",
        "DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}",
        "GET /api/playlists/{id}/entries",
        "GET /api/playlists/{id}/entries/paged",
        "PUT /api/playlists/{id}/entries/{entryId}/order",
        "POST /api/playlists/{id}/entries/batch-reorder",
        "GET /api/playlists/{id}/entries/max-sort-order",
        "POST /api/playlists/{id}/entries/{entryId}/move-to-beginning",
        "POST /api/playlists/{id}/entries/{entryId}/move-between",
        "PATCH /api/playlists/{id}/sort-mode",
        "PUT /api/playlists/{id}/genres",
        "POST /api/playlists/{id}/genres/reset",
        "POST /api/playlists/{id}/play",
        "POST /api/playlists/{id}/play/next",
        "POST /api/playlists/{id}/play/previous",
        "POST /api/playlists/{id}/play/advance",
        "POST /api/playlists/{id}/cover/upload",
        "POST /api/playlists/{id}/cover/regenerate",
        "POST /api/playlists/{id}/cover/preview",
        "GET /api/playlists/{id}/cover",
        "DELETE /api/playlists/{id}/cover",
        "GET /hubs/mediaupdate"
    };

    [Fact]
    public void ApiDocumentationContainsMauiRelevantRoutes()
    {
        var undocumented = FindUndocumentedRoutes(ReadApiDocument());

        Assert.True(
            undocumented.Count == 0,
            $"docs/API.md beschreibt {undocumented.Count} Route(n) nicht (erwartet je eine Überschrift "
            + $"\"### <Verb> <Pfad>\"): {string.Join(", ", undocumented)}.");
    }

    /// <summary>
    /// Counter-proof for <see cref="ApiDocumentationContainsMauiRelevantRoutes"/>: removing exactly one
    /// route's section heading must make the check report exactly that route. A mere substring check would
    /// stay green here for every route that is a prefix of another one (e.g. <c>GET /api/playlists</c>,
    /// which <c>GET /api/playlists/public</c> also contains), so this pins the heading-anchored comparison.
    /// </summary>
    [Fact]
    public void ApiDocumentation_WithARequiredRouteHeadingRemoved_ReportsExactlyThatRoute()
    {
        var apiDocument = ReadApiDocument();

        foreach (var route in RequiredRoutes)
        {
            var mutilatedDocument = RemoveHeadingOf(apiDocument, route);
            Assert.NotEqual(apiDocument, mutilatedDocument);

            var undocumented = FindUndocumentedRoutes(mutilatedDocument);

            Assert.Equal(new[] { route }, undocumented);
        }
    }

    /// <summary>
    /// Returns the routes of <see cref="RequiredRoutes"/> that the given document does not describe with a
    /// section heading of the form <c>### &lt;Verb&gt; &lt;Pfad&gt;</c>. The comparison is anchored to the
    /// whole (trimmed) line rather than done as a substring search, so a route that is a prefix of another
    /// one is not considered documented just because the longer route is.
    /// </summary>
    /// <param name="apiDocument">The content of <c>docs/API.md</c> to check.</param>
    /// <returns>The undocumented routes, in the order of <see cref="RequiredRoutes"/>.</returns>
    private static IReadOnlyList<string> FindUndocumentedRoutes(string apiDocument)
    {
        var headings = apiDocument
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("### ", StringComparison.Ordinal))
            .Select(line => line["### ".Length..].Trim())
            .ToHashSet(StringComparer.Ordinal);

        return RequiredRoutes.Where(route => !headings.Contains(route)).ToList();
    }

    /// <summary>
    /// Removes the single section heading line that documents the given route, leaving the rest of the
    /// document (including every other route's heading and any in-text mention of this route) untouched.
    /// </summary>
    /// <param name="apiDocument">The content of <c>docs/API.md</c>.</param>
    /// <param name="route">The route whose heading line is removed.</param>
    /// <returns>The document without that heading line.</returns>
    private static string RemoveHeadingOf(string apiDocument, string route)
    {
        var remainingLines = apiDocument
            .Split('\n')
            .Where(line => !string.Equals(line.Trim(), $"### {route}", StringComparison.Ordinal))
            .ToList();

        return string.Join('\n', remainingLines);
    }

    /// <summary>
    /// Reads <c>docs/API.md</c> from the repository the test assembly was built in.
    /// </summary>
    /// <returns>The document's content.</returns>
    private static string ReadApiDocument()
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "API.md"));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VideoPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
