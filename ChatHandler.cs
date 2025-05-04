namespace wander_wallet_chat
{
    public class ChatHandler
    {
        private readonly ChatService chatService;

        public ChatHandler(ChatService chatService)
        {
            this.chatService = chatService;
        }

        public async Task<string> Chat(string message) {
            return await chatService.InvokePromptAsync(message);
        }
    }
}
