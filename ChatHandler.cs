namespace wander_wallet_chat
{
    public class ChatHandler
    {
        private readonly ChatService chatService;

        public ChatHandler(ChatService chatService)
        {
            this.chatService = chatService;
        }

        public async Task<string> Chat(string userId, string message)
        {
            // Default to a generic user ID if none provided
            if (string.IsNullOrEmpty(userId))
            {
                userId = "anonymous-user";
            }

            return await chatService.InvokePromptAsync(userId, message);
        }

        public async IAsyncEnumerable<string> ChatStreaming(ChatRequest request)
        { 
            if (string.IsNullOrEmpty(request.UserId))
            {
                request.UserId = "anonymous-user";
            }
             
            await foreach (var chunk in chatService.InvokePromptStreamingAsync(request.UserId, request.Message, request))
            {
                yield return chunk;
            }
        }
    }
}