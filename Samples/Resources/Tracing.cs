using Azure.AI.Agents.Persistent;
using Samples.Common;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Azure.Monitor.OpenTelemetry.Exporter;
using System.Diagnostics;
using Azure;
using Microsoft.ML.Tokenizers;

namespace Samples.Resources;

/// <summary>
/// Demonstrates OpenTelemetry tracing with Azure AI Agents.
/// Exports traces to Azure Monitor (Application Insights).
/// Reference: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Agents.Persistent/README.md#tracing
/// </summary>
public class TracingAgent : Base
{
    private static readonly ActivitySource ActivitySource = new("AgentTracingSample");
    
    // Tokenizer for GPT-4/GPT-4o models (cl100k_base encoding)
    private static readonly Tokenizer Tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");

    public TracingAgent(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "OpenTelemetry Tracing";

    public override string Description =>
        "Demonstrates OpenTelemetry tracing with Azure Monitor exporter.";

    /// <summary>
    /// Counts tokens in the given text using tiktoken encoding.
    /// </summary>
    private static int CountTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return Tokenizer.CountTokens(text);
    }

    /// <summary>
    /// Counts tokens for chat completion messages following OpenAI's token counting approach.
    /// Reference: https://cookbook.openai.com/examples/how_to_count_tokens_with_tiktoken#6-counting-tokens-for-chat-completions-api-calls
    /// </summary>
    private static int CountTokensForMessages(List<ChatMessage> messages, string model = "gpt-4o")
    {
        // Token overhead per message varies by model
        int tokensPerMessage = 3; // For gpt-4o, gpt-4o-mini, gpt-4, gpt-3.5-turbo
        int tokensPerName = 1;

        int numTokens = 0;
        foreach (var message in messages)
        {
            numTokens += tokensPerMessage;
            numTokens += CountTokens(message.Role);
            numTokens += CountTokens(message.Content);
            if (!string.IsNullOrEmpty(message.Name))
            {
                numTokens += CountTokens(message.Name);
                numTokens += tokensPerName;
            }
        }
        numTokens += 3; // Every reply is primed with <|start|>assistant<|message|>
        return numTokens;
    }

    /// <summary>
    /// Simple message structure for token counting.
    /// </summary>
    private class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public string? Name { get; set; }
    }

    /// <summary>
    /// Gets the individual tokens from the given text.
    /// </summary>
    private static IReadOnlyList<string> GetTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();
        
        var encoded = Tokenizer.EncodeToTokens(text, out _);
        return encoded.Select(t => t.Value).ToList();
    }

    public override async Task RunAsync()
    {
        DisplayHeader();
        await RunTracedAgentAsync();
        DisplayFooter();
    }

    /// <summary>
    /// Executes a sample agent interaction with tracing.
    /// Note: Tracing is initialized globally in Program.cs
    /// </summary>
    private async Task RunTracedAgentAsync()
    {
        using (var activity = ActivitySource.StartActivity("Agent Interaction"))
        {   
            // Create and run the agent based on agent id
            PersistentAgent agent = await AgentClient.Administration.GetAgentAsync("asst_zwzRCLQTx6qwNzm5uJF1jNZx");

            // Create a new thread for the agent interaction
            PersistentAgentThread thread = await AgentClient.Threads.CreateThreadAsync();

            // Send user message to agent
            var userMessage = "Hello! Can you provide a brief overview of Azure Cosmos DB?";
            PersistentThreadMessage agentResponse = await AgentClient.Messages.CreateMessageAsync(
                threadId: thread.Id,
                MessageRole.User,
                userMessage
            );

            // Get thread run
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id);

            // Wait for the run to complete
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                run = await AgentClient.Runs.GetRunAsync(thread.Id, run.Id);
            }
            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

            // Display all messages in the thread with token details
            Console.WriteLine("\n=== Messages ===");
            var inputText = new System.Text.StringBuilder();
            var outputText = new System.Text.StringBuilder();

            await foreach (var message in AgentClient.Messages.GetMessagesAsync(
                threadId: thread.Id,
                order: ListSortOrder.Ascending))
            {
                Console.Write($"{message.CreatedAt:yyyy-MM-dd HH:mm:ss} - {message.Role,10}: ");

                foreach (MessageContent contentItem in message.ContentItems)
                {
                    if (contentItem is MessageTextContent textItem)
                    {
                        Console.Write(textItem.Text);
                        
                        // Track input vs output text
                        if (message.Role == MessageRole.User)
                            inputText.AppendLine(textItem.Text);
                        else if (message.Role == MessageRole.Agent)
                            outputText.AppendLine(textItem.Text);
                    }
                    else if (contentItem is MessageImageFileContent imageFileItem)
                    {
                        Console.Write($"<image from ID: {imageFileItem.FileId}>");
                    }
                }
                Console.WriteLine();
            }

            // Display token usage information
            Console.WriteLine("\n=== Token Usage (API Reported) ===");
            if (run.Usage != null)
            {
                Console.WriteLine($"Prompt Tokens (Input):     {run.Usage.PromptTokens}");
                Console.WriteLine($"Completion Tokens (Output): {run.Usage.CompletionTokens}");
                Console.WriteLine($"Total Tokens:              {run.Usage.TotalTokens}");
            }

            // Local token count using tiktoken with chat message formatting
            var systemInstructions = agent.Instructions ?? "";
            var userInputText = inputText.ToString().TrimEnd();
            var agentOutputText = outputText.ToString().TrimEnd();

            // Build messages list for accurate token counting (following OpenAI cookbook approach)
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(systemInstructions))
            {
                messages.Add(new ChatMessage { Role = "system", Content = systemInstructions });
            }
            messages.Add(new ChatMessage { Role = "user", Content = userInputText });

            var estimatedPromptTokens = CountTokensForMessages(messages);
            var outputTokenCount = CountTokens(agentOutputText);

            // Raw token counts (without message formatting)
            var systemTokenCount = CountTokens(systemInstructions);
            var userTokenCount = CountTokens(userInputText);

            Console.WriteLine("\n=== Token Usage (Local Tiktoken - OpenAI Cookbook Method) ===");
            Console.WriteLine($"System Instructions Tokens: {systemTokenCount}");
            Console.WriteLine($"User Message Tokens:        {userTokenCount}");
            Console.WriteLine($"Message Overhead:           {estimatedPromptTokens - systemTokenCount - userTokenCount} (3 per msg + 3 for assistant prime)");
            Console.WriteLine($"Estimated Prompt Tokens:    {estimatedPromptTokens}");
            Console.WriteLine($"Output Tokens:              {outputTokenCount}");
            Console.WriteLine($"Total (Local):              {estimatedPromptTokens + outputTokenCount}");

            // Compare with API reported tokens
            if (run.Usage != null)
            {
                var promptDiff = run.Usage.PromptTokens - estimatedPromptTokens;
                var outputDiff = run.Usage.CompletionTokens - outputTokenCount;
                Console.WriteLine($"\n--- Comparison with API ---");
                Console.WriteLine($"Prompt Difference:          {promptDiff} tokens (API - Local)");
                Console.WriteLine($"Output Difference:          {outputDiff} tokens (API - Local)");
                if (promptDiff > 0)
                {
                    Console.WriteLine($"Note: Remaining difference may be from thread metadata or internal context.");
                }
            }

            // Display agent system instructions (contributes to prompt tokens)
            Console.WriteLine("\n=== Agent System Instructions ===");
            Console.WriteLine(systemInstructions.Length > 0 ? systemInstructions : "(none)");
            Console.WriteLine($"[Token count: {systemTokenCount}]");

            // Display raw text for validation
            Console.WriteLine("\n=== Input Text (User Messages) ===");
            Console.WriteLine(userInputText);
            Console.WriteLine($"[Token count: {userTokenCount}]");
            Console.WriteLine("\nTokens: " + string.Join(" | ", GetTokens(userInputText)));
            
            Console.WriteLine("\n=== Output Text (Agent Response) ===");
            Console.WriteLine(agentOutputText);
            Console.WriteLine($"[Token count: {outputTokenCount}]");
            // Uncomment below to see individual tokens (can be verbose for long responses)
            // Console.WriteLine("\nTokens: " + string.Join(" | ", GetTokens(agentOutputText)));
            // Console.WriteLine("\nTokens: " + string.Join(" | ", GetTokens(agentOutputText)));
        }
    }
}
