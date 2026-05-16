using Api.Contracts;
using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class ChatResponseMapperTests
{
    [Fact]
    public void FromMatches_MapsCitationsAndStructuredOutput()
    {
        var matches = new List<VectorSearchMatch>
        {
            new()
            {
                Score = 0.8123,
                Record = new VectorRecord
                {
                    Id = "chunk-1",
                    Source = "SOP.md",
                    ChunkText = "## Store Hours\nOpen Monday to Friday",
                    Metadata = new Dictionary<string, string>
                    {
                        ["SectionTitle"] = "Store Hours"
                    }
                }
            }
        };

        var usage = new ChatUsageDto
        {
            Model = "fallback",
            Source = "fallback",
            IsEstimated = true
        };

        var response = ChatResponseMapper.FromMatches(
            conversationId: "conv-1",
            status: "success",
            isPlaceholder: true,
            assistantMessage: "Store opens at 8am.",
            matches: matches,
            usage: usage);

        Assert.Equal("conv-1", response.ConversationId);
        Assert.Equal("success", response.Status);
        Assert.True(response.IsPlaceholder);
        Assert.Single(response.Citations);
        Assert.Equal("chunk-1", response.Citations[0].ChunkId);
        Assert.Equal("Store Hours", response.Citations[0].SectionTitle);
        Assert.NotEqual(ConfidenceIndicatorDto.NotFound, response.Confidence.Level);
        Assert.Equal("Store opens at 8am.", response.StructuredOutput.AnswerText);
        Assert.Equal(usage, response.Usage);
    }

    [Fact]
    public void NoContext_MapsNotFoundConfidenceAndReason()
    {
        var usage = new ChatUsageDto
        {
            Model = "gpt-4o-mini",
            Source = "guardrail",
            IsEstimated = true
        };

        var response = ChatResponseMapper.NoContext(
            conversationId: "conv-2",
            status: "success",
            isPlaceholder: false,
            assistantMessage: "I cannot answer that.",
            reason: "guardrail_sensitive",
            usage: usage);

        Assert.Equal("conv-2", response.ConversationId);
        Assert.Equal("success", response.Status);
        Assert.False(response.IsPlaceholder);
        Assert.Empty(response.Citations);
        Assert.Equal(ConfidenceIndicatorDto.NotFound, response.Confidence.Level);
        Assert.Equal("guardrail_sensitive", response.StructuredOutput.RefusalReason);
        Assert.Equal(usage, response.Usage);
    }
}
