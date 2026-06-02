namespace PatchMindAI.Core.Enums;

public enum PatchingStatus
{
    Vulnerable,          // CVE detected, no patch applied
    InProgress,          // Patch deployment in progress
    Patched,            // Successfully patched
    Mitigated,          // Workaround applied, not patched
    NotApplicable,      // CVE doesn't apply to this asset
    AcceptedRisk        // Known vulnerability, risk accepted
}
