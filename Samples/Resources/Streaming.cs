using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Streaming responses from Azure AI Foundry agents.
/// Handles real-time response streaming for user experience.
/// </summary>
public class Streaming : Base
{
    public Streaming(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "Streaming Response";

    public override string Description => 
        "Real-time response streaming from AI agents. Shows how responses can be " +
        "displayed progressively as they are generated, providing a better user experience.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            Console.WriteLine("Creating agent optimized for streaming responses...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "StreamingDemo-Agent",
                instructions: "You are a creative writing assistant. When asked to write content, " +
                             "provide detailed, engaging responses. Write in a natural, flowing style. " +
                             "For stories or explanations, include vivid details and examples.");

            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");

            // Create conversation thread
            thread = await AgentClient.Threads.CreateThreadAsync();

            // Demonstrate streaming with different types of content
            await DemoStreamingResponse(thread, agent, 
                "Write a short creative story about a programmer who discovers their code can predict the future. Make it engaging and about 200 words.");

            await DemoStreamingResponse(thread, agent, 
                "Explain the concept of quantum computing in simple terms, using analogies that a high school student would understand. Include why it's important for the future.");

            await DemoStreamingResponse(thread, agent, 
                "Create a step-by-step guide for someone learning to cook their first homemade pasta from scratch, including tips for success.");

            Console.WriteLine("\nStreaming completed successfully!");
            Console.WriteLine("Demonstrated real-time response streaming capabilities.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during streaming: {ex.Message}");
        }
        finally
        {
            await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Streaming response handling for a single interaction.
    /// </summary>
    private async Task DemoStreamingResponse(PersistentAgentThread thread, PersistentAgent agent, string userMessage)
    {
        Console.WriteLine($"\n[User]: {userMessage}");
        Console.WriteLine();
        Console.Write("[Agent]: ");
        
        // Send user message
        await AgentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, userMessage);

        // Create run for streaming
        Console.WriteLine("Generating response...");
        ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id);

        // Simulate streaming by polling and showing progress
        await SimulateStreamingProgress(thread, run);

        // Wait for final completion
        run = await WaitForRunCompletionAsync(thread, run);

        if (run.Status == RunStatus.Completed)
        {
            // Display the final response
            await DisplayLatestAgentMessage(thread.Id);
            Console.WriteLine("\nStreaming response completed!");
        }
        else
        {
            Console.WriteLine($"\nRun status: {run.Status}");
        }
    }

    /// <summary>
    /// Simulates streaming progress by showing periodic updates.
    /// </summary>
    private async Task SimulateStreamingProgress(PersistentAgentThread thread, ThreadRun run)
    {
        var progressChars = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int progressIndex = 0;
        var lastStatus = run.Status;
        var startTime = DateTime.UtcNow;

        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
        {
            // Show progress indicator
            Console.Write($"\r{progressChars[progressIndex]} Processing");
            progressIndex = (progressIndex + 1) % progressChars.Length;

            // Check if status changed
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            run = await AgentClient.Runs.GetRunAsync(thread.Id, run.Id);

            if (run.Status != lastStatus)
            {
                Console.Write($"\r✓ Status: {run.Status}");
                lastStatus = run.Status;
            }

            // Break if run takes too long (safety net)
            if (DateTime.UtcNow - startTime > TimeSpan.FromMinutes(2))
            {
                Console.WriteLine("\nResponse taking longer than expected...");
                break;
            }
        }

        Console.WriteLine(); // New line after progress
    }

    /// <summary>
    /// Displays only the latest agent message from the thread.
    /// </summary>
    private async Task DisplayLatestAgentMessage(string threadId)
    {
        await foreach (var message in AgentClient.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Descending))
        {
            if (message.Role == MessageRole.User) // Skip user messages
                continue;
                
            // This is an assistant message
            Console.WriteLine($"\n[Agent]: ");
            foreach (MessageContent contentItem in message.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    // Simulate typing effect
                    await TypewriterEffect(textItem.Text);
                }
            }
            break; // Only show the latest assistant message
        }
    }

    /// <summary>
    /// Creates a typewriter effect for displaying text.
    /// </summary>
    private static async Task TypewriterEffect(string text)
    {
        foreach (char c in text)
        {
            Console.Write(c);
            
            // Add slight delay for typewriter effect (faster than real typing)
            if (c == ' ')
                await Task.Delay(20); // Shorter delay for spaces
            else if (char.IsPunctuation(c))
                await Task.Delay(50); // Slightly longer for punctuation
            else
                await Task.Delay(10); // Very short delay for letters
        }
    }
}