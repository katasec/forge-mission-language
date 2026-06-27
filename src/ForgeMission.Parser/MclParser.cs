using Antlr4.Runtime;

namespace ForgeMission.Parser;

public record Diagnostic(string Message, int Line, int Column, int EndColumn = -1);

public record ParseResult(Program? Ast, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Ast is not null && Diagnostics.Count == 0;
}

public static class MclParser
{
    /// <summary>
    /// Parse MCL source and return the AST, throwing <see cref="ParseException"/> on error.
    /// </summary>
    public static Program Parse(string source)
    {
        var result = TryParse(source);
        if (!result.Success)
        {
            var first = result.Diagnostics[0];
            throw new ParseException(first.Message, first.Line, first.Column);
        }
        return result.Ast!;
    }

    /// <summary>
    /// Parse MCL source and return all diagnostics alongside a best-effort AST.
    /// Use this path for LSP / tooling that needs to handle incomplete input.
    /// </summary>
    public static ParseResult TryParse(string source)
    {
        var diagnostics = new List<Diagnostic>();

        var inputStream  = CharStreams.fromString(source);
        var lexer        = new MclGrammarLexer(inputStream);
        var tokenStream  = new CommonTokenStream(lexer);
        var parser       = new MclGrammarParser(tokenStream);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();

        var errorListener = new DiagnosticErrorListener(diagnostics);
        lexer.AddErrorListener(errorListener);
        parser.AddErrorListener(errorListener);

        var tree = parser.program();

        if (diagnostics.Count > 0)
            return new ParseResult(null, diagnostics);

        var ast = (Program)new MclAstBuilder().Visit(tree)!;
        return new ParseResult(ast, []);
    }
}

file sealed class DiagnosticErrorListener(List<Diagnostic> diagnostics)
    : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
{
    // Called by the lexer (offending symbol is an int token type)
    void IAntlrErrorListener<int>.SyntaxError(
        System.IO.TextWriter output, IRecognizer recognizer,
        int offendingSymbol, int line, int col, string msg,
        Antlr4.Runtime.RecognitionException e)
    {
        diagnostics.Add(new Diagnostic(msg, line, col, col + 1));
    }

    // Called by the parser (offending symbol is an IToken)
    void IAntlrErrorListener<IToken>.SyntaxError(
        System.IO.TextWriter output, IRecognizer recognizer,
        IToken offendingSymbol, int line, int col, string msg,
        Antlr4.Runtime.RecognitionException e)
    {
        var message = offendingSymbol?.Type switch
        {
            MclGrammarLexer.FAT_ARROW =>
                "'=>' is not a valid operator — use '->' to connect pipeline steps",

            MclGrammarLexer.LOWER_ID when InMissionRule(recognizer) =>
                $"'{offendingSymbol!.Text}' is not valid here — did you mean to wrap parameters in parentheses? e.g. mission Name({offendingSymbol.Text})",

            MclGrammarLexer.LOWER_ID =>
                $"'{offendingSymbol!.Text}' is not valid here — expert and mission names must be PascalCase",

            _ => msg
        };

        var endCol = col + (offendingSymbol?.Text?.Length ?? 1);
        diagnostics.Add(new Diagnostic(message, line, col, endCol));
    }

    // Returns true when the error occurs directly inside the mission rule —
    // distinguishes a missing-parens mistake from a lowercase expert name in a pipeline.
    private static bool InMissionRule(IRecognizer recognizer)
    {
        return recognizer is MclGrammarParser parser
            && parser.Context.RuleIndex == MclGrammarParser.RULE_mission;
    }
}
