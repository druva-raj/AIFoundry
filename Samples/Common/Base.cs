using Azure.AI.Agents.Persistent;

namespace Samples.Common;

/// <summary>
/// Abstract base class for all AI Foundry samples.
/// Common functionality and consistent structure across all samples.
/// </summary>
public abstract class Base
{
    protected readonly PersistentAgentsClient AgentClient;
    protected readonly string ModelDeploymentName;

    protected Base(PersistentAgentsClient agentClient, string modelDeploymentName)
    {
        AgentClient = agentClient ?? throw new ArgumentNullException(nameof(agentClient));
        ModelDeploymentName = modelDeploymentName ?? throw new ArgumentNullException(nameof(modelDeploymentName));
    }

    /// <summary>
    /// Gets the name of the sample for display purposes.
    /// </summary>
    public abstract string Name { get;}

    /// <summary>
    /// Gets a description of what this sample showcases.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Runs the sample asynchronously.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public abstract Task RunAsync();

    /// <summary>
    /// Displays a header for the sample.
    /// </summary>
    protected void DisplayHeader()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"SAMPLE: {Name}");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Description: {Description}");
        Console.WriteLine();
    }

    /// <summary>
    /// Displays a footer and waits for user input before continuing.
    /// </summary>
    protected void DisplayFooter()
    {
        Console.WriteLine();
        Console.WriteLine(new string('-', 60));
        Console.WriteLine("Sample completed! Press any key to return to the main menu...");
        Console.ReadKey();
        Console.Clear();
    }

    /// <summary>
    /// Waits for a run to complete, displaying progress updates.
    /// </summary>
    /// <param name="thread">The thread containing the run.</param>
    /// <param name="run">The run to monitor.</param>
    /// <param name="maxWaitTime">Maximum time to wait before timing out.</param>
    /// <returns>The completed run.</returns>
    protected async Task<ThreadRun> WaitForRunCompletionAsync(
        PersistentAgentThread thread, 
        ThreadRun run, 
        TimeSpan? maxWaitTime = null)
    {
        maxWaitTime ??= TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        while (run.Status == RunStatus.Queued || 
               run.Status == RunStatus.InProgress || 
               run.Status == RunStatus.RequiresAction)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1000));
            run = await AgentClient.Runs.GetRunAsync(thread.Id, run.Id);

            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
            {
                var toolApprovals = new List<ToolApproval>();
                foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                {
                    if (toolCall is RequiredMcpToolCall mcpToolCall)
                    {
                        Console.WriteLine($"Approving MCP tool call: {mcpToolCall.Name}");
                        toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                    }
                }

                if (toolApprovals.Count > 0)
                {
                    run = await AgentClient.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolApprovals: toolApprovals);
                }
            }

            if (run.Status == RunStatus.InProgress)
            {
                Console.WriteLine("Run is in progress...");
                
                // Check for timeout
                if (DateTime.UtcNow - startTime > maxWaitTime)
                {
                    Console.WriteLine("Run is taking too long. Cancelling the run.");
                    await AgentClient.Runs.CancelRunAsync(thread.Id, run.Id);
                    break;
                }
            }
        }

        return run;
    }

    /// <summary>
    /// Prints all messages in a thread to the console.
    /// </summary>
    /// <param name="threadId">The ID of the thread to display messages from.</param>
    protected async Task DisplayMessagesAsync(string threadId)
    {
        Console.WriteLine("\n=== Conversation ===");
        
        await foreach (var message in AgentClient.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Ascending))
        {
            Console.Write($"{message.CreatedAt:yyyy-MM-dd HH:mm:ss} - {message.Role,10}: ");
            
            foreach (MessageContent contentItem in message.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    Console.Write(textItem.Text);
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
    /// Safely cleans up agent and thread resources.
    /// </summary>
    /// <param name="agent">The agent to delete (optional).</param>
    /// <param name="thread">The thread to delete (optional).</param>
    protected async Task CleanupAsync(PersistentAgent? agent = null, PersistentAgentThread? thread = null)
    {
        try
        {
            if (thread != null)
            {
                Console.WriteLine("Cleaning up thread...");
                await AgentClient.Threads.DeleteThreadAsync(threadId: thread.Id);
            }

            if (agent != null)
            {
                Console.WriteLine("Cleaning up agent...");
                await AgentClient.Administration.DeleteAgentAsync(agentId: agent.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during cleanup: {ex.Message}");
        }
    }
}