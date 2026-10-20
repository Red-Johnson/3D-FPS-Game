using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeStyleCheck;

// A dependency-free regression suite; run with --self-test.
internal static class FormatterTests
{
    internal static async Task<int> RunAsync(string root)
    {
        string editorConfig = await File.ReadAllTextAsync(Path.Combine(root, ".editorconfig"));
        int passed = 0;
        foreach (string newline in new[] { "\n", "\r\n" })
        {
            string Wrap(string field) => ("using UnityEngine;\n\npublic class Example : MonoBehaviour\n{\n" + field + "\n}\n")
                .Replace("\n", newline);
            string basic = Wrap("    [SerializeField, Min(0f)] private float _jumpPower = 10f;");
            string desired = Wrap("    [Header(\"점프 설정\")] [SerializeField, Min(0f)]\n    private float _jumpPower = 10f;");
            string inline = Wrap("    [Header(\"점프 설정\")] [SerializeField, Min(0f)] private float _jumpPower = 10f;");
            var cases = new (string Name, string Input, string Expected)[]
            {
                ("existing inline field", basic, basic),
                ("desired Header layout", desired, desired),
                ("single-line class", ("using UnityEngine;\n\npublic class Example : MonoBehaviour { [Header(\"점프 설정\")] [SerializeField, Min(0f)] private float _jumpPower = 10f; }\n")
                    .Replace("\n", newline), inline),
                ("missing attribute separator is allowed", desired.Replace(")] [SerializeField", ")][SerializeField"),
                    desired.Replace(")] [SerializeField", ")][SerializeField")),
                ("extra attribute separator is allowed", desired.Replace(")] [SerializeField", ")]   [SerializeField"),
                    desired.Replace(")] [SerializeField", ")]   [SerializeField")),
                ("separate attribute lines", desired.Replace(")] [SerializeField", ")]" + newline + "    [SerializeField"),
                    desired.Replace(")] [SerializeField", ")]" + newline + "    [SerializeField")),
                ("Rider inline declaration", inline, inline),
                ("inline Header still checks declaration spacing", inline.Replace(" = ", "="), inline),
                ("inline Header still checks indentation", inline.Replace("    [Header", " [Header"), inline),
                ("inline Header still checks attribute arguments", inline.Replace("Min(0f)", "Min( 0f )"), inline),
                ("multiline attribute indentation still checked", desired.Replace(")] [SerializeField", ")]" + newline + " [SerializeField"),
                    desired.Replace(")] [SerializeField", ")]" + newline + "    [SerializeField")),
                ("bad indentation and operators", desired.Replace("    [Header", " [Header")
                    .Replace("    private", " private").Replace(" = ", "="), desired),
                ("fully qualified Header", desired.Replace("[Header(", "[UnityEngine.Header("),
                    desired.Replace("[Header(", "[UnityEngine.Header(")),
                ("HeaderAttribute suffix", desired.Replace("[Header(", "[HeaderAttribute("),
                    desired.Replace("[Header(", "[HeaderAttribute(")),
                ("only Header", Wrap("    [Header(\"점프\")] private float _jumpPower = 10f;"),
                    Wrap("    [Header(\"점프\")] private float _jumpPower = 10f;")),
                ("multiple attribute lists", desired.Replace("[SerializeField, Min(0f)]", "[SerializeField] [Min(0f)]"),
                    desired.Replace("[SerializeField, Min(0f)]", "[SerializeField] [Min(0f)]")),
                ("do not change attribute order", Wrap("    [SerializeField] [Header(\"점프\")] private float _jumpPower = 10f;"),
                    Wrap("    [SerializeField] [Header(\"점프\")] private float _jumpPower = 10f;")),
                ("stat group from Rider", Wrap("    [Header(\"스탯\")] [SerializeField] private float _moveSpeed = 7f;\n" +
                    "    [SerializeField] private float _sprintSpeed = 12f;\n" +
                    "    [SerializeField] private float _jumpPower = 10f;\n" +
                    "    [SerializeField] private float _staminaRecovery = 15f;\n" +
                    "    [SerializeField] private float _sprintCost = 10f;\n" +
                    "    [SerializeField] private float _jumpCost = 20f;"),
                    Wrap("    [Header(\"스탯\")] [SerializeField] private float _moveSpeed = 7f;\n" +
                    "    [SerializeField] private float _sprintSpeed = 12f;\n" +
                    "    [SerializeField] private float _jumpPower = 10f;\n" +
                    "    [SerializeField] private float _staminaRecovery = 15f;\n" +
                    "    [SerializeField] private float _sprintCost = 10f;\n" +
                    "    [SerializeField] private float _jumpCost = 20f;")),
                ("comment before field", desired.Replace("    [Header", "    // 점프 값은 유지한다.\n    [Header".Replace("\n", newline)),
                    desired.Replace("    [Header", "    // 점프 값은 유지한다.\n    [Header".Replace("\n", newline))),
                ("comment between attributes", Wrap("    [Header(\"점프\")]\n    // Keep this comment.\n    [SerializeField, Min(0f)]\n    private float _jumpPower = 10f;"),
                    Wrap("    [Header(\"점프\")]\n    // Keep this comment.\n    [SerializeField, Min(0f)]\n    private float _jumpPower = 10f;")),
                ("comment before declaration", Wrap("    [Header(\"점프\")] [SerializeField]\n    // Keep this too.\n    private float _jumpPower = 10f;"),
                    Wrap("    [Header(\"점프\")] [SerializeField]\n    // Keep this too.\n    private float _jumpPower = 10f;")),
                ("attribute argument text", Wrap("    [Header(\"한글 ] [ \\\"제목\\\"\")] [SerializeField]\n    private string _text = \"a ] [ b\";"),
                    Wrap("    [Header(\"한글 ] [ \\\"제목\\\"\")] [SerializeField]\n    private string _text = \"a ] [ b\";")),
                ("non-Header attributes unchanged", Wrap("    [Tooltip(\"점프\")]\n    [SerializeField]\n    private float _jumpPower = 10f;"),
                    Wrap("    [Tooltip(\"점프\")]\n    [SerializeField]\n    private float _jumpPower = 10f;")),
                ("Header on a property unchanged", Wrap("    [Header(\"점프\")]\n    [SerializeField]\n    public float JumpPower { get; set; }"),
                    Wrap("    [Header(\"점프\")]\n    [SerializeField]\n    public float JumpPower { get; set; }")),
            };

            foreach (var test in cases)
            {
                string actual = await FormatAsync(test.Input, editorConfig, root);
                AssertEqual(test.Expected, actual, test.Name);
                AssertEqual(actual, await FormatAsync(actual, editorConfig, root), test.Name + " (idempotent)");
                // Formatting must preserve every token, comment, and directive.
                AssertEqual(SignificantContent(test.Input), SignificantContent(actual), test.Name + " (content)");
                passed++;
            }

            string conditional = desired.Replace("    [Header", "#if UNITY_EDITOR" + newline + "    [Header")
                .Replace(newline + "}", newline + "#endif" + newline + "}");
            AssertEqual(conditional, await FormatAsync(conditional, editorConfig, root), "Unity preprocessor symbols");
            passed++;

            string configWithTabs = editorConfig + "\nindent_style = tab\nindent_size = 4\n";
            string tabbed = desired.Replace("    ", "\t");
            AssertEqual(tabbed, await FormatAsync(desired, configWithTabs, root), "EditorConfig indentation");
            passed++;

            string configWithEol = editorConfig + "\nend_of_line = lf\n";
            AssertEqual(desired.Replace("\r\n", "\n"), await FormatAsync(desired, configWithEol, root), "EditorConfig line endings");
            passed++;
        }

        Console.WriteLine($"CodeStyleCheck self-test: {passed} cases passed (including idempotence and content preservation).");
        return 0;
    }

    private static async Task<string> FormatAsync(string input, string config, string root)
    {
        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        Solution solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(projectId,
                VersionStamp.Create(), "Tests", "Tests", LanguageNames.CSharp,
                filePath: Path.Combine(root, "FormatterTests.csproj"),
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { "UNITY_EDITOR" })))
            .AddAnalyzerConfigDocument(DocumentId.CreateNewId(projectId), ".editorconfig",
                SourceText.From(config), filePath: Path.Combine(root, ".editorconfig"));
        DocumentId documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, "Example.cs", SourceText.From(input, new UTF8Encoding(false)),
            filePath: Path.Combine(root, "Assets", "02.Scripts", "Example.cs"));
        return (await HeaderFormatter.FormatAsync(solution.GetDocument(documentId)!)).ToString();
    }

    private static string SignificantContent(string source)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(source,
            new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { "UNITY_EDITOR" })).GetRoot();
        return string.Join("\n", root.DescendantTokens().Select(token => token.Text)) + "\n" +
            string.Join("\n", root.DescendantTrivia().Where(trivia =>
                !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)).Select(trivia => trivia.ToString()));
    }

    private static void AssertEqual(string expected, string actual, string name)
    {
        if (expected != actual)
            throw new InvalidOperationException($"Self-test failed: {name}\nExpected:\n{expected}\nActual:\n{actual}");
    }
}
