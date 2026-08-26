using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

var repoRoot = GetArgumentValue(args, "--root") ?? Directory.GetCurrentDirectory();
repoRoot = Path.GetFullPath(repoRoot);

var scanner = new SecretScanner(repoRoot);
return scanner.Run();

static string? GetArgumentValue(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name)
        {
            return args[i + 1];
        }
    }

    return null;
}

internal sealed partial class SecretScanner(string repoRoot)
{
    private const string Redaction = "<redacted-github-token>";

    public int Run()
    {
        var findings = new List<string>();
        findings.AddRange(ScanRemotes());
        findings.AddRange(ScanStagedFiles());

        if (findings.Count == 0)
        {
            Console.WriteLine("Secret scan passed.");
            return 0;
        }

        Console.Error.WriteLine("Secret scan failed: possible GitHub tokens were found.");
        foreach (var finding in findings)
        {
            Console.Error.WriteLine(finding);
        }

        Console.Error.WriteLine("Remove the token, rotate it in GitHub, then commit again.");
        return 1;
    }

    private IEnumerable<string> ScanRemotes()
    {
        var remotes = RunGitText("remote", "-v");
        foreach (var line in ReadLines(remotes.StandardOutput))
        {
            if (GitHubTokenRegex().IsMatch(line))
            {
                yield return $"git remote: {Redact(line)}";
            }
        }
    }

    private IEnumerable<string> ScanStagedFiles()
    {
        var stagedFiles = RunGitBytes("diff", "--cached", "--name-only", "-z", "--diff-filter=ACMR");
        foreach (var stagedFile in SplitNullTerminatedUtf8(stagedFiles.StandardOutput))
        {
            var stagedContent = RunGitBytes("show", $":{stagedFile}");
            if (stagedContent.ExitCode != 0)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(stagedContent.StandardOutput);
            var lineNumber = 0;
            foreach (var line in ReadLines(text))
            {
                lineNumber++;
                if (GitHubTokenRegex().IsMatch(line))
                {
                    yield return $"{stagedFile}:{lineNumber}: {Redact(line)}";
                }
            }
        }
    }

    private static IEnumerable<string> ReadLines(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<string> SplitNullTerminatedUtf8(byte[] bytes)
    {
        var start = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != 0)
            {
                continue;
            }

            if (i > start)
            {
                yield return Encoding.UTF8.GetString(bytes, start, i - start);
            }

            start = i + 1;
        }
    }

    private static string Redact(string value)
    {
        return GitHubTokenRegex().Replace(value, Redaction);
    }

    private GitResult<string> RunGitText(params string[] arguments)
    {
        var result = RunGit(arguments);
        return new GitResult<string>(
            result.ExitCode,
            Encoding.UTF8.GetString(result.StandardOutput),
            Encoding.UTF8.GetString(result.StandardError));
    }

    private GitResult<byte[]> RunGitBytes(params string[] arguments)
    {
        return RunGit(arguments);
    }

    private GitResult<byte[]> RunGit(params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.WorkingDirectory = repoRoot;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stdout);
        process.StandardError.BaseStream.CopyTo(stderr);
        process.WaitForExit();

        return new GitResult<byte[]>(process.ExitCode, stdout.ToArray(), stderr.ToArray());
    }

    [GeneratedRegex(@"(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,}|github_pat_[A-Za-z0-9_]+", RegexOptions.Compiled)]
    private static partial Regex GitHubTokenRegex();
}

internal sealed record GitResult<T>(int ExitCode, T StandardOutput, T StandardError);
