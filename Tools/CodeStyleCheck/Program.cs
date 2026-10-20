using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace CodeStyleCheck;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            string root = Directory.GetCurrentDirectory();
            bool fix = false;
            bool selfTest = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root" when i + 1 < args.Length:
                        root = Path.GetFullPath(args[++i]);
                        break;
                    case "--fix":
                        fix = true;
                        break;
                    case "--self-test":
                        selfTest = true;
                        break;
                    case "--help":
                        Console.WriteLine("CodeStyleCheck [--root <Unity project>] [--fix | --self-test]");
                        return 0;
                    default:
                        throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }

            if (selfTest)
            {
                if (fix) throw new ArgumentException("--self-test cannot be combined with --fix.");
                return await FormatterTests.RunAsync(root);
            }

            // Register before JIT-loading any MSBuildWorkspace code.
            MSBuildLocator.RegisterDefaults();
            return await CheckAsync(root, fix);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"CodeStyleCheck: {exception.Message}");
            return 2;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> CheckAsync(string root, bool fix)
    {
        string solutionPath = Path.Combine(root, "3D FPS Game.sln");
        if (!File.Exists(solutionPath))
            throw new InvalidOperationException("Unity에서 프로젝트 파일을 다시 생성하세요: 3D FPS Game.sln이 없습니다.");

        using var workspace = MSBuildWorkspace.Create();
        using var registration = workspace.RegisterWorkspaceFailedHandler(
            args => Console.Error.WriteLine($"Workspace: {args.Diagnostic.Message}"));
        Solution solution = await workspace.OpenSolutionAsync(solutionPath);
        if (workspace.Diagnostics.Any(diagnostic => diagnostic.Kind == WorkspaceDiagnosticKind.Failure))
            throw new InvalidOperationException("Unity 솔루션을 완전히 읽지 못했습니다. 프로젝트 파일을 다시 생성하세요.");

        string scriptsRoot = Path.GetFullPath(Path.Combine(root, "Assets", "02.Scripts")) + Path.DirectorySeparatorChar;
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var documents = solution.Projects.SelectMany(project => project.Documents)
            .Where(document => document.FilePath is not null &&
                Path.GetFullPath(document.FilePath).StartsWith(scriptsRoot,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            .GroupBy(document => Path.GetFullPath(document.FilePath!), pathComparer)
            .OrderBy(group => group.Key, pathComparer).ToArray();

        string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
        var loadedPaths = documents.Select(group => group.Key).ToHashSet(pathComparer);
        if (files.Length == 0 || files.Any(path => !loadedPaths.Contains(Path.GetFullPath(path))))
            throw new InvalidOperationException("검사할 스크립트가 솔루션에서 누락됐습니다. Unity에서 프로젝트 파일을 다시 생성하세요.");

        // Prepare every result before --fix writes anything. Conflicting linked-file
        // configurations must not result in whichever project happens to run last winning.
        var results = new List<(string Path, SourceText Original, SourceText Expected)>();
        foreach (var group in documents)
        {
            using var stream = File.OpenRead(group.Key);
            SourceText original = SourceText.From(stream, new UTF8Encoding(false, true));
            SourceText? expected = null;
            foreach (Document document in group)
            {
                SourceText candidate = await HeaderFormatter.FormatAsync(document.WithText(original));
                if (expected is not null && !expected.ContentEquals(candidate))
                    throw new InvalidOperationException($"프로젝트별 서식 결과가 다릅니다: {group.Key}");
                expected = candidate;
            }
            results.Add((group.Key, original, expected!));
        }

        int changed = 0;
        foreach (var result in results)
        {
            if (result.Original.ContentEquals(result.Expected)) continue;
            changed++;
            if (fix)
            {
                // Preserve the original encoding (including BOM) and line endings.
                await File.WriteAllTextAsync(result.Path, result.Expected.ToString(), result.Original.Encoding!);
                Console.WriteLine($"Fixed: {Path.GetRelativePath(root, result.Path)}");
            }
            else
            {
                ReportDifference(result.Path, result.Original, result.Expected);
            }
        }

        Console.WriteLine($"CodeStyleCheck: {documents.Length} files checked, {changed} {(fix ? "fixed" : "need formatting")}.");
        return changed > 0 && !fix ? 1 : 0;
    }

    private static void ReportDifference(string path, SourceText original, SourceText expected)
    {
        int offset = 0;
        while (offset < original.Length && offset < expected.Length && original[offset] == expected[offset]) offset++;
        LinePosition position = original.Lines.GetLinePosition(offset);
        Console.Error.WriteLine($"{path}({position.Line + 1},{position.Character + 1}): error STYLEFORMAT: 서식을 수정하세요.");
        int expectedLine = expected.Lines.GetLinePosition(Math.Min(offset, expected.Length)).Line;
        Console.Error.WriteLine("Expected:");
        for (int line = expectedLine; line < Math.Min(expectedLine + 2, expected.Lines.Count); line++)
            Console.Error.WriteLine(expected.Lines[line].ToString());
        Console.Error.WriteLine("수정: dotnet run --project Tools/CodeStyleCheck -- --fix");
    }
}
