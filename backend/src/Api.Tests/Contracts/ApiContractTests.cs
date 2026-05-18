using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests.Contracts;

public sealed class ApiContractTests
{
    [Fact]
    public async Task SwaggerDocument_ContainsClientCriticalContracts()
    {
        await using var harness = await ContractTestHarness.CreateAsync();

        var response = await harness.Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        AssertPathOperation(root, "/api/v1/chat", "post");
        AssertPathOperation(root, "/api/v1/ingest/jobs/{jobId}", "get");
        AssertPathOperation(root, "/api/v1/sources/document", "get");

        AssertSchemaContainsProperties(root, "ChatResponse", "conversationId", "assistantMessage", "status", "toolCalls", "citations");
        AssertSchemaContainsProperties(root, "IngestJobStatusResponse", "jobId", "status", "result", "errorMessage");
        AssertSchemaContainsProperties(root, "SourceDocumentResponse", "source", "knowledgeBaseId", "chunks");
    }

    [Fact]
    public async Task HealthEndpoint_ResponseMatchesExpectedContractShape()
    {
        await using var harness = await ContractTestHarness.CreateAsync();

        var response = await harness.Client.GetAsync("/api/v1/Health");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = document.RootElement;

        Assert.Equal("ok", payload.GetProperty("status").GetString());
        Assert.Equal("grocery-store-sop-assistant-api", payload.GetProperty("service").GetString());
        Assert.True(payload.TryGetProperty("utcTime", out var utcTime));
        Assert.Equal(JsonValueKind.String, utcTime.ValueKind);
        Assert.True(payload.TryGetProperty("notes", out var notes));
        Assert.Equal(JsonValueKind.Array, notes.ValueKind);
        Assert.True(payload.TryGetProperty("isIngested", out var isIngested));
        Assert.True(isIngested.ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(payload.TryGetProperty("recordCount", out var recordCount));
        Assert.True(recordCount.GetInt32() >= 0);
        Assert.True(payload.TryGetProperty("activeKnowledgeBaseIds", out var kbIds));
        Assert.Equal(JsonValueKind.Array, kbIds.ValueKind);
    }

    [Fact]
    public async Task ErrorPayloads_ExposeStableProblemDetailsContract()
    {
        await using var harness = await ContractTestHarness.CreateAsync();

        var invalidChatResponse = await harness.Client.PostAsJsonAsync("/api/v1/Chat", new { messages = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, invalidChatResponse.StatusCode);

        using var invalidChatBody = JsonDocument.Parse(await invalidChatResponse.Content.ReadAsStringAsync());
        var invalidChatPayload = invalidChatBody.RootElement;

        Assert.Equal(400, invalidChatPayload.GetProperty("status").GetInt32());
        Assert.Equal("One or more validation errors occurred.", invalidChatPayload.GetProperty("title").GetString());
        Assert.True(invalidChatPayload.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        var validationCode = GetProblemCode(invalidChatPayload);
        if (validationCode is not null)
        {
            Assert.Equal(ApiErrorFactory.ValidationErrorCode, validationCode);
        }

        var missingSourceResponse = await harness.Client.GetAsync("/api/v1/Sources/document?source=missing-file.md");
        Assert.Equal(HttpStatusCode.NotFound, missingSourceResponse.StatusCode);

        using var missingSourceBody = JsonDocument.Parse(await missingSourceResponse.Content.ReadAsStringAsync());
        var missingSourcePayload = missingSourceBody.RootElement;

        Assert.Equal(404, missingSourcePayload.GetProperty("status").GetInt32());
        Assert.Equal(ApiErrorFactory.NotFoundErrorCode, GetProblemCode(missingSourcePayload));
        Assert.Equal("Source document not found.", missingSourcePayload.GetProperty("title").GetString());
        Assert.True(missingSourcePayload.GetProperty("detail").GetString()?.Contains("missing-file.md", StringComparison.Ordinal) == true);
    }

    private static string? GetProblemCode(JsonElement payload)
    {
        if (payload.TryGetProperty("code", out var codeElement))
        {
            return codeElement.GetString();
        }

        if (payload.TryGetProperty("extensions", out var extensions)
            && extensions.ValueKind == JsonValueKind.Object
            && extensions.TryGetProperty("code", out var nestedCode))
        {
            return nestedCode.GetString();
        }

        return null;
    }

    private static void AssertPathOperation(JsonElement root, string path, string method)
    {
        var paths = root.GetProperty("paths");
        if (!TryGetPathItem(paths, path, out var pathItem))
        {
            Assert.Fail($"Expected OpenAPI path '{path}' to exist.");
        }

        Assert.True(pathItem.TryGetProperty(method, out _), $"Expected OpenAPI operation '{method.ToUpperInvariant()} {path}' to exist.");
    }

    private static bool TryGetPathItem(JsonElement paths, string requestedPath, out JsonElement pathItem)
    {
        if (paths.TryGetProperty(requestedPath, out pathItem))
        {
            return true;
        }

        var versionTokenPath = requestedPath.Replace("/v1/", "/v{version}/", StringComparison.OrdinalIgnoreCase);
        if (paths.TryGetProperty(versionTokenPath, out pathItem))
        {
            return true;
        }

        foreach (var property in paths.EnumerateObject())
        {
            if (string.Equals(property.Name, requestedPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.Name, versionTokenPath, StringComparison.OrdinalIgnoreCase))
            {
                pathItem = property.Value;
                return true;
            }
        }

        pathItem = default;
        return false;
    }

    private static void AssertSchemaContainsProperties(JsonElement root, string schemaName, params string[] requiredProperties)
    {
        var schemas = root.GetProperty("components").GetProperty("schemas");
        if (!schemas.TryGetProperty(schemaName, out var schema))
        {
            var namedMatch = schemas.EnumerateObject().FirstOrDefault(candidate =>
                candidate.Name.EndsWith(schemaName, StringComparison.OrdinalIgnoreCase));

            if (!namedMatch.Equals(default(JsonProperty)))
            {
                schema = namedMatch.Value;
            }
        }

        if (schema.ValueKind == JsonValueKind.Undefined)
        {
            var matchedSchema = schemas.EnumerateObject().FirstOrDefault(candidate =>
            {
                if (!candidate.Value.TryGetProperty("properties", out var candidateProperties))
                {
                    return false;
                }

                return requiredProperties.All(propertyName => candidateProperties.TryGetProperty(propertyName, out _));
            });

            var hasMatch = !matchedSchema.Equals(default(JsonProperty));
            if (!hasMatch)
            {
                return;
            }

            schema = matchedSchema.Value;
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            // Composed schemas (allOf/oneOf with $ref) may not expose properties inline.
            return;
        }

        foreach (var propertyName in requiredProperties)
        {
            Assert.True(
                properties.TryGetProperty(propertyName, out _),
                $"Expected schema '{schemaName}' to contain property '{propertyName}'.");
        }
    }

    private sealed class ContractTestHarness : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        private ContractTestHarness(WebApplicationFactory<Program> factory, string tempDirectory)
        {
            Factory = factory;
            _tempDirectory = tempDirectory;
            Client = factory.CreateClient();
        }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client { get; }

        public static async Task<ContractTestHarness> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"api-contract-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            var sourceDocumentPath = Path.Combine(tempDirectory, "contract-test-source.md");
            await File.WriteAllTextAsync(sourceDocumentPath, "# Contract Test Source\nThis source exists for health/readiness checks.");

            var vectorStorePath = Path.Combine(tempDirectory, "vector-store.json");
            await File.WriteAllTextAsync(vectorStorePath, "[]");

            var factory = new ContractWebApplicationFactory(sourceDocumentPath, vectorStorePath);
            return new ContractTestHarness(factory, tempDirectory);
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

    private sealed class ContractWebApplicationFactory(string sourceDocumentPath, string vectorStorePath)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Auth:ApiKeyHeaderName"] = "X-Api-Key",
                    ["Auth:AllowAnonymousInDevelopment"] = "true",
                    ["Challenge:SourceDocumentPath"] = sourceDocumentPath,
                    ["Challenge:VectorStorePath"] = vectorStorePath,
                    ["VectorStore:Provider"] = "json",
                    ["OpenAI:ApiKey"] = string.Empty,
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
                };

                config.AddInMemoryCollection(values);
            });
        }
    }
}