namespace PatchMindAI.Core.Domain;

public sealed class ChatMessage
{
    public Guid Id { get; set; }

    public string SessionId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }

    public string? RelatedCveId { get; set; }
}
