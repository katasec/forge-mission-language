namespace ForgeMission.Core.Runtime;

public enum MissionStatus { Pass, Fail }

public record MissionResult(
    string MissionName,
    string Text,
    MissionStatus Status = MissionStatus.Pass,
    string? FailReason = null,
    int Attempts = 1);
