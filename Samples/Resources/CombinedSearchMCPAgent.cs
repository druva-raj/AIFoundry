using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Combined Azure AI Search and MCP Agent Sample
/// Demonstrates how to create an agent that uses both Azure AI Search and MCP tools together.
/// The agent can search indexed documents AND use MCP tools to access Microsoft Learn documentation.
/// </summary>
public class CombinedSearchMCPAgent : Base
{
    private readonly ConfigurationHelper.AIFoundryConfig _config;
    private const string MCP_SERVER_LABEL = "search_mslearn_docs";
    private const string MCP_SERVER_URL = "https://learn.microsoft.com/api/mcp";
    private const string AGENT_NAME = "combined-search-mcp-agent";

    public CombinedSearchMCPAgent(PersistentAgentsClient agentClient, string modelDeploymentName, ConfigurationHelper.AIFoundryConfig config)
        : base(agentClient, modelDeploymentName)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override string Name => "Combined AI Search + MCP Agent";

    public override string Description =>
        "Demonstrates creating an agent with both Azure AI Search and MCP capabilities. " +
        "The agent can search through indexed documents in Azure AI Search AND use MCP tools " +
        "to access Microsoft Learn documentation, providing comprehensive answers from multiple sources.";

    public override async Task RunAsync()
    {
        DisplayHeader();
        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // Configure Azure AI Search tool resource
            AzureAISearchToolResource searchResource = new(
                indexConnectionId: _config.AzureAIConnectionId,
                indexName: _config.AzureAISearchIndexName,
                topK: 10,
                filter: null,
                queryType: AzureAISearchQueryType.VectorSemanticHybrid
            );

            ToolResources toolResources = new()
            {
                AzureAISearch = searchResource
            };

            // Configure MCP tool
            Console.WriteLine($"Configuring MCP tool with {MCP_SERVER_URL}...");
            MCPToolDefinition mcpTool = new(MCP_SERVER_LABEL, MCP_SERVER_URL);
            Console.WriteLine();

            // Create agent with BOTH tools
            Console.WriteLine("Creating agent with Azure AI Search AND MCP capabilities...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: AGENT_NAME,
                instructions: @"You are a helpful AI agent with access to multiple knowledge sources:

                                1. Azure AI Search - Contains indexed documents with information about agents, tasks, and internal knowledge
                                2. MCP Tools - Provides access to Microsoft Learn documentation for official Azure service information

                                When answering questions:
                                - Use Azure AI Search for queries about internal tasks, agents, prerequisites, and workflows
                                - Use MCP tools for queries about Azure services, features, and official documentation
                                - If a question spans both areas, use both tools and synthesize the information
                                - Always cite your sources and indicate which tool provided the information
                                - For Azure AI Search results, create JSON responses when listing tasks with fields: AgentTask, AgentName, Parameters, Context, Prerequisites, Sequence
                                - For complex queries, break them down and use the appropriate tool for each part

                                Provide comprehensive, accurate answers based on the available tools.",
                tools: [new AzureAISearchToolDefinition(), mcpTool],
                toolResources: toolResources
            );

            Console.WriteLine($"Agent created successfully: {agent.Name}");
            
            Console.WriteLine($"Using agent: {agent.Name} (ID: {agent.Id})");
            Console.WriteLine();

            // Step 2: Create conversation thread
            Console.WriteLine("Step 2: Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");
            Console.WriteLine();

            // Step 3: Ask a question that can benefit from both tools
            string userQuestion = "What are the tasks for listing opportunities in the knowledge base, and also explain Azure Cosmos DB partitioning from Microsoft Learn?";
            Console.WriteLine($"Step 3: Sending user query...");
            Console.WriteLine($"[User]: {userQuestion}");
            Console.WriteLine();

            await AgentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                userQuestion
            );

            // Step 4: Set up MCP tool resources with auto-approval
            Console.WriteLine("Step 4: Configuring MCP tool resources...");
            MCPToolResource mcpToolResource = new(MCP_SERVER_LABEL);
            mcpToolResource.RequireApproval = new MCPApproval("never"); // Auto-approve for demo
            ToolResources runToolResources = mcpToolResource.ToToolResources();
            Console.WriteLine("MCP auto-approval configured");
            Console.WriteLine();

            // Step 5: Run the agent with both tools
            Console.WriteLine("Step 5: Running agent with AI Search and MCP tools...");
            Console.WriteLine("The agent will use both Azure AI Search and MCP as needed...");
            Console.WriteLine();

            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread, agent, runToolResources);

            // Wait for completion
            run = await WaitForRunCompletionAsync(thread, run);

            // Check run status
            if (run.Status != RunStatus.Completed)
            {
                Console.WriteLine($"ERROR: Run did not complete successfully. Status: {run.Status}");
                if (run.LastError != null)
                {
                    Console.WriteLine($"Error: {run.LastError.Message}");
                }
                return;
            }

            Console.WriteLine("Run completed successfully!");
            Console.WriteLine();

            // Step 6: Display the run steps to see which tools were used
            await DisplayRunStepsAsync(run);

            // Step 7: Display the conversation with citations
            await DisplayMessagesWithCitationsAsync(thread.Id);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error during combined agent execution: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            // Clean up thread only (keep agent for reuse)
            if (thread != null)
            {
                // await CleanupAsync(thread: thread);
            }
        }

        DisplayFooter();
    }

    /// <summary>
    /// Displays the run steps to show which tools (AI Search, MCP, or both) were called.
    /// </summary>
    private async Task DisplayRunStepsAsync(ThreadRun run)
    {
        Console.WriteLine("=== Tool Usage Summary ===");
        Console.WriteLine();

        bool foundAISearch = false;
        bool foundMCP = false;
        
        IReadOnlyList<RunStep> runSteps = [.. AgentClient.Runs.GetRunSteps(run: run)];
        
        foreach (RunStep step in runSteps)
        {
            if (step.StepDetails is RunStepActivityDetails activityDetails)
            {
                foreach (RunStepDetailsActivity activity in activityDetails.Activities)
                {
                    foreach (KeyValuePair<string, ActivityFunctionDefinition> activityFunction in activity.Tools)
                    {
                        foundMCP = true;
                        Console.WriteLine("✓ MCP Tool Used");
                        Console.WriteLine($"  Function: {activityFunction.Key}");
                        Console.WriteLine($"  Description: {activityFunction.Value.Description}");
                        Console.WriteLine();
                    }
                }
            }
        }

        if (!foundAISearch && !foundMCP)
        {
            Console.WriteLine("No tools were used in this run.");
        }

        Console.WriteLine();
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Displays messages with properly formatted citations from Azure AI Search results.
    /// </summary>
    private async Task DisplayMessagesWithCitationsAsync(string threadId)
    {
        Console.WriteLine("=== Conversation with Citations ===");
        Console.WriteLine();

        await foreach (var message in AgentClient.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Ascending))
        {
            Console.Write($"{message.CreatedAt:yyyy-MM-dd HH:mm:ss} - {message.Role,10}: ");

            foreach (MessageContent contentItem in message.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    // Process annotations for Agent messages to show citations
                    if (message.Role == MessageRole.Agent && textItem.Annotations.Count > 0)
                    {
                        string annotatedText = textItem.Text;
                        
                        foreach (MessageTextAnnotation annotation in textItem.Annotations)
                        {
                            if (annotation is MessageTextUriCitationAnnotation uriAnnotation)
                            {
                                // Replace citation markers with formatted links
                                annotatedText = annotatedText.Replace(
                                    uriAnnotation.Text,
                                    $" [see {uriAnnotation.UriCitation.Title}]({uriAnnotation.UriCitation.Uri})");
                            }
                        }
                        
                        Console.Write(annotatedText);
                    }
                    else
                    {
                        Console.Write(textItem.Text);
                    }
                }
                else if (contentItem is MessageImageFileContent imageFileItem)
                {
                    Console.Write($"<image from ID: {imageFileItem.FileId}>");
                }
            }
            Console.WriteLine();
        }
    }
}
