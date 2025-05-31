using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using wander_wallet_chat;
using wander_wallet_chat.Plugins;

var builder = WebApplication.CreateSlimBuilder(args);
var allowedOrigins = new string[] { "https://localhost", "https://localhost:8100", "http://localhost:8100", "capacitor://localhost" };
var headers = new string[] { "Access-Control-Allow-Origin", "Origin", "Content-Length", "Content-Type", "Authorization" };
var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIURL");
var apiKey = Environment.GetEnvironmentVariable("AzureOpenAIKey");
var deploymentName = Environment.GetEnvironmentVariable("AzureOpenAIDeploymentName");

// Register HttpClient for weather API calls
builder.Services.AddHttpClient<WeatherPlugin>();

// Create and configure the Semantic Kernel
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
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
    var httpClient = serviceProvider.GetRequiredService<HttpClient>();
    var weatherPlugin = new WeatherPlugin(httpClient);
    kernel.Plugins.AddFromObject(weatherPlugin, "WeatherPlugin");

    return kernel;
});

builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ChatHandler>();
builder.Services.AddSingleton<IChatCompletionService>(serviceProvider =>
{
    var kernel = serviceProvider.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>();
});

//// Configure JSON for AOT + camelCase + metadata
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default;
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
public record ReplyResponse(string Reply);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(ReplyResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}



