namespace ForgeMission.Core.Parser;

internal class MclAstBuilder : MclGrammarBaseVisitor<object?>
{
    public override object? VisitProgram(MclGrammarParser.ProgramContext ctx)
    {
        var uses = ctx.useDecl()
            .Select(u => (UseDeclaration)Visit(u)!)
            .ToList();

        var bindings = ctx.letBinding()
            .Select(b => (LetBinding)Visit(b)!)
            .ToList();

        var declarations = ctx.declaration()
            .Select(d => (Declaration)Visit(d)!)
            .ToList();

        var outputs = ctx.outputDecl()
            .Select(o => (OutputDeclaration)Visit(o)!)
            .ToList();

        return new Program(uses, bindings, declarations, outputs);
    }

    public override object? VisitUseDecl(MclGrammarParser.UseDeclContext ctx)
        => new UseDeclaration(StripQuotes(ctx.STRING().GetText()));

    public override object? VisitLetBinding(MclGrammarParser.LetBindingContext ctx)
    {
        var name  = ctx.LOWER_ID().GetText();
        var value = ParseLetValue(ctx.value());
        return new LetBinding(name, value);
    }

    public override object? VisitOutputDecl(MclGrammarParser.OutputDeclContext ctx)
    {
        var missionName = ctx.UPPER_ID().GetText();
        var filePath    = ctx.STRING() is { } s ? StripQuotes(s.GetText()) : null;
        return new OutputDeclaration(missionName, filePath);
    }

    public override object? VisitDeclaration(MclGrammarParser.DeclarationContext ctx)
    {
        if (ctx.mission() is { } m) return Visit(m);
        if (ctx.expert() is { } e)  return Visit(e);
        throw new ParseException("Unknown declaration", ctx.Start.Line, ctx.Start.Column);
    }

    public override object? VisitMission(MclGrammarParser.MissionContext ctx)
    {
        var name     = ctx.UPPER_ID().GetText();
        var @params  = ParseParams(ctx.@params());
        var pipeline = (Pipeline)Visit(ctx.pipeline())!;
        var maxLoops = ctx.loopClause() is { } lc ? int.Parse(lc.INT().GetText()) : 1;
        return new MissionDeclaration(name, @params, pipeline, maxLoops);
    }

    public override object? VisitExpert(MclGrammarParser.ExpertContext ctx)
    {
        var name     = ctx.UPPER_ID().GetText();
        var @params  = ParseParams(ctx.@params());
        var pipeline = (Pipeline)Visit(ctx.pipeline())!;
        return new ExpertDeclaration(name, @params, pipeline);
    }

    public override object? VisitPipeline(MclGrammarParser.PipelineContext ctx)
    {
        var steps = ctx.step().Select(s => (Step)Visit(s)!).ToList();
        return new Pipeline(steps);
    }

    public override object? VisitStep(MclGrammarParser.StepContext ctx)
    {
        var name = ctx.UPPER_ID().GetText();
        var with = ctx.withClause() is { } wc
            ? (IReadOnlyList<Binding>)Visit(wc)!
            : (IReadOnlyList<Binding>)[];
        return new Step(name, with);
    }

    public override object? VisitWithClause(MclGrammarParser.WithClauseContext ctx)
    {
        var bindings = ctx.binding().Select(b => (Binding)Visit(b)!).ToList();
        return (IReadOnlyList<Binding>)bindings;
    }

    public override object? VisitBinding(MclGrammarParser.BindingContext ctx)
    {
        var key   = ctx.LOWER_ID().GetText();
        var value = ParseBindingValue(ctx.value());
        return new Binding(key, value);
    }

    private static IReadOnlyList<string> ParseParams(MclGrammarParser.ParamsContext? ctx)
        => ctx is null
            ? []
            : ctx.LOWER_ID().Select(id => id.GetText()).ToList();

    private static LetValue ParseLetValue(MclGrammarParser.ValueContext ctx)
    {
        if (ctx.STRING() is { } str)
            return new StringLetValue(StripQuotes(str.GetText()));
        if (ctx.envCall() is { } env)
            return ParseEnvLetValue(env);
        throw new ParseException(
            "let bindings only support string literals and env() calls",
            ctx.Start.Line, ctx.Start.Column);
    }

    private static BindingValue ParseBindingValue(MclGrammarParser.ValueContext ctx)
    {
        if (ctx.STRING() is { } str)
            return new StringBindingValue(StripQuotes(str.GetText()));
        if (ctx.envCall() is { } env)
        {
            var ev = ParseEnvLetValue(env);
            return new EnvBindingValue(ev.VarName, ev.DefaultValue);
        }
        if (ctx.LOWER_ID() is { } id)
            return new VarRefBindingValue(id.GetText());
        throw new ParseException("Unknown value form", ctx.Start.Line, ctx.Start.Column);
    }

    private static EnvLetValue ParseEnvLetValue(MclGrammarParser.EnvCallContext ctx)
    {
        var strings      = ctx.STRING();
        var varName      = StripQuotes(strings[0].GetText());
        var defaultValue = strings.Length > 1 ? StripQuotes(strings[1].GetText()) : null;
        return new EnvLetValue(varName, defaultValue);
    }

    private static string StripQuotes(string text) => text[1..^1];
}
