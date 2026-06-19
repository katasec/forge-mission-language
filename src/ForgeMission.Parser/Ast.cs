namespace ForgeMission.Parser;

public record Program(
    IReadOnlyList<LetBinding> Bindings,
    IReadOnlyList<Declaration> Declarations,
    IReadOnlyList<OutputDeclaration> Outputs);

public record OutputDeclaration(string MissionName, string? FilePath);

public record LetBinding(string Name, LetValue Value);

public abstract record LetValue;
public record StringLetValue(string Text) : LetValue;
public record EnvLetValue(string VarName, string? DefaultValue) : LetValue;

public abstract record Declaration(string Name);

public record MissionDeclaration(string Name, IReadOnlyList<string> Params, Pipeline Pipeline, int MaxLoops = 1)
    : Declaration(Name);

// Pipeline is a sequence of elements — each element is a step or a parallel block.
public record Pipeline(IReadOnlyList<PipelineElement> Elements);

public abstract record PipelineElement;
public record StepElement(Step Step) : PipelineElement;
public record ParallelElement(IReadOnlyList<Step> Steps) : PipelineElement;

// A step is a named expert/mission call with optional domain context, provider profile, and guard.
public record Step(
    string ExpertName,
    IReadOnlyList<Binding> Context,   // (key: value) named parameters
    string? Using,                    // using <profile> — provider profile selector
    WhenClause? When);                // when() guard — null means unconditional

// When guard — abstract so new expression types are additive (Phase 22+)
public abstract record WhenClause;
public record StringEqualsWhen(string Key, string Value) : WhenClause;
public record ElseWhen() : WhenClause;

// Binding value types
public abstract record BindingValue;
public record StringBindingValue(string Text) : BindingValue;
public record VarRefBindingValue(string Name) : BindingValue;
public record EnvBindingValue(string VarName, string? DefaultValue) : BindingValue;
public record NumberBindingValue(int Number) : BindingValue;

public record Binding(string Key, BindingValue Value);
