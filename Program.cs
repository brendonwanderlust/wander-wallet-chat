using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using wander_wallet_chat;
using wander_wallet_chat.Plugins;

// Use regular CreateBuilder instead of CreateSlimBuilder
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();

var allowedOrigins = new string[] { "https://localhost", "https://localhost:8100", "http://localhost:8100", "capacitor://localhost" };
var headers = new string[] { "Access-Control-Allow-Origin", "Origin", "Content-Length", "Content-Type", "Authorization" };
var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIURL");
var apiKey = Environment.GetEnvironmentVariable("AzureOpenAIKey");
var deploymentName = Environment.GetEnvironmentVariable("AzureOpenAIDeploymentName");

// Register services
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<ChatHandler>();

builder.Services.AddHttpClient<WeatherPlugin>();
builder.Services.AddTransient<WeatherPlugin>();

// Create and configure the Semantic Kernel
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var kernelBuilder = Kernel.CreateBuilder();

    // Add Azure OpenAI chat completion
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: deploymentName!,
        endpoint: endpoint!,
        apiKey: apiKey!
    );

    // Build the kernel
    var kernel = kernelBuilder.Build();

    // Add the weather plugin
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

// Register IChatCompletionService separately for backwards compatibility
builder.Services.AddSingleton<IChatCompletionService>(serviceProvider =>
{
    var kernel = serviceProvider.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>();
});

// Configure JSON serialization for regular reflection-based approach
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
    HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    try
    {
        await foreach (var chunk in handler.ChatStreaming(request))
        {
            await context.Response.WriteAsync($"data: {chunk}\n\n");
            await context.Response.Body.FlushAsync();
        }

        await context.Response.WriteAsync("event: complete\ndata: \n\n");
        await context.Response.Body.FlushAsync();
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n");
        await context.Response.Body.FlushAsync();
    }
}).RequireCors("SSE");

app.Run();