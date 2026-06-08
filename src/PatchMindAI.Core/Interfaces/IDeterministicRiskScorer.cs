using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Core.Interfaces;

public interface IDeterministicRiskScorer
{
    RiskScoringResult Score(Cve cve, SqlFactSnapshot facts);
}
