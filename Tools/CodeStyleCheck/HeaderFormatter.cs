using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace CodeStyleCheck;

internal static class HeaderFormatter
{
    internal static async Task<SourceText> FormatAsync(Document document)
    {
        SourceText original = await document.GetTextAsync();
        SyntaxNode syntax = (await document.GetSyntaxRootAsync())!;
        if (syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new InvalidOperationException($"C# 구문 오류를 먼저 수정하세요: {document.FilePath}");

        OptionSet options = await document.GetOptionsAsync();
        var tree = await document.GetSyntaxTreeAsync();
        var config = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree!);
        // Keep LF/CRLF unless the applicable .editorconfig explicitly chooses one.
        if (!config.TryGetValue("end_of_line", out _))
        {
            string newline = original.Lines.Select(line => original.ToString(
                    TextSpan.FromBounds(line.End, line.EndIncludingLineBreak)))
                .FirstOrDefault(value => value.Length > 0) ?? Environment.NewLine;
            options = options.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, newline);
        }

        Document formatted = await Formatter.FormatAsync(document, options);
        SourceText text = await formatted.GetTextAsync();
        SyntaxNode root = (await formatted.GetSyntaxRootAsync())!;
        string lineEnding = options.GetOption(FormattingOptions.NewLine, LanguageNames.CSharp);
        var changes = new List<TextChange>();

        foreach (FieldDeclarationSyntax field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!field.AttributeLists.SelectMany(list => list.Attributes).Any(IsHeader)) continue;

            SyntaxToken first = field.GetFirstToken();
            TextLine line = text.Lines.GetLineFromPosition(first.SpanStart);
            string indent = text.ToString(TextSpan.FromBounds(line.Start, first.SpanStart));
            // Formatter normally places fields on their own line. Do not interpret
            // non-whitespace text as indentation in unusual inline declarations.
            if (indent.Any(character => character is not (' ' or '\t'))) continue;

            for (int i = 0; i < field.AttributeLists.Count - 1; i++)
            {
                AddWhitespaceChange(text, changes, field.AttributeLists[i].Span.End,
                    field.AttributeLists[i + 1].SpanStart, " ");
            }

            SyntaxToken declaration = field.Modifiers.Count > 0
                ? field.Modifiers[0] : field.Declaration.GetFirstToken();
            AddWhitespaceChange(text, changes, field.AttributeLists.Last().Span.End,
                declaration.SpanStart, lineEnding + indent);
        }

        return text.WithChanges(changes);
    }

    private static bool IsHeader(AttributeSyntax attribute)
    {
        string name = attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => ""
        };
        return name is "Header" or "HeaderAttribute";
    }

    private static void AddWhitespaceChange(SourceText text, List<TextChange> changes,
        int start, int end, string replacement)
    {
        TextSpan span = TextSpan.FromBounds(start, end);
        string current = text.ToString(span);
        // Comments and preprocessor directives are never removed or moved across.
        if (current.All(character => character is ' ' or '\t' or '\r' or '\n') && current != replacement)
            changes.Add(new TextChange(span, replacement));
    }
}
