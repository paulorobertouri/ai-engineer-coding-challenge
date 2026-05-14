using System.Text.Json;
using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

public sealed class RagEvaluationFixtureTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _storePath;

    public RagEvaluationFixtureTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"rag-eval-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _storePath = Path.Combine(_tempDirectory, "vector-store.json");
    }

    [Fact]
    public async Task FixtureQuestions_ReturnExpectedEvidenceSections_WithoutPaidModelCalls()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Fixtures/rag-evaluation.json"));
        var fixtureJson = await File.ReadAllTextAsync(fixturePath);
        var scenarios = JsonSerializer.Deserialize<List<RagEvalScenario>>(fixtureJson) ?? [];

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(_tempDirectory);

        var vectorStore = new JsonVectorStoreService(
            Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
            {
                VectorStorePath = _storePath,
                SourceDocumentPath = "unused.md"
            }),
            environment.Object);

        var embedder = new DeterministicEmbeddingService();
        await vectorStore.SaveAsync(await BuildSeedRecordsAsync(embedder));

        var service = new FallbackRetrievalChatService(
            embedder,
            vectorStore,
            new LexicalRetrievalReranker(),
            Microsoft.Extensions.Options.Options.Create(new RetrievalOptions
            {
                TopK = 3,
                MinSimilarityScore = 0.30
            }),
            NullLogger<FallbackRetrievalChatService>.Instance);

        foreach (var scenario in scenarios)
        {
            var response = await service.GenerateResponseAsync(new ChatRequest
            {
                ConversationId = "eval",
                Messages = [new ChatMessageDto { Role = "user", Content = scenario.Question }]
            });

            if (scenario.ExpectCitations)
            {
                Assert.NotEmpty(response.Citations);
                Assert.Contains(
                    response.Citations,
                    citation => citation.SectionTitle.Contains(scenario.ExpectedSectionTitle, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                Assert.Empty(response.Citations);
                Assert.Contains("could not find enough relevant information", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static async Task<List<VectorRecord>> BuildSeedRecordsAsync(IEmbeddingService embedder)
    {
        var chunks = new[]
        {
            (Id: "opening-0000", Section: "Store Opening Procedures", Text: "## Store Opening Procedures\nOpening procedures include unlocking doors and checking registers before opening."),
            (Id: "complaints-0001", Section: "Customer Complaints", Text: "## Customer Complaints\nWhen handling customer complaints, listen actively, apologize, and document details in the incident log."),
            (Id: "closing-0002", Section: "Closing Checklist", Text: "## Closing Checklist\nClosing checklist includes cash reconciliation, cleaning, and alarm activation.")
        };

        var records = new List<VectorRecord>();
        foreach (var chunk in chunks)
        {
            var embedding = await embedder.EmbedAsync(chunk.Text);
            records.Add(new VectorRecord
            {
                Id = chunk.Id,
                Source = "Grocery_Store_SOP.md",
                ChunkText = chunk.Text,
                Embedding = embedding,
                Metadata = new Dictionary<string, string>
                {
                    ["SectionTitle"] = chunk.Section
                }
            });
        }

        return records;
    }

    private sealed record RagEvalScenario(string Question, string ExpectedSectionTitle, bool ExpectCitations);
}
