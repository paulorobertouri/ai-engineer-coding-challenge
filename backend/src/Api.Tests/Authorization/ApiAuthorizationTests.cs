using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Authorization;

public class ApiAuthorizationTests
{
    [Fact]
    public async Task InDevelopment_ProtectedPreview_AllowsAnonymousAccess()
    {
        await using var factory = CreateFactory(environmentName: "Development", apiKey: null);
        var client = factory.CreateClient();
        var response = await SendPreviewRequestAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InProduction_ProtectedPreview_DeniesAnonymousAccess()
    {
        await using var factory = CreateFactory(environmentName: "Production", apiKey: null);
        var client = factory.CreateClient();
        var response = await SendPreviewRequestAsync(client);

        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InProduction_ProtectedPreview_AllowsValidApiKey()
    {
        await using var factory = CreateFactory(environmentName: "Production", apiKey: "secret-key");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "secret-key");

        var response = await SendPreviewRequestAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InProduction_ChatUserKey_CanAccessChatButCannotAccessPreview()
    {
        await using var factory = CreateFactory(environmentName: "Production", apiKey: null);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "chat-only-key");

        var chatResponse = await SendChatRequestAsync(client);
        var previewResponse = await SendPreviewRequestAsync(client);

        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        Assert.True(previewResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

        [Fact]
        public async Task InDevelopment_Chat_AllowsAnonymousAccess()
        {
                await using var factory = CreateFactory(environmentName: "Development", apiKey: null);
                var client = factory.CreateClient();

                var response = await SendChatRequestAsync(client);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task InProduction_Chat_DeniesAnonymousAccess()
        {
                await using var factory = CreateFactory(environmentName: "Production", apiKey: null);
                var client = factory.CreateClient();

                var response = await SendChatRequestAsync(client);

                Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        }

        private static async Task<HttpResponseMessage> SendChatRequestAsync(HttpClient client)
        {
                var payload = """
                {
                    "conversationId": "auth-test",
                    "messages": [
                        {
                            "role": "user",
                            "content": "What are the store hours?"
                        }
                    ]
                }
                """;

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                return await client.PostAsync("/api/v1/chat", content);
        }

    private static WebApplicationFactory<Program> CreateFactory(string environmentName, string? apiKey)
    {
        return new ApiWebApplicationFactory(environmentName, apiKey);
    }

    private static async Task<HttpResponseMessage> SendPreviewRequestAsync(HttpClient client)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("## Section\nPreview content"));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/markdown");
        content.Add(fileContent, "file", "preview.md");

        return await client.PostAsync("/api/v1/ingest/preview", content);
    }

    private sealed class ApiWebApplicationFactory(string environmentName, string? apiKey)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["Auth:ApiKeyHeaderName"] = "X-Api-Key",
                    ["Auth:ApiKey"] = apiKey,
                    ["Auth:AllowAnonymousInDevelopment"] = "true",
                    ["Auth:ApiKeys:0:Name"] = "chat-user",
                    ["Auth:ApiKeys:0:Key"] = "chat-only-key",
                    ["Auth:ApiKeys:0:Scopes:0"] = AuthorizationPolicies.ChatUser,
                    ["Auth:ApiKeys:1:Name"] = "operator",
                    ["Auth:ApiKeys:1:Key"] = "secret-key",
                    ["Auth:ApiKeys:1:Scopes:0"] = AuthorizationPolicies.ChatUser,
                    ["Auth:ApiKeys:1:Scopes:1"] = AuthorizationPolicies.Operator,
                    ["Auth:ApiKeys:1:Scopes:2"] = AuthorizationPolicies.KnowledgeAdmin,
                    ["Challenge:SourceDocumentPath"] = "../../../../knowledge-base/Grocery_Store_SOP.md",
                    ["Challenge:VectorStorePath"] = "Data/vector-store.json",
                    ["VectorStore:Provider"] = "json",
                    ["OpenAI:ApiKey"] = string.Empty,
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
                };

                config.AddInMemoryCollection(values);
            });
        }
    }
}
