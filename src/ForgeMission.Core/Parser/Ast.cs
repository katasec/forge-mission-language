namespace ForgeMission.Core.Parser;

public record Program(
    IReadOnlyList<UseDeclaration> Uses,
    IReadOnlyList<LetBinding> Bindings,
    IReadOnlyList<Declaration> Declarations,
    IReadOnlyList<OutputDeclaration> Outputs);

public record UseDeclaration(string Source);

public record OutputDeclaration(string MissionName, string? FilePath);

public record LetBinding(string Name, LetValue Value);

public abstract record LetValue;
public record StringLetValue(string Text) : LetValue;
public record EnvLetValue(string VarName, string? DefaultValue) : LetValue;

public abstract record Declaration(string Name);

public record MissionDeclaration(string Name, IReadOnlyList<string> Params, Pipeline Pipeline, int MaxLoops = 1)
    : Declaration(Name);

public record ExpertDeclaration(string Name, IReadOnlyList<string> Params, Pipeline Pipeline)
    : Declaration(Name);

public record Pipeline(IReadOnlyList<Step> Steps);

public record Step(string ExpertName, IReadOnlyList<Binding> With);

public abstract record BindingValue;
public record StringBindingValue(string Text) : BindingValue;
public record VarRefBindingValue(string Name) : BindingValue;
public record EnvBindingValue(string VarName, string? DefaultValue) : BindingValue;

public record Binding(string Key, BindingValue Value);
