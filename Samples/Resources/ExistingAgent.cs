using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Invoke an existing agent by its ID.
/// Demonstrates connecting to and conversing with a pre-created agent.
/// </summary>
public class ExistingAgent : Base
{
    public ExistingAgent(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "Invoke Existing Agent";

    public override string Description => 
        "Connect to and interact with an existing agent by ID. " +
        "Useful for reusing pre-configured agents without recreating them.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgentThread? thread = null;

        try
        {
            // Replace with your existing agent ID
            var agentId = "asst_JcOvtJ4wU1iL6ktUUKs5F2R5";

            if (string.IsNullOrEmpty(agentId))
            {
                Console.WriteLine("Agent ID cannot be empty.");
                DisplayFooter();
                return;
            }

            // Retrieve the existing agent
            Console.WriteLine($"Retrieving agent with ID: {agentId}...");
            PersistentAgent agent = await AgentClient.Administration.GetAgentAsync(agentId);
            Console.WriteLine($"Agent retrieved: {agent.Name ?? "(unnamed)"} (ID: {agent.Id})");
            Console.WriteLine($"Model: {agent.Model}");
            
            if (!string.IsNullOrEmpty(agent.Instructions))
            {
                var truncatedInstructions = agent.Instructions.Length > 200 
                    ? agent.Instructions.Substring(0, 200) + "..." 
                    : agent.Instructions;
                Console.WriteLine($"Instructions: {truncatedInstructions}");
            }

            // Create a conversation thread
            Console.WriteLine("\nCreating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");

            // Interactive conversation loop
            Console.WriteLine("\nStarting conversation with the agent. Type 'exit' to end.\n");

            while (true)
            {
                Console.Write("[You]: ");
                var userMessage = Console.ReadLine();

                if (string.IsNullOrEmpty(userMessage))
                {
                    continue;
                }

                if (userMessage.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Ending conversation...");
                    break;
                }

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

                // Display the response
                if (run.Status == RunStatus.Completed)
                {
                    await DisplayMessagesAsync(thread.Id);
                }
                else
                {
                    Console.WriteLine($"\n⚠ Run ended with status: {run.Status}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("\nExisting Agent interaction completed successfully!");
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine($"Agent not found. Please verify the agent ID exists.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during existing agent interaction: {ex.Message}");
        }
        finally
        {
            // Clean up thread only (don't delete the existing agent)
            // if (thread != null)
            // {
            //     await CleanupAsync(thread: thread);
            // }
        }

        DisplayFooter();
    }
}
