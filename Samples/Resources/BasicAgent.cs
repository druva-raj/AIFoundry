using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Basic agent interaction capabilities.
/// Creates a simple AI agent and demonstrates conversation.
/// </summary>
public class BasicAgent : Base
{
    public BasicAgent(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "Basic Agent Interaction";

    public override string Description => 
        "Basic AI agent creation and conversation. Shows how to create an agent " +
        "with specific instructions and have a simple Q&A interaction.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // Create a basic AI agent
            Console.WriteLine("Creating a basic AI agent...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "BasicDemo-Helper",
                instructions: "You are a helpful AI assistant. You are knowledgeable, friendly, and concise. " +
                             "Answer questions clearly and provide helpful information. " +
                             "If you don't know something, say so honestly.");

            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");

            // Create a conversation thread
            Console.WriteLine("Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");

            // Demonstrate multiple interactions
            await DemoInteraction(thread, agent, 
                "Hello! Can you tell me what you can help me with?");

            await DemoInteraction(thread, agent, 
                "What are the key benefits of using Azure AI services?");

            await DemoInteraction(thread, agent, 
                "Can you explain what makes a good software architecture in 3 key points?");

            // Display full conversation
            await DisplayMessagesAsync(thread.Id);

            Console.WriteLine("\nBasic Agent completed successfully!");
            Console.WriteLine("The agent successfully handled multiple conversational turns.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during basic agent: {ex.Message}");
        }
        finally
        {
            // Clean up resources
            await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Single interaction with the agent.
    /// </summary>
    private async Task DemoInteraction(PersistentAgentThread thread, PersistentAgent agent, string userMessage)
    {
        Console.WriteLine($"\n[User]: {userMessage}");
        
        // Send user message
        await AgentClient.Messages.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            userMessage);

        // Create and run the agent
        Console.WriteLine("Processing...");
        ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id);

        // Wait for completion
        run = await WaitForRunCompletionAsync(thread, run);

        // Check run status
        if (run.Status != RunStatus.Completed)
        {
            Console.WriteLine($"Run did not complete successfully. Status: {run.Status}");
            if (run.LastError != null)
            {
                Console.WriteLine($"Error: {run.LastError.Message}");
            }
        }
        else
        {
            Console.WriteLine("Response generated successfully!");
        }
    }
}