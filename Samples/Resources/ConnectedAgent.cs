using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Connected Agent pattern with MCP integration.
/// Creates a main agent that identifies product names from user questions and delegates 
/// documentation searches to an MCP-based sub-agent.
/// </summary>
public class ConnectedAgent : Base
{
    private const string MCP_SERVER_LABEL = "search_mslearn_docs";
    private const string MCP_SERVER_URL = "https://learn.microsoft.com/api/mcp";

    public ConnectedAgent(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "Connected Agent with MCP";

    public override string Description => 
        "Demonstrates connected agent pattern where the main agent extracts product names " +
        "from user questions and delegates to an MCP-based sub-agent to search Microsoft Learn " +
        "documentation for relevant information about those products.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? mainAgent = null;
        PersistentAgent? subAgent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // Step 1: Create MCP-based sub-agent (documentation search specialist)
            Console.WriteLine($"Step 1: Creating MCP-based documentation search agent...");
            Console.WriteLine($"Setting up MCP integration with {MCP_SERVER_URL}...");
            MCPToolDefinition mcpTool = new(MCP_SERVER_LABEL, MCP_SERVER_URL);

            subAgent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "docs-search-specialist",
                instructions: "You are a Microsoft Learn documentation search specialist. " +
                             "When given a product name and query, use MCP tools to search Microsoft Learn documentation " +
                             "for relevant information. Provide comprehensive answers based on official documentation, " +
                             "including code examples, best practices, and step-by-step guidance when available. " +
                             "Always cite the documentation sources you find.",
                tools: [mcpTool]);

            Console.WriteLine($"Sub-agent created: {subAgent.Name} (ID: {subAgent.Id})");

            // Step 2: Create connected agent tool definition
            Console.WriteLine("\nStep 2: Setting up connected agent tool...");
            ConnectedAgentToolDefinition connectedAgentTool = new(
                new ConnectedAgentDetails(
                    id: subAgent.Id,
                    name: "DocumentationSearcher",
                    description: "A specialized Microsoft Learn documentation search assistant that can find " +
                                "information about Microsoft products and services. Provide the product name and " +
                                "your specific question to get relevant documentation, tutorials, and code examples."
                )
            );

            // Step 3: Create main agent with connected agent tool
            Console.WriteLine("Creating main agent with connected agent tool...");
            mainAgent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "product-inquiry-router",
                instructions: "You are a helpful assistant that routes user questions about Microsoft products " +
                             "to a documentation search specialist. When a user asks about a Microsoft product or service " +
                             "(like Azure, .NET, Visual Studio, etc.), identify the product name and use the DocumentationSearcher " +
                             "tool to find relevant information. Pass both the product name and the user's question to get " +
                             "accurate documentation-based answers.",
                tools: [connectedAgentTool]);

            Console.WriteLine($"Main agent created: {mainAgent.Name} (ID: {mainAgent.Id})");

            // Step 4: Create a conversation thread
            Console.WriteLine("\nStep 3: Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");

            // Step 5: Demonstrate connected agent with a product-specific query
            string userQuestion = "How do I create a serverless function in Azure that responds to HTTP requests?";
            Console.WriteLine($"\n[User]: {userQuestion}");
            Console.WriteLine("Main agent will extract 'Azure' as the product and delegate to documentation searcher...");
            
            // Create message in thread
            await AgentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                userQuestion);

            // Step 6: Set up MCP tool resources with auto-approval for the sub-agent
            MCPToolResource mcpToolResource = new(MCP_SERVER_LABEL);
            mcpToolResource.RequireApproval = new MCPApproval("always"); // Auto-approve for demo
            ToolResources toolResources = mcpToolResource.ToToolResources();

            // Step 7: Create and run the main agent
            Console.WriteLine("\nStep 4: Running main agent (will extract product and delegate to docs searcher)...");
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread, mainAgent, toolResources);

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

            Console.WriteLine("\nConnected agent demonstration completed successfully!");
            Console.WriteLine("The main agent identified the product and delegated documentation search to the sub-agent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during connected agent execution: {ex.Message}");
        }
        finally
        {
            // Clean up resources
            // await CleanupAsync(mainAgent, subAgent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Cleanup method to delete agents and thread.
    /// </summary>
    private async Task CleanupAsync(PersistentAgent? mainAgent, PersistentAgent? subAgent, PersistentAgentThread? thread)
    {
        Console.WriteLine("\nCleaning up resources...");
        
        try
        {
            if (thread != null)
            {
                await AgentClient.Threads.DeleteThreadAsync(thread.Id);
                Console.WriteLine($"Thread deleted: {thread.Id}");
            }

            if (mainAgent != null)
            {
                await AgentClient.Administration.DeleteAgentAsync(mainAgent.Id);
                Console.WriteLine($"Main agent deleted: {mainAgent.Id}");
            }

            if (subAgent != null)
            {
                await AgentClient.Administration.DeleteAgentAsync(subAgent.Id);
                Console.WriteLine($"Sub-agent deleted: {subAgent.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays the run steps to show what tools were called.
    /// </summary>
    private async Task DisplayRunStepsAsync(ThreadRun run)
    {
        Console.WriteLine("\n=== Agent Tool Calls ===");
        
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