namespace ForgeMission.Core.Resolution;

public enum MclErrorCode
{
    UnknownExpert          = 1,
    DuplicateExpert        = 2,
    CircularReference      = 3,
    MissingFrontmatter     = 4,
    SourceNotFound         = 5,
    StaleLockFile          = 6,
    NotInitialised         = 7,
    OciNotPulled           = 10,
    OciPullFailed          = 11,
}

public class MclException(MclErrorCode code, string message, string? detail = null)
    : Exception($"MCL{(int)code:D3} {message}{(detail is null ? "" : $"\n\n{detail}")}")
{
    public MclErrorCode Code    { get; } = code;
    public string?      Detail  { get; } = detail;
}
