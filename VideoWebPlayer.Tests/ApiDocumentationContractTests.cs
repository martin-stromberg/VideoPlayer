using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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
    /// The routes <c>docs/API.md</c> must describe with their own section heading: the long-standing
    /// Maui-relevant routes, the complete playlist area (management, entries, manual order, sort mode,
    /// genres, playback, cover) and the session endpoints of the QR bootstrap. This list is maintained by
    /// hand and only guards one direction — a route that stands here and whose section heading disappears
    /// from the document fails the build. That a <b>new</b> controller route cannot slip past
    /// unnoticed is not covered by this list but by
    /// <see cref="ApiDocumentation_DescribesEveryRouteOfThePlaylistsController"/> and
    /// <see cref="RequiredRoutes_ContainEveryRouteOfThePlaylistsController"/>, which read the routes of
    /// <see cref="PlaylistsController"/> straight off its attributes. Other controllers have no such
    /// check, because <c>docs/API.md</c> deliberately does not describe all of their routes (see its
    /// section "Admininterne Endpunkte", e.g. <c>POST /api/auth/impersonate</c>).
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
    /// Closes the gap the hand-maintained <see cref="RequiredRoutes"/> leaves open in the other direction:
    /// every route <see cref="PlaylistsController"/> actually exposes — read straight off its
    /// <see cref="RouteAttribute"/> and <see cref="HttpMethodAttribute"/>s — must have its own section in
    /// <c>docs/API.md</c>. A route added to that controller without documentation therefore fails the
    /// build, which is the core promise of A2 ("<c>docs/API.md</c> describes every playlist endpoint").
    /// </summary>
    [Fact]
    public void ApiDocumentation_DescribesEveryRouteOfThePlaylistsController()
    {
        var documentedHeadings = ReadRouteHeadings(ReadApiDocument());

        var undocumented = GetPlaylistsControllerRoutes()
            .Where(route => !documentedHeadings.Contains(route))
            .ToList();

        Assert.True(
            undocumented.Count == 0,
            $"docs/API.md beschreibt {undocumented.Count} Route(n) des PlaylistsController nicht "
            + $"(erwartet je eine Überschrift \"### <Verb> <Pfad>\"): {string.Join(", ", undocumented)}.");
    }

    /// <summary>
    /// Keeps <see cref="RequiredRoutes"/> itself honest: every route of <see cref="PlaylistsController"/>
    /// must also be listed there, so the counter-proof
    /// <see cref="ApiDocumentation_WithARequiredRouteHeadingRemoved_ReportsExactlyThatRoute"/> covers it too.
    /// </summary>
    [Fact]
    public void RequiredRoutes_ContainEveryRouteOfThePlaylistsController()
    {
        var missing = GetPlaylistsControllerRoutes()
            .Where(route => !RequiredRoutes.Contains(route, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} Route(n) des PlaylistsController fehlen in RequiredRoutes: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// Reads the routes of <see cref="PlaylistsController"/> from its attributes, in the notation the
    /// section headings of <c>docs/API.md</c> use (e.g. <c>GET /api/playlists/{id}/entries</c>).
    /// </summary>
    /// <returns>The controller's routes.</returns>
    private static IReadOnlyList<string> GetPlaylistsControllerRoutes()
    {
        var controller = typeof(PlaylistsController);
        var prefix = controller.GetCustomAttributes<RouteAttribute>().Single().Template.Trim('/');

        var routes = new List<string>();
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in method.GetCustomAttributes<HttpMethodAttribute>())
            {
                var verb = Assert.Single(attribute.HttpMethods);
                var path = string.IsNullOrEmpty(attribute.Template) ? prefix : $"{prefix}/{attribute.Template.Trim('/')}";
                routes.Add($"{verb} /{path}");
            }
        }

        Assert.NotEmpty(routes);
        return routes;
    }

    /// <summary>
    /// Returns the routes of <see cref="RequiredRoutes"/> that the given document does not describe with a
    /// section heading of the form <c>### &lt;Verb&gt; &lt;Pfad&gt;</c>.
    /// </summary>
    /// <param name="apiDocument">The content of <c>docs/API.md</c> to check.</param>
    /// <returns>The undocumented routes, in the order of <see cref="RequiredRoutes"/>.</returns>
    private static IReadOnlyList<string> FindUndocumentedRoutes(string apiDocument)
    {
        var headings = ReadRouteHeadings(apiDocument);

        return RequiredRoutes.Where(route => !headings.Contains(route)).ToList();
    }

    /// <summary>
    /// Reads the route of every <c>### </c> section heading of the document. The comparison is anchored to
    /// the whole (trimmed) line rather than done as a substring search, so a route that is a prefix of
    /// another one is not considered documented just because the longer route is.
    /// </summary>
    /// <param name="apiDocument">The content of <c>docs/API.md</c> to read.</param>
    /// <returns>The routes the document documents with their own section.</returns>
    private static HashSet<string> ReadRouteHeadings(string apiDocument)
        => apiDocument
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("### ", StringComparison.Ordinal))
            .Select(line => line["### ".Length..].Trim())
            .ToHashSet(StringComparer.Ordinal);

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
