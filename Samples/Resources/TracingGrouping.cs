using Azure.AI.Agents.Persistent;
using Samples.Common;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Azure.Monitor.OpenTelemetry.Exporter;
using System.Diagnostics;
using Azure;

namespace Samples.Resources;

/// <summary>
/// Demonstrates OpenTelemetry tracing with Azure AI Agents using grouped traces.
/// Groups traces by User Login, MMSID, and Conversation ID.
/// Exports traces to Azure Monitor (Application Insights).
/// Reference: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Agents.Persistent/README.md#tracing
/// </summary>
public class TracingGrouping : Base
{
    private static readonly ActivitySource ActivitySource = new("AgentTracingGroupingSample");

    public TracingGrouping(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "OpenTelemetry Tracing with Grouping";

    public override string Description =>
        "Demonstrates OpenTelemetry tracing with trace grouping by User Login, MMSID, and Conversation ID.";

    public override async Task RunAsync()
    {
        DisplayHeader();
        await RunTracedAgentWithGroupingAsync();
        DisplayFooter();
    }

    /// <summary>
    /// Configures tracing with custom grouping and executes a sample agent interaction.
    /// </summary>
    private async Task RunTracedAgentWithGroupingAsync()
    {
        // Enable experimental Azure SDK observability
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        // Enable content recording in traces (can include sensitive information)
        AppContext.SetSwitch("Azure.Experimental.TraceGenAIMessageContent", true);

        var config = ConfigurationHelper.LoadConfiguration();
        var connectionString = config.ApplicationInsightsConnectionString;

        // Placeholder values for grouping
        var userLogin = "user@example.com"; // Placeholder for actual user login
        var mmsid = "MMSID-12345"; // Placeholder for MMSID

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Azure.AI.Agents.Persistent.*")
            .AddSource("AgentTracingGroupingSample")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("AgentTracingGroupingSample")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["user.login"] = userLogin,
                    ["user.mmsid"] = mmsid
                }))
            .AddAzureMonitorTraceExporter(
                options =>
                {
                    options.ConnectionString = connectionString;
                }
            ).Build();


        using (var activity = ActivitySource.StartActivity($"Agent Interaction - User: {userLogin}, MMSID: {mmsid}"))
        {

            // Create and run the agent based on agent id
            PersistentAgent agent = await AgentClient.Administration.GetAgentAsync("asst_kmYNOylpp5V4DTyWSmsV5kRm");

            // Create a new thread for the agent interaction
            PersistentAgentThread thread = await AgentClient.Threads.CreateThreadAsync();

            // Use thread ID as Conversation ID
            var conversationId = thread.Id;
            
            // Add custom tags for grouping
            activity?.SetTag("user.login", userLogin);
            activity?.SetTag("user.mmsid", mmsid);
            activity?.SetTag("conversation.id", conversationId);

            Console.WriteLine($"Trace Grouping Information:");
            Console.WriteLine($"  User Login: {userLogin}");
            Console.WriteLine($"  MMSID: {mmsid}");
            Console.WriteLine($"  Conversation ID: {conversationId}");
            Console.WriteLine();

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

            // Display all messages in the thread
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
}
