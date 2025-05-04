using System.Text.Json.Serialization;
using DotNetEnv;
using wander_wallet_chat;

var builder = WebApplication.CreateSlimBuilder(args); 
var allowedOrigins = new string[] { "https://localhost", "https://localhost:8100", "http://localhost:8100", "capacitor://localhost" };
var headers = new string[] { "Access-Control-Allow-Origin", "Origin", "Content-Length", "Content-Type", "Authorization" }; 

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
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
}); 

var app = builder.Build();

app.UseCors();

//app.Use(async (context, next) =>
//{
//    // Handle preflight requests for SSE
//    if (context.Request.Method == "OPTIONS")
//    {
//        context.Response.Headers.Add("Access-Control-Allow-Origin", context.Request.Headers["Origin"].ToString());
//        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
//        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, Accept");
//        context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
//        context.Response.StatusCode = 200;
//        return;
//    }

//    await next();
//});

app.UseRouting();

if (builder.Environment.IsDevelopment())
{
    Env.Load();
}

var chatApi = app.MapGroup("/chat");
chatApi.MapGet("/", async (string message) =>{
    var chatService = new ChatService();
    var handler = new ChatHandler(chatService);
    return await handler.Chat(message);
});

chatApi.MapGet("/stream", async (string message, ChatHandler handler, HttpContext context) => {     
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("Connection", "keep-alive"); 

    try
    {
        await foreach (var chunk in handler.ChatStreaming(message))
        {
            // Server-Sent Events format
            await context.Response.WriteAsync($"data: {chunk}\n\n");
            await context.Response.Body.FlushAsync();
        }

        // Send completion event
        await context.Response.WriteAsync("event: complete\ndata: \n\n");
        await context.Response.Body.FlushAsync();
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n");
        await context.Response.Body.FlushAsync();
    }
});

app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
