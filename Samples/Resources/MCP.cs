using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// MCP (Model Context Protocol) integration with Azure AI Foundry.
/// Creates an agent that can use MCP tools to search Microsoft Learn documentation.
/// </summary>
public class MCP : Base
{
    private const string MCP_SERVER_LABEL = "search_mslearn_docs";
    private const string MCP_SERVER_URL = "https://learn.microsoft.com/api/mcp";

    public MCP(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "MCP Integration";

    public override string Description => 
        "Model Context Protocol integration by creating an agent that can search " +
        "Microsoft Learn documentation using MCP tools. The agent will answer questions about " +
        "Azure services by searching official documentation.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // Create MCP tool definition
            Console.WriteLine($"Setting up MCP integration with {MCP_SERVER_URL}...");
            MCPToolDefinition mcpTool = new(MCP_SERVER_LABEL, MCP_SERVER_URL);

            // Create an AI agent with MCP tools
            Console.WriteLine("Creating agent with MCP capabilities...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "mslearn-mcp-agent",
                instructions: "You are a helpful agent that can use MCP tools to assist users. " +
                             "Use the available MCP tools to answer questions and perform tasks. " +
                             "When searching for information, provide comprehensive and accurate responses " +
                             "based on the official Microsoft documentation.",
                tools: [mcpTool]);

            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");

            // Create a conversation thread
            Console.WriteLine("Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");

            // Demonstrate MCP functionality with a sample query
            string userQuestion = "Please summarize the Cosmos DB Per-Region Per Partition Feature";
            Console.WriteLine($"\n[User]: {userQuestion}");
            
            // Create message in thread
            await AgentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                userQuestion);

            // Set up MCP tool resources with auto-approval
            MCPToolResource mcpToolResource = new(MCP_SERVER_LABEL);
            mcpToolResource.RequireApproval = new MCPApproval("never"); // Auto-approve for demo
            ToolResources toolResources = mcpToolResource.ToToolResources();

            // Create and run the agent
            Console.WriteLine("\nProcessing request with MCP tools...");
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread, agent, toolResources);

            // Wait for completion and handle any required actions
            run = await WaitForRunCompletionAsync(thread, run);

            // Check run status
            if (run.Status != RunStatus.Completed)
            {
                Console.WriteLine($"Run did not complete successfully. Status: {run.Status}");
                if (run.LastError != null)
                {
                    Console.WriteLine($"Error: {run.LastError.Message}");
                }
                return;
            }

            // Display the run steps (tool calls made)
            await DisplayRunStepsAsync(run);

            // Display the conversation
            await DisplayMessagesAsync(thread.Id);

            Console.WriteLine("\nMCP completed successfully!");
            Console.WriteLine("The agent successfully used MCP tools to search Microsoft Learn documentation.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during MCP: {ex.Message}");
        }
        finally
        {
            // Clean up resources
            await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Displays the run steps to show what MCP tools were called.
    /// </summary>
    private async Task DisplayRunStepsAsync(ThreadRun run)
    {
        Console.WriteLine("\n=== MCP Tool Calls ===");
        
        IReadOnlyList<RunStep> runSteps = [.. AgentClient.Runs.GetRunSteps(run: run)];
        
        foreach (RunStep step in runSteps)
        {
            if (step.StepDetails is RunStepActivityDetails activityDetails)
            {
                foreach (RunStepDetailsActivity activity in activityDetails.Activities)
                {
                    foreach (KeyValuePair<string, ActivityFunctionDefinition> activityFunction in activity.Tools)
                    {
                        Console.WriteLine($"Function: {activityFunction.Key}");
                        Console.WriteLine($"   Description: {activityFunction.Value.Description}");
                        
                        if (activityFunction.Value.Parameters.Properties.Count > 0)
                        {
                            Console.WriteLine("   Parameters:");
                            foreach (KeyValuePair<string, FunctionArgument> arg in activityFunction.Value.Parameters.Properties)
                            {
                                Console.WriteLine($"     • {arg.Key} ({arg.Value.Type})");
                                if (!string.IsNullOrEmpty(arg.Value.Description))
                                    Console.WriteLine($"       {arg.Value.Description}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("   No parameters required");
                        }
                        Console.WriteLine();
                    }
                }
            }
        }
        
        // Add a small delay to make this method properly async
        await Task.Delay(1);
    }
}