using ForgeMission.Core.Runtime;

namespace ForgeUI.Models;

public record PipelineTraceEvent(
    string       ExpertName,
    StepEnvelope Envelope,
    DateTime     Timestamp,
    int          Attempt
);
