using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using wander_wallet_chat;

public class ChatService
{
    private readonly IChatCompletionService chatCompletion;
    private readonly ConversationService conversationService;

    public ChatService(IChatCompletionService chatCompletion, ConversationService conversationService)
    {
        this.chatCompletion = chatCompletion;
        this.conversationService = conversationService;
    }

    private ChatHistory BuildChatHistory(string userId)
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

            Your purpose is to make travel easier and more enjoyable for Wander Wallet users. Help them discover, plan, and navigate their journeys with confidence.";


        var history = new ChatHistory(systemPrompt);
        var conversation = conversationService.GetOrCreateConversation(userId);

        foreach (var msg in conversation.Messages)
        {
            if (msg.Role == "user")
                history.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                history.AddAssistantMessage(msg.Content);
        }

        return history;
    }

    public async Task<string> InvokePromptAsync(string userId, string input)
    {
        // Track user message
        conversationService.AddMessage(userId, "user", input);

        var chatHistory = BuildChatHistory(userId);

        // Ask SK to respond using full chat history
        var result = await chatCompletion.GetChatMessageContentAsync(chatHistory);

        // Track assistant reply
        conversationService.AddMessage(userId, "assistant", result.Content);

        return result.Content;
    }

    public async IAsyncEnumerable<string> InvokePromptStreamingAsync(string userId, string input)
    {
        conversationService.AddMessage(userId, "user", input);

        var chatHistory = BuildChatHistory(userId);

        var response = chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory);

        var assistantReply = new StringBuilder();

        await foreach (var chunk in response)
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                assistantReply.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        conversationService.AddMessage(userId, "assistant", assistantReply.ToString());
    }
}
