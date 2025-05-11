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
        var history = new ChatHistory("You are a helpful travel assistant.");
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
