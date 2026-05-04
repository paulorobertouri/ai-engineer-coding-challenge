using Api.Services;
using OpenAI;
using DotNetEnv;
using Serilog;

// Load environment variables from .env file
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, config) =>
{
    var logsPath = Path.Combine(ctx.HostingEnvironment.ContentRootPath, "Logs", "api-.log");
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(logsPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);
});
builder.Configuration.AddEnvironmentVariables();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddControllers();
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

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["OpenAI:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException("OpenAI ApiKey is missing.");
    }

    return new OpenAIClient(apiKey);
});

builder.Services.AddSingleton<IChunkingService, MarkdownChunkingService>();
builder.Services.AddSingleton<IVectorStoreService, JsonVectorStoreService>();

var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
if (string.IsNullOrWhiteSpace(openAiApiKey))
{
    builder.Services.AddSingleton<IEmbeddingService, DeterministicEmbeddingService>();
    builder.Services.AddSingleton<IRetrievalChatService, FallbackRetrievalChatService>();
}
else
{
    builder.Services.AddSingleton<IEmbeddingService, OpenAIEmbeddingService>();
    builder.Services.AddSingleton<IRetrievalChatService, OpenAIRetrievalChatService>();
}

var app = builder.Build();

app.UseResponseCompression();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseCors("LocalFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
