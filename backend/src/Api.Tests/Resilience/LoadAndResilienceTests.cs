using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests.Resilience;

public sealed class LoadAndResilienceTests
{
    [Fact]
    public async Task ChatBurst_ConcurrentRequests_AllSucceed()
    {
        await using var harness = await TestHarness.CreateAsync(
            serviceBehavior: async (_, cancellationToken) =>
            {
                await Task.Delay(15, cancellationToken);
                return CreateSuccessResponse();
            },
            configure: config =>
            {
                config["RateLimiting:Mode"] = RateLimitingMode.InMemory;
                config["RateLimiting:Chat:PermitLimit"] = "200";
                config["RateLimiting:Chat:WindowSeconds"] = "60";
                config["RateLimiting:Chat:QueueLimit"] = "0";
                config["Observability:Enabled"] = "false";
            });

        var requests = Enumerable.Range(0, 15)
            .Select(_ => harness.Client.PostAsJsonAsync("/api/v1/chat", CreateChatRequest()))
            .ToArray();

        var responses = await Task.WhenAll(requests);
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        Assert.All(statusCodes, code => Assert.Equal(HttpStatusCode.OK, code));

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task IngestConcurrentRequests_OnlyOneSucceeds_OthersConflict()
    {
        await using var harness = await TestHarness.CreateAsync(
            serviceBehavior: (_, _) => Task.FromResult(CreateSuccessResponse()),
            configure: config =>
            {
                config["RateLimiting:Mode"] = RateLimitingMode.InMemory;
                config["RateLimiting:Ingest:PermitLimit"] = "20";
                config["RateLimiting:Ingest:WindowSeconds"] = "60";
                config["RateLimiting:Ingest:QueueLimit"] = "0";
                config["Observability:Enabled"] = "false";
            });

        var payload = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var secondPayload = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var thirdPayload = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var requests = new[]
        {
            harness.Client.PostAsync("/api/v1/ingest", payload),
            harness.Client.PostAsync("/api/v1/ingest", secondPayload),
            harness.Client.PostAsync("/api/v1/ingest", thirdPayload)
        };

        var responses = await Task.WhenAll(requests);
        var groupedByStatus = responses.GroupBy(response => response.StatusCode)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(1, groupedByStatus.GetValueOrDefault(HttpStatusCode.OK));
        Assert.Equal(2, groupedByStatus.GetValueOrDefault(HttpStatusCode.Conflict));

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Chat_WhenProviderTimesOut_ReturnsRequestTimeout()
    {
        await using var harness = await TestHarness.CreateAsync(
            serviceBehavior: (_, _) => throw new OperationCanceledException(),
            configure: config => config["Observability:Enabled"] = "false");

        using var response = await harness.Client.PostAsJsonAsync("/api/v1/chat", CreateChatRequest());
        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorFactory.RequestTimeoutErrorCode, ReadProblemCode(payload.RootElement));
    }

    [Fact]
    public async Task Chat_WhenProviderRateLimited_ReturnsInternalServerErrorProblem()
    {
        await using var harness = await TestHarness.CreateAsync(
            serviceBehavior: (_, _) => throw new HttpRequestException("429 Too Many Requests"),
            configure: config => config["Observability:Enabled"] = "false");

        using var response = await harness.Client.PostAsJsonAsync("/api/v1/chat", CreateChatRequest());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorFactory.InternalServerErrorCode, ReadProblemCode(payload.RootElement));
    }

    [Fact]
    public async Task Chat_WhenProviderReturnsServerError_ReturnsInternalServerErrorProblem()
    {
        await using var harness = await TestHarness.CreateAsync(
            serviceBehavior: (_, _) => throw new InvalidOperationException("Provider 500"),
            configure: config => config["Observability:Enabled"] = "false");

        using var response = await harness.Client.PostAsJsonAsync("/api/v1/chat", CreateChatRequest());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ApiErrorFactory.InternalServerErrorCode, ReadProblemCode(payload.RootElement));
    }

    private static ChatRequest CreateChatRequest() => new()
    {
        Messages = [new ChatMessageDto { Role = "user", Content = "What are the store hours?" }]
    };

    private static ChatResponse CreateSuccessResponse() => new()
    {
        ConversationId = Guid.NewGuid().ToString("N"),
        AssistantMessage = "Store opens at 7am.",
        Status = "success",
        ToolCalls = [],
        Citations = []
    };

    private static string? ReadProblemCode(JsonElement payload)
    {
        if (payload.TryGetProperty("code", out var code))
        {
            return code.GetString();
        }

        if (payload.TryGetProperty("extensions", out var extensions)
            && extensions.ValueKind == JsonValueKind.Object
            && extensions.TryGetProperty("code", out var nestedCode))
        {
            return nestedCode.GetString();
        }

        return null;
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        private TestHarness(WebApplicationFactory<Program> factory, string tempDirectory)
        {
            Factory = factory;
            Client = factory.CreateClient();
            _tempDirectory = tempDirectory;
        }

        public HttpClient Client { get; }

        public WebApplicationFactory<Program> Factory { get; }

        public static async Task<TestHarness> CreateAsync(
            Func<ChatRequest, CancellationToken, Task<ChatResponse>> serviceBehavior,
            Action<Dictionary<string, string?>>? configure = null)
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"task-54-resilience-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            var sourcePath = Path.Combine(tempDirectory, "SOP.md");
            var vectorStorePath = Path.Combine(tempDirectory, "vector-store.json");

            await File.WriteAllTextAsync(sourcePath, "## Store hours\nThe store opens at 7am and closes at 10pm.");
            await File.WriteAllTextAsync(vectorStorePath, "[]");

            var configuration = new Dictionary<string, string?>
            {
                ["Auth:AllowAnonymousInDevelopment"] = "true",
                ["Auth:ApiKeyHeaderName"] = "X-Api-Key",
                ["Challenge:SourceDocumentPath"] = sourcePath,
                ["Challenge:VectorStorePath"] = vectorStorePath,
                ["VectorStore:Provider"] = "json",
                ["OpenAI:ApiKey"] = string.Empty,
                ["IngestJobs:Mode"] = "sync",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            };

            configure?.Invoke(configuration);

            var factory = new ApiWebApplicationFactory(configuration, serviceBehavior);
            return new TestHarness(factory, tempDirectory);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();

            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }

    private sealed class ApiWebApplicationFactory(
        IReadOnlyDictionary<string, string?> settings,
        Func<ChatRequest, CancellationToken, Task<ChatResponse>> serviceBehavior)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRetrievalChatService>();
                services.AddSingleton<IRetrievalChatService>(new DelegateRetrievalChatService(serviceBehavior));
            });
        }
    }

    private sealed class DelegateRetrievalChatService(
        Func<ChatRequest, CancellationToken, Task<ChatResponse>> behavior) : IRetrievalChatService
    {
        public Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            behavior(request, cancellationToken);
    }
}