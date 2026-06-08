using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Services;

namespace PatchMindAI.Tests.Unit.Services;

public class AzureSearchKnowledgeRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_ShouldUseVectorFirst_AndFallbackToLexical_WhenVectorReturnsNoResults()
    {
        var queryRunner = new Mock<IAzureSearchQueryRunner>();
        var options = Options.Create(new AzureSearchOptions
        {
            EnableVectorSearch = true,
            SourceIdField = "id",
            ContentField = "content",
            VectorField = "contentVector"
        });

        var calls = new List<(string? SearchText, bool HasVectorSearch)>();

        queryRunner
            .Setup(r => r.SearchAsync(It.IsAny<string?>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string?, SearchOptions, CancellationToken>((searchText, searchOptions, _) =>
            {
                calls.Add((searchText, searchOptions.VectorSearch is not null));
            })
            .ReturnsAsync(() =>
            {
                if (calls.Count == 1)
                {
                    return Array.Empty<RetrievedChunk>();
                }

                return new[]
                {
                    new RetrievedChunk
                    {
                        SourceId = "CVE-2021-44228",
                        Text = "Log4Shell",
                        Score = 1.0
                    }
                };
            });

        var retriever = new AzureSearchKnowledgeRetriever(queryRunner.Object, options, NullLogger<AzureSearchKnowledgeRetriever>.Instance);

        var results = await retriever.RetrieveAsync("log4shell", 5, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(2, calls.Count);
        Assert.Null(calls[0].SearchText);
        Assert.True(calls[0].HasVectorSearch);
        Assert.Equal("log4shell", calls[1].SearchText);
        Assert.False(calls[1].HasVectorSearch);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldFallbackToLexical_WhenVectorThrowsRequestFailedException()
    {
        var queryRunner = new Mock<IAzureSearchQueryRunner>();
        var options = Options.Create(new AzureSearchOptions
        {
            EnableVectorSearch = true,
            SourceIdField = "id",
            ContentField = "content",
            VectorField = "contentVector"
        });

        var calls = new List<(string? SearchText, bool HasVectorSearch)>();

        queryRunner
            .Setup(r => r.SearchAsync(It.IsAny<string?>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string?, SearchOptions, CancellationToken>((searchText, searchOptions, _) =>
            {
                calls.Add((searchText, searchOptions.VectorSearch is not null));
            })
            .Returns<string?, SearchOptions, CancellationToken>((_, searchOptions, _) =>
            {
                if (searchOptions.VectorSearch is not null)
                {
                    throw new RequestFailedException(400, "vector unsupported", "BadRequest", null);
                }

                IReadOnlyList<RetrievedChunk> chunks = new[]
                {
                    new RetrievedChunk
                    {
                        SourceId = "CVE-2014-0160",
                        Text = "Heartbleed",
                        Score = 0.8
                    }
                };
                return Task.FromResult(chunks);
            });

        var retriever = new AzureSearchKnowledgeRetriever(queryRunner.Object, options, NullLogger<AzureSearchKnowledgeRetriever>.Instance);

        var results = await retriever.RetrieveAsync("heartbleed", 5, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(2, calls.Count);
        Assert.True(calls[0].HasVectorSearch);
        Assert.False(calls[1].HasVectorSearch);
    }
}
