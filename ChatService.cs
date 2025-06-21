using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using wander_wallet_chat;
using System.Diagnostics;

public class ChatService
{
    private readonly Kernel _kernel;
    private readonly ConversationService _conversationService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(Kernel kernel, ConversationService conversationService, ILogger<ChatService> logger)
    {
        _kernel = kernel;
        _conversationService = conversationService;
        _logger = logger;
    }

    private ChatHistory BuildChatHistory(string userId, ChatRequest? request = null)
    {
        var systemPrompt = BuildSystemPrompt(request);
        var history = new ChatHistory(systemPrompt);
        var conversation = _conversationService.GetOrCreateConversation(userId);

        foreach (var msg in conversation.Messages)
        {
            if (msg.Role == "user")
                history.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                history.AddAssistantMessage(msg.Content);
        }

        return history;
    }

    private string BuildSystemPrompt(ChatRequest? request)
    {
        var systemPrompt = @"
            You are Wander Wallet's friendly travel assistant, designed to help travelers with their journey.

            IMPORTANT GUIDELINES:
            - Always keep responses travel-related and focused on helping the user plan, navigate, or enjoy their travels.
            - Keep responses concise and to the point - travelers are often busy and need quick, actionable information.
            - Maintain a friendly, conversational tone that makes travelers feel supported.
            - Consider practical traveler needs like transportation, accommodation, local customs, safety tips, and budget considerations.
            - Provide specific recommendations when appropriate, not just general advice.
            - If asked about costs, always clarify which currency you're referring to.
            - When suggesting activities, consider weather and seasonal factors.
            - If you don't know something specific about a location, acknowledge that rather than providing potentially incorrect information.
            - Never respond to sexually explicit requests or non-travel related inappropriate content.
            - Do not share personal opinions on sensitive political, religious, or cultural topics.

            WEATHER INFORMATION:
            - When users ask about weather in any location, use the get_weather function to provide current and forecast information.
            - Always consider weather when making travel recommendations.
            - Mention how weather might affect planned activities.

            Your purpose is to make travel easier and more enjoyable for Wander Wallet users. Help them discover, plan, and navigate their journeys with confidence.";

        // Add user context if available
        if (request != null)
        {
            var contextInfo = new StringBuilder();

            if (request.MeasurementSystem != MeasurementSystem.Imperial)
            {
                contextInfo.AppendLine($"User prefers {request.MeasurementSystem.ToString().ToLower()} measurements (Celsius, kilometers, etc.).");
            }
            else
            {
                contextInfo.AppendLine("User prefers imperial measurements (Fahrenheit, miles, etc.).");
            }

            if (request.Activities?.Any() == true)
            {
                contextInfo.AppendLine($"User is interested in these activities: {string.Join(", ", request.Activities)}.");
            }

            if (request.Latitude != 0 && request.Longitude != 0)
            {
                contextInfo.AppendLine($"User's approximate location: {request.Latitude:F2}, {request.Longitude:F2}.");
            }

            if (contextInfo.Length > 0)
            {
                systemPrompt += "\n\nUSER CONTEXT:\n" + contextInfo.ToString();
            }
        }

        return systemPrompt;
    }

    public async Task<string> InvokePromptAsync(string userId, string input, ChatRequest? request = null)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Processing chat request for user {UserId} and input {Input}", userId, input);

        try
        {
            // Track user message
            _conversationService.AddMessage(userId, "user", input);

            var chatHistory = BuildChatHistory(userId, request);

            // Configure execution settings to enable automatic function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.7,
                MaxTokens = 1000
            };

            // Get the chat completion service from the kernel
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

            // Ask SK to respond using full chat history with function calling enabled
            var result = await chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // Track assistant reply
            _conversationService.AddMessage(userId, "assistant", result.Content ?? "");

            stopwatch.Stop();
            _logger.LogInformation("Chat request completed for user {UserId} in {ElapsedMs}ms", userId, stopwatch.ElapsedMilliseconds);

            return result.Content ?? "";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing chat request for user {UserId} after {ElapsedMs}ms", userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async IAsyncEnumerable<string> InvokePromptStreamingAsync(string userId, string input, ChatRequest? request = null)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting streaming chat for user {UserId}", userId);
         
        IAsyncEnumerable<StreamingChatMessageContent> response;
        try
        {
            _conversationService.AddMessage(userId, "user", input);

            var chatHistory = BuildChatHistory(userId, request);
             
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.7,
                MaxTokens = 1000
            };

            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            response = chatCompletion.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error setting up streaming chat for user {UserId} after {ElapsedMs}ms", userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
         
        var assistantReply = new StringBuilder();

        await foreach (var chunk in response)
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                assistantReply.Append(chunk.Content);
                yield return chunk.Content;
            }
        }
         
        stopwatch.Stop();
        _conversationService.AddMessage(userId, "assistant", assistantReply.ToString());
        _logger.LogInformation("Streaming chat completed for user {UserId} in {ElapsedMs}ms", userId, stopwatch.ElapsedMilliseconds);
    }
     
    public async Task<string> InvokePromptAsync(string userId, string input)
    {
        return await InvokePromptAsync(userId, input, null);
    }

    public async IAsyncEnumerable<string> InvokePromptStreamingAsync(string userId, string input)
    {
        await foreach (var chunk in InvokePromptStreamingAsync(userId, input, null))
        {
            yield return chunk;
        }
    }
}