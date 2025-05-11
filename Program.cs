using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using wander_wallet_chat;

var builder = WebApplication.CreateSlimBuilder(args);
var allowedOrigins = new string[] { "https://localhost", "https://localhost:8100", "http://localhost:8100", "capacitor://localhost" };
var headers = new string[] { "Access-Control-Allow-Origin", "Origin", "Content-Length", "Content-Type", "Authorization" };

builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ChatHandler>();

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
chatApi.MapGet("/", async (
    [FromQuery] string message,
    [FromQuery] string userId,
    ChatHandler handler) =>
{
    var reply = await handler.Chat(userId, message);
    return Results.Ok(reply);
});

chatApi.MapGet("/stream", async (
    [FromQuery] string message,
    [FromQuery] string userId,
    ChatHandler handler,
    HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    try
    {
        await foreach (var chunk in handler.ChatStreaming(userId, message))
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



