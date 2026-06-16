namespace ForgeMission.Cli.Docker;

public enum PrereqStatus { Pass, Fail, Skipped }

public record PrereqCheck(string Label, PrereqStatus Status, string Detail);
