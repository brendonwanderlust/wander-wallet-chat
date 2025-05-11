using System.Collections.Concurrent;
using System.Text;

namespace wander_wallet_chat
{
    public class ConversationService
    {
        // In-memory store for conversations (in production, you'd use a database)
        private readonly ConcurrentDictionary<string, Conversation> _conversations = new();

        // Get or create a conversation for a user
        public Conversation GetOrCreateConversation(string userId)
        {
            return _conversations.GetOrAdd(userId, (id) => new Conversation { UserId = id });
        }

        // Add a message to a conversation
        public void AddMessage(string userId, string role, string content)
        {
            var conversation = GetOrCreateConversation(userId);
            conversation.Messages.Add(new Message { Role = role, Content = content });
            conversation.UpdatedAt = DateTime.UtcNow;
        }

        // Get conversation history as a formatted string for the AI
        public string GetFormattedHistory(string userId)
        {
            var conversation = GetOrCreateConversation(userId);

            if (conversation.Messages.Count == 0)
                return string.Empty;

            var formattedHistory = new StringBuilder();
            foreach (var message in conversation.Messages)
            {
                formattedHistory.AppendLine($"{message.Role}: {message.Content}");
            }

            return formattedHistory.ToString();
        }
    }
}
