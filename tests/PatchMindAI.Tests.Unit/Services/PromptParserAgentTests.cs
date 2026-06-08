using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using PatchMindAI.Agents;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Enums;

namespace PatchMindAI.Tests.Unit.Services;

public class PromptParserAgentTests
{
    [Fact]
    public async Task ParseAsync_ShouldBypassLlm_WhenQueryContainsExactCveId()
    {
        var chat = new Mock<IChatCompletionService>(MockBehavior.Strict);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AgentSettings());

        var agent = new PromptParserAgent(
            chat.Object,
            cache,
            options,
            NullLogger<PromptParserAgent>.Instance);

        var result = await agent.ParseAsync("Analyze CVE-2021-44228 impact", CancellationToken.None);

        Assert.Equal(QueryIntent.CveSearch, result.Intent);
        Assert.Equal("CVE-2021-44228", result.ExtractedCveId);
        Assert.Equal(1.0, result.Confidence);

        chat.Verify(
            x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ParseAsync_ShouldUseIntentCache_ForRepeatedQuery()
    {
        var chat = new Mock<IChatCompletionService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AgentSettings
        {
            IntentCacheTtlMinutes = 10,
            CacheTimeWindowMinutes = 15
        });

        chat.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, """
                { "intent": "PriorityReport", "topN": 10, "confidence": 0.9 }
                """)
            });

        var agent = new PromptParserAgent(
            chat.Object,
            cache,
            options,
            NullLogger<PromptParserAgent>.Instance);

        var first = await agent.ParseAsync("what should we patch first", CancellationToken.None);
        var second = await agent.ParseAsync("what should we patch first", CancellationToken.None);

        Assert.Equal(QueryIntent.PriorityReport, first.Intent);
        Assert.Equal(QueryIntent.PriorityReport, second.Intent);

        chat.Verify(
            x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
