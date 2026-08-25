using System.Text;
using System.Text.RegularExpressions;

namespace VideoWebPlayer.Tools.MarkdownLinkCheck;

public static partial class Program
{
    public static int Main(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--root" && i + 1 < args.Length)
            {
                root = args[++i];
            }
        }

        var result = MarkdownLinkChecker.Check(root);
        foreach (var diagnostic in result.Diagnostics)
        {
            Console.WriteLine(diagnostic);
        }

        if (result.Errors.Count == 0)
        {
            Console.WriteLine($"Markdown link check passed ({result.CheckedFiles} files, {result.CheckedLinks} local links).");
            return 0;
        }

        Console.Error.WriteLine($"Markdown link check failed ({result.Errors.Count} invalid local links).");
        return 1;
    }
}

public sealed record LinkCheckResult(int CheckedFiles, int CheckedLinks, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Errors);

public static partial class MarkdownLinkChecker
{
    private static readonly string[] IgnoredDirectories =
    [
        ".git",
        ".vs",
        "bin",
        "obj",
        "artifacts",
        "Sub-Repository"
    ];

    public static LinkCheckResult Check(string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        var diagnostics = new List<string>();
        var errors = new List<string>();
        var checkedFiles = 0;
        var checkedLinks = 0;

        foreach (var markdownFile in EnumerateMarkdownFiles(root))
        {
            checkedFiles++;
            foreach (var link in ExtractLinks(markdownFile))
            {
                if (!TryCreateLocalTarget(link.Target, out var targetPath, out var fragment))
                {
                    continue;
                }

                checkedLinks++;
                var baseDirectory = Path.GetDirectoryName(markdownFile) ?? root;
                var candidate = Path.IsPathRooted(targetPath)
                    ? Path.Combine(root, targetPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    : Path.Combine(baseDirectory, targetPath);
                var fullPath = Path.GetFullPath(candidate);

                if (!IsInside(root, fullPath))
                {
                    AddError("target leaves repository");
                    continue;
                }

                if (!ExistsWithExactCasing(fullPath, out var existingPath))
                {
                    AddError("missing file or casing mismatch");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fragment) && File.Exists(existingPath) && !FragmentExists(existingPath, fragment))
                {
                    AddError($"missing fragment #{fragment}");
                }

                void AddError(string reason)
                {
                    var relativeFile = Path.GetRelativePath(root, markdownFile).Replace('\\', '/');
                    var relativeTarget = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
                    var message = $"{relativeFile}:{link.Line}: {reason}: {link.Target} -> {relativeTarget}";
                    diagnostics.Add(message);
                    errors.Add(message);
                }
            }
        }

        return new LinkCheckResult(checkedFiles, checkedLinks, diagnostics, errors);
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                var name = Path.GetFileName(directory);
                if (!IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(directory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(current, "*.md"))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<MarkdownLink> ExtractLinks(string markdownFile)
    {
        var inFence = false;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(markdownFile))
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            foreach (Match match in InlineLinkRegex().Matches(line))
            {
                yield return new MarkdownLink(lineNumber, match.Groups["target"].Value.Trim());
            }
        }
    }

    private static bool TryCreateLocalTarget(string rawTarget, out string path, out string? fragment)
    {
        path = string.Empty;
        fragment = null;
        var target = rawTarget.Trim().Trim('<', '>');
        if (string.IsNullOrWhiteSpace(target) || target.StartsWith('#'))
        {
            return false;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Scheme))
        {
            return false;
        }

        var hashIndex = target.IndexOf('#');
        if (hashIndex >= 0)
        {
            fragment = Uri.UnescapeDataString(target[(hashIndex + 1)..]);
            target = target[..hashIndex];
        }

        path = Uri.UnescapeDataString(target).Replace('/', Path.DirectorySeparatorChar);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool ExistsWithExactCasing(string fullPath, out string existingPath)
    {
        existingPath = fullPath;
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        var current = root;
        var segments = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (!Directory.Exists(current))
            {
                return false;
            }

            var match = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.Ordinal));
            if (match is null)
            {
                return false;
            }

            current = match;
        }

        existingPath = current;
        return File.Exists(current) || Directory.Exists(current);
    }

    private static bool FragmentExists(string filePath, string fragment)
    {
        var anchors = File.ReadLines(filePath)
            .Select(line => HeadingRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => ToGitHubAnchor(match.Groups["heading"].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return anchors.Contains(ToGitHubAnchor(fragment));
    }

    private static string ToGitHubAnchor(string value)
    {
        var builder = new StringBuilder();
        var previousWasDash = false;
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
            {
                builder.Append(c);
                previousWasDash = false;
            }
            else if (char.IsWhiteSpace(c) && !previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static bool IsInside(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex InlineLinkRegex();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(?<heading>.+?)\s*#*\s*$", RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    private sealed record MarkdownLink(int Line, string Target);
}
