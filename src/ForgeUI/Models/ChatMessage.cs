namespace ForgeUI.Models;

public record ChatMessage(
    string                   UserText,
    string?                  AgentText,
    TrustSignal?             Trust,
    List<PipelineTraceEvent> Trace
);
