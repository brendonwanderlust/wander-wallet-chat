using System.Text.Json.Serialization;

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
        policy.WithOrigins(allowedOrigins);
        policy.WithHeaders(headers);  
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCors();

var chatApi = app.MapGroup("/chat");
chatApi.MapGet("/", () => "Hello, this is your wander wallet travel buddy. How can I help you today?");
app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
