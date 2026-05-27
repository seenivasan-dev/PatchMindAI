namespace PatchMindAI.Core.Domain;

public sealed class ChatSession
{
    public string SessionId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastActivityAtUtc { get; set; }
}
