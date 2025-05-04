using Microsoft.SemanticKernel;

namespace wander_wallet_chat
{
    public class ChatService
    {
        private readonly Kernel kernel;

        public ChatService() 
        {
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

        public async Task<string> InvokePromptAsync(string message)
        {
            var result = await kernel.InvokePromptAsync(message);
            return result.GetValue<string>();
        }
    }
}
