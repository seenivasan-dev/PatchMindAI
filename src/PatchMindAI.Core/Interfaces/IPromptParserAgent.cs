using PatchMindAI.Core.Models;

namespace PatchMindAI.Core.Interfaces;

/// <summary>
/// Classifies user queries into structured intents using LLM.
/// </summary>
public interface IPromptParserAgent
{
    /// <summary>
    /// Parses a user's plain-English query and classifies the intent.
    /// </summary>
    Task<ParsedIntent> ParseAsync(string userQuery, CancellationToken cancellationToken = default);
}
