namespace ForgeUI.Models;

public record TrustSignal(
    bool Verified,
    int  StepCount,
    int  RetryCount
);
