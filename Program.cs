using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);
var corsOriginsPolicy = "WanderWalletCorsOriginsPolicy";
var allowedOrigins = new string[] { "https://localhost", "https://localhost:8100", "http://localhost:8100", "capacitor://localhost" };
var headers = new string[] { "Origin", "Content-Length", "Content-Type", "Authorization", "Access-Control-Allow-Origin" };

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsOriginsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins);
        //policy.WithHeaders(headers);
        policy.AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors(corsOriginsPolicy);

var todosApi = app.MapGroup("/chat");
todosApi.MapGet("/", () => "Hello, this is your wander wallet travel buddy. How can I help you today?");
app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
