using Api;
using Api.Middleware;
using Api.Observability;
using Api.Options;
using Api.Services;
using Api.Security;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenAI;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Threading.Channels;
using System.Threading.RateLimiting;

// Load environment variables from .env file
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, config) =>
{
    var isRunningInContainer = string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase);

    var logsPath = isRunningInContainer
        ? "/app/Data/Logs/api-.log"
        : Path.Combine(ctx.HostingEnvironment.ContentRootPath, "Logs", "api-.log");

    config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(logsPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);
});
builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection(OpenAIOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ChallengeOptions>()
    .Bind(builder.Configuration.GetSection(ChallengeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<VectorStoreOptions>()
    .Bind(builder.Configuration.GetSection(VectorStoreOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<VectorStoreOptions>, VectorStoreOptionsValidator>();

builder.Services
    .AddOptions<RetrievalOptions>()
    .Bind(builder.Configuration.GetSection(RetrievalOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<GuardrailOptions>()
    .Bind(builder.Configuration.GetSection(GuardrailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<UploadOptions>()
    .Bind(builder.Configuration.GetSection(UploadOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<TimeoutOptions>()
    .Bind(builder.Configuration.GetSection(TimeoutOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ObservabilityOptions>()
    .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<IngestJobsOptions>()
    .Bind(builder.Configuration.GetSection(IngestJobsOptions.SectionName));

builder.Services
    .AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

var uploadOptions = builder.Configuration.GetSection(UploadOptions.SectionName).Get<UploadOptions>() ?? new UploadOptions();
var rateLimitingOptions = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
var openAiOptions = builder.Configuration.GetSection(OpenAIOptions.SectionName).Get<OpenAIOptions>() ?? new OpenAIOptions();
var vectorStoreOptions = builder.Configuration.GetSection(VectorStoreOptions.SectionName).Get<VectorStoreOptions>() ?? new VectorStoreOptions();
var isDistributedRateLimiting = DistributedRateLimitingRegistration.IsDistributedEnabled(rateLimitingOptions);
var observabilityOptions = builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();

builder.Services.AddControllers();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = uploadOptions.MaxUploadBytes);
builder.Services.AddHttpContextAccessor();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

if (observabilityOptions.Enabled)
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(AppTelemetry.ServiceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddSource(AppTelemetry.ActivitySourceName);

            if (observabilityOptions.EnableConsoleExporter)
            {
                tracing.AddConsoleExporter();
            }

            if (!string.IsNullOrWhiteSpace(observabilityOptions.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(observabilityOptions.OtlpEndpoint);
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(AppTelemetry.MeterName);

            if (observabilityOptions.EnableConsoleExporter)
            {
                metrics.AddConsoleExporter();
            }

            if (!string.IsNullOrWhiteSpace(observabilityOptions.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(observabilityOptions.OtlpEndpoint);
                });
            }
        });
}

builder.Services.AddSingleton<IChunkingService, HybridChunkingService>();
builder.Services.AddSingleton<IDocumentExtractionService, LocalDocumentExtractionService>();
builder.Services.AddSingleton<IIngestionAuditService, JsonIngestionAuditService>();
builder.Services.AddSingleton<IRetrievalReranker, LexicalRetrievalReranker>();
builder.Services.AddSingleton<IUserQueryGuardrailService, RuleBasedUserQueryGuardrailService>();
builder.Services.AddSingleton<OpenAIUsageTracker>();
builder.Services.AddSingleton<OpenAIRetrievalChatServiceDependencies>();
builder.Services.AddSingleton<IIngestJobStatusStore, InMemoryIngestJobStatusStore>();
builder.Services.AddSingleton(Channel.CreateUnbounded<IngestJobRequest>());
builder.Services.AddSingleton<IIngestProcessingService, IngestProcessingService>();
builder.Services.AddSingleton<IIngestJobDispatcher, IngestJobDispatcher>();
builder.Services.AddHostedService<IngestJobBackgroundService>();
builder.Services.AddSingleton<IVectorStoreService>(sp =>
{
    var provider = vectorStoreOptions.Provider.Trim().ToLowerInvariant();

    return provider switch
    {
        "json" => ActivatorUtilities.CreateInstance<JsonVectorStoreService>(sp),
        _ => throw new InvalidOperationException($"Unsupported VectorStore provider '{vectorStoreOptions.Provider}'.")
    };
});


builder.Services.AddAuthentication(LocalApiKeyAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, LocalApiKeyAuthenticationHandler>(
        LocalApiKeyAuthenticationDefaults.AuthenticationScheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ChatUser, policy =>
    {
        policy.RequireAssertion(context =>
            builder.Environment.IsDevelopment() || context.User.HasClaim("scope", AuthorizationPolicies.ChatUser));
    });

    options.AddPolicy(AuthorizationPolicies.Operator, policy =>
    {
        policy.RequireAssertion(context =>
            builder.Environment.IsDevelopment() || context.User.HasClaim("scope", AuthorizationPolicies.Operator));
    });

    options.AddPolicy(AuthorizationPolicies.KnowledgeAdmin, policy =>
    {
        policy.RequireAssertion(context =>
            builder.Environment.IsDevelopment() || context.User.HasClaim("scope", AuthorizationPolicies.KnowledgeAdmin));
    });
});

if (isDistributedRateLimiting)
{
    DistributedRateLimitingRegistration.ConfigureDistributedProvider(builder.Services, rateLimitingOptions);
    builder.Services.AddSingleton<DistributedRateLimitingMiddleware>();
}
// Rate limiting defaults: 30 requests/minute per IP on /api/chat; 10 requests/minute on /api/ingest
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("chat", httpContext =>
        isDistributedRateLimiting
            ? RateLimitPartition.GetNoLimiter("chat")
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitingOptions.Chat.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitingOptions.Chat.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitingOptions.Chat.QueueLimit
                }));

    options.AddPolicy("ingest", httpContext =>
        isDistributedRateLimiting
            ? RateLimitPartition.GetNoLimiter("ingest")
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitingOptions.Ingest.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitingOptions.Ingest.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitingOptions.Ingest.QueueLimit
                }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var problem = ApiErrorFactory.RateLimit(context.HttpContext.Request.Path);
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
});

if (string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
{
    builder.Services.AddSingleton<IEmbeddingService, DeterministicEmbeddingService>();
    builder.Services.AddSingleton<IRetrievalChatService, FallbackRetrievalChatService>();
}
else
{
    builder.Services.AddSingleton(new OpenAIClient(openAiOptions.ApiKey));
    builder.Services.AddSingleton<IEmbeddingService, OpenAIEmbeddingService>();
    builder.Services.AddSingleton<IRetrievalChatService, OpenAIRetrievalChatService>();
}

var app = builder.Build();

app.UseResponseCompression();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseAuthentication();

var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    foreach (var groupName in apiVersionDescriptionProvider.ApiVersionDescriptions.Select(d => d.GroupName))
    {
        options.SwaggerEndpoint(
            $"/swagger/{groupName}/swagger.json",
            $"Grocery Store SOP API {groupName.ToUpperInvariant()}");
    }
    options.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var isSwaggerRequest = context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
    var contentSecurityPolicy = isSwaggerRequest
        ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"
        : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    context.Response.Headers.Append("Content-Security-Policy", contentSecurityPolicy);
    context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseCors("LocalFrontend");
if (isDistributedRateLimiting)
{
    app.UseMiddleware<DistributedRateLimitingMiddleware>();
}
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

public partial class Program
{
    protected Program()
    {
    }
}
