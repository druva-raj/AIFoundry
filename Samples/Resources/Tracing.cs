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
/// Demonstrates OpenTelemetry tracing with Azure AI Agents.
/// Exports traces to Azure Monitor (Application Insights).
/// Reference: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Agents.Persistent/README.md#tracing
/// </summary>
public class TracingAgent : Base
{
    private static readonly ActivitySource ActivitySource = new("AgentTracingSample");

    public TracingAgent(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "OpenTelemetry Tracing";

    public override string Description =>
        "Demonstrates OpenTelemetry tracing with Azure Monitor exporter.";

    public override async Task RunAsync()
    {
        DisplayHeader();
        await RunTracedAgentAsync();
        DisplayFooter();
    }

    /// <summary>
    /// Configures tracing and executes a sample agent interaction.
    /// </summary>
    private async Task RunTracedAgentAsync()
    {
        // Enable experimental Azure SDK observability
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        // Enable content recording in traces (can include sensitive information)
        AppContext.SetSwitch("Azure.Experimental.TraceGenAIMessageContent", true);

        var config = ConfigurationHelper.LoadConfiguration();
        var connectionString = config.ApplicationInsightsConnectionString;

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Azure.AI.Agents.Persistent.*")
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AgentTracingSample"))
            .AddAzureMonitorTraceExporter(
                options =>
                {
                    options.ConnectionString = connectionString;
                }
            ).Build();

        // Create and run the agent based on agent id
        PersistentAgent agent = await AgentClient.Administration.GetAgentAsync("asst_kmYNOylpp5V4DTyWSmsV5kRm");

        using (var activity = ActivitySource.StartActivity("Agent Interaction"))
        {   
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
