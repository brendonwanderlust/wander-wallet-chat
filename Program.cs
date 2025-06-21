using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using System.Text.Json.Serialization;
using wander_wallet_chat;
using wander_wallet_chat.Middleware;
using wander_wallet_chat.Plugins;

var builder = WebApplication.CreateBuilder(args);
 
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "json";
});
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.JsonWriterOptions = new JsonWriterOptions
    {
        Indented = false  
    };
});

var allowedOrigins = new string[] { "https://localhost", "https://localhost:8100", "http://localhost:8100", "capacitor://localhost" };
var headers = new string[] { "Access-Control-Allow-Origin", "Origin", "Content-Length", "Content-Type", "Authorization" };
var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIURL");
var apiKey = Environment.GetEnvironmentVariable("AzureOpenAIKey");
var deploymentName = Environment.GetEnvironmentVariable("AzureOpenAIDeploymentName");
 
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ChatHandler>();

builder.Services.AddHttpClient<WeatherPlugin>();
builder.Services.AddTransient<WeatherPlugin>();
 
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: deploymentName!,
        endpoint: endpoint!,
        apiKey: apiKey!
    );

    var kernel = kernelBuilder.Build();

    try
    {
        var weatherPlugin = serviceProvider.GetRequiredService<WeatherPlugin>();
        kernel.Plugins.AddFromObject(weatherPlugin, "WeatherPlugin");
        logger.LogInformation("Weather plugin registered successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to register weather plugin");
    }

    return kernel;
});

builder.Services.AddSingleton<IChatCompletionService>(serviceProvider =>
{
    var kernel = serviceProvider.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>();
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithHeaders(headers)
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .WithExposedHeaders("Content-Length", "Content-Type")
              .AllowCredentials();
    });

    options.AddPolicy("SSE", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithHeaders(headers)
              .WithMethods("GET", "POST", "OPTIONS")
              .WithExposedHeaders("Content-Length", "Content-Type")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromSeconds(3600));
    });
});

var app = builder.Build();
 
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRouting();
app.UseCors();

if (builder.Environment.IsDevelopment())
{
    Env.Load();
}

var chatApi = app.MapGroup("/chat");

chatApi.MapPost("/stream", async (
    [FromBody] ChatRequest request,
    ChatHandler handler,
    HttpContext context,
    ILogger<Program> logger) =>
{
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["UserId"] = request.UserId ?? "anonymous-user",
        ["EndpointType"] = "streaming-chat",
        ["RequestId"] = context.TraceIdentifier
    });

    logger.LogInformation("Starting chat stream request for user {UserId}", request.UserId ?? "anonymous-user");

    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    try
    {
        var messageCount = 0;
        await foreach (var chunk in handler.ChatStreaming(request))
        {
            await context.Response.WriteAsync($"data: {chunk}\n\n");
            await context.Response.Body.FlushAsync();
            messageCount++;
        }

        await context.Response.WriteAsync("event: complete\ndata: \n\n");
        await context.Response.Body.FlushAsync();

        logger.LogInformation("Chat stream completed successfully. Chunks sent: {ChunkCount}", messageCount);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during chat stream for user {UserId}", request.UserId ?? "anonymous-user");
        await context.Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n");
        await context.Response.Body.FlushAsync();
    }
}).RequireCors("SSE");
 
app.Run();