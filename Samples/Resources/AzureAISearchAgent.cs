using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Azure AI Search Agent Sample
/// Demonstrates how to create an agent that uses Azure AI Search to retrieve information from indexed documents.
/// Based on: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Agents.Persistent/samples/Sample14_PersistentAgents_Azure_AI_Search.md
/// </summary>
public class AzureAISearchAgent : Base
{
    private readonly ConfigurationHelper.AIFoundryConfig _config;

    public AzureAISearchAgent(PersistentAgentsClient agentClient, string modelDeploymentName, ConfigurationHelper.AIFoundryConfig config)
        : base(agentClient, modelDeploymentName)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public override string Name => "Azure AI Search Agent";

    public override string Description =>
        "Demonstrates creating an agent with Azure AI Search capabilities to query indexed documents. " +
        "The agent can search through documents, retrieve relevant information, and provide cited answers " +
        "with links to source documents.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        // Validate Azure AI Search configuration
        if (string.IsNullOrWhiteSpace(_config.AzureAIConnectionId) || 
            string.IsNullOrWhiteSpace(_config.AzureAISearchIndexName))
        {
            Console.WriteLine("WARNING: Azure AI Search is not configured.");
            Console.WriteLine();
            Console.WriteLine("To use this sample, you need to configure:");
            Console.WriteLine("  1. AZURE_AI_CONNECTION_ID - The connection ID for your Azure AI Search resource");
            Console.WriteLine("  2. AZURE_AI_SEARCH_INDEX_NAME - The name of your search index");
            Console.WriteLine();
            Console.WriteLine("Add these to your appsettings.development.json file.");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  \"AZURE_AI_CONNECTION_ID\": \"/subscriptions/{subscription}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{search-service}\"");
            Console.WriteLine("  \"AZURE_AI_SEARCH_INDEX_NAME\": \"sample_index\"");
            DisplayFooter();
            return;
        }

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // Step 1: Create Azure AI Search Tool Resource
            Console.WriteLine("Step 1: Configuring Azure AI Search tool resource...");
            Console.WriteLine($"  Connection ID: {_config.AzureAIConnectionId}");
            Console.WriteLine($"  Index Name: {_config.AzureAISearchIndexName}");
            Console.WriteLine($"  Max Results: 5");
            Console.WriteLine($"  Query Type: Simple");
            Console.WriteLine();

            AzureAISearchToolResource searchResource = new(
                indexConnectionId: _config.AzureAIConnectionId,
                indexName: _config.AzureAISearchIndexName,
                topK: 10,
                filter: null, // No filter applied - searches all documents
                queryType: AzureAISearchQueryType.VectorSemanticHybrid
            );

            ToolResources toolResources = new()
            {
                AzureAISearch = searchResource
            };

            // Step 2: Create Agent with Azure AI Search capabilities
            Console.WriteLine("Step 2: Creating agent with Azure AI Search tool...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "AzureSearchAgent",
                instructions: @"Get the list of Agents based on the user input message from the Knowledge search. Knowledge source has all the agents available.
                                Create a JSON response to list the applicable tasks with the following fields:
                                - AgentTask
                                - AgentName
                                - Parameters
                                - Context
                                - Prerequisites
                                - Sequence

                                Your goal is to extract and list these fields based on user intent from the knowledge base content.

                                If there are any Prerequisites, regenerate the JSON including the dependent task with that prerequisite name.",
                tools: [new AzureAISearchToolDefinition()],
                toolResources: toolResources
            );

            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");
            Console.WriteLine();

            // Step 3: Create conversation thread
            Console.WriteLine("Step 3: Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");
            Console.WriteLine();

            // Step 4: Ask a question that will trigger Azure AI Search
            string userQuestion = "Tasks for listing opportunities";
            Console.WriteLine($"Step 4: Sending user query...");
            Console.WriteLine($"[User]: {userQuestion}");
            Console.WriteLine();

            await AgentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                userQuestion
            );

            // Step 5: Run the agent
            Console.WriteLine("Step 5: Running agent with Azure AI Search...");
            Console.WriteLine("The agent will search the index for relevant documents...");
            Console.WriteLine(); 

            // Option 1: Auto (default) - let the model decide
            // ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread, agent);

            // Option 2: Force the model to use Azure AI Search tool
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(
                threadId: thread.Id,
                assistantId: agent.Id,
                toolChoice: BinaryData.FromObjectAsJson(new
                {
                    type = "azure_ai_search"
                }));

            // Wait for completion
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                run = await AgentClient.Runs.GetRunAsync(thread.Id, run.Id);

                if (run.Status == RunStatus.InProgress)
                {
                    Console.Write(".");
                }
            }
            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

            Console.WriteLine();
            Console.WriteLine();

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

            // Step 6: Display the conversation with citations
            await DisplayMessagesWithCitationsAsync(thread.Id);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Error during Azure AI Search agent execution: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            // Clean up resources
            // await CleanupSearchAgentAsync(agent, thread);
        }

        DisplayFooter();
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

    /// <summary>
    /// Cleanup method to delete agent and thread resources.
    /// </summary>
    private async Task CleanupSearchAgentAsync(PersistentAgent? agent, PersistentAgentThread? thread)
    {
        Console.WriteLine("Cleaning up resources...");

        try
        {
            if (thread != null)
            {
                await AgentClient.Threads.DeleteThreadAsync(thread.Id);
                Console.WriteLine($"Thread deleted: {thread.Id}");
            }

            if (agent != null)
            {
                await AgentClient.Administration.DeleteAgentAsync(agent.Id);
                Console.WriteLine($"Agent deleted: {agent.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Warning during cleanup: {ex.Message}");
        }
    }
}
