using Microsoft.SemanticKernel;

namespace wander_wallet_chat
{
    public class ChatService
    {
        private readonly Kernel kernel;
        private readonly ConversationService conversationService;

        public ChatService(ConversationService conversationService)
        {
            this.conversationService = conversationService;

            var builder = Kernel.CreateBuilder();

            var endpoint = Environment.GetEnvironmentVariable("AzureOpenAIURL");
            var apiKey = Environment.GetEnvironmentVariable("AzureOpenAIKey");
            var deploymentName = Environment.GetEnvironmentVariable("AzureOpenAIDeploymentName");

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
            {
                throw new InvalidOperationException("Required Azure OpenAI configuration is missing");
            }

            builder.AddAzureOpenAIChatCompletion(
                deploymentName: deploymentName,
                endpoint: endpoint,
                apiKey: apiKey
            );

            kernel = builder.Build();
        }

        public async Task<string> InvokePromptAsync(string userId, string message)
        {
            // Add user message to conversation history
            conversationService.AddMessage(userId, "user", message);

            // Get conversation history
            var history = conversationService.GetFormattedHistory(userId);

            // Create prompt with history context
            var prompt = $"Previous conversation:\n{history}\n\nUser: {message}\nAssistant:";

            // Invoke AI model
            var result = await kernel.InvokePromptAsync(prompt);
            var response = result.GetValue<string>();

            // Add AI response to conversation history
            conversationService.AddMessage(userId, "assistant", response);

            return response;
        }

        public async IAsyncEnumerable<string> InvokePromptStreamingAsync(string userId, string message)
        {
            // Add user message to conversation history
            conversationService.AddMessage(userId, "user", message);

            // Get conversation history
            var history = conversationService.GetFormattedHistory(userId);

            // Create prompt with history context
            var prompt = $"Previous conversation:\n{history}\n\nUser: {message}\nAssistant:";

            string fullResponse = "";

            await foreach (var chunk in kernel.InvokePromptStreamingAsync(prompt))
            {
                var text = chunk.ToString();
                fullResponse += text;
                yield return text;
            }

            // Add complete AI response to conversation history
            conversationService.AddMessage(userId, "assistant", fullResponse);
        }
    }
}