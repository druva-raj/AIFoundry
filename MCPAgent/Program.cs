using System.Reflection;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

// Reference - https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Agents.Persistent/samples/Sample32_PersistentAgents_MCP.md

var configRoot = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

string projectEndpoint = configRoot["PROJECT_ENDPOINT"] ?? throw new InvalidOperationException("PROJECT_ENDPOINT is not configured");
string modelDeploymentName = configRoot["MODEL_DEPLOYMENT_NAME"] ?? throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME is not configured");
string tenantId = configRoot["TENANT_ID"] ?? throw new InvalidOperationException("TENANT_ID is not configured");
string clientId = configRoot["CLIENT_ID"] ?? throw new InvalidOperationException("CLIENT_ID is not configured");
string clientSecret = configRoot["CLIENT_SECRET"] ?? throw new InvalidOperationException("CLIENT_SECRET is not configured");

string mcpServerLabel = "search_mslearn_docs";
string mcpServerUrl = "https://learn.microsoft.com/api/mcp";

// Initialize the client with Service Principal credentials
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

PersistentAgentsClient agentClient = new(projectEndpoint, new DefaultAzureCredential());

MCPToolDefinition mcpTool = new(mcpServerLabel, mcpServerUrl);

PersistentAgent agent = await agentClient.Administration.CreateAgentAsync(
   model: modelDeploymentName,
   name: "mslearn-mcp-agent",
   instructions: "You are a helpful agent that can use MCP tools to assist users. Use the available MCP tools to answer questions and perform tasks.",
   tools: [mcpTool]
   );

PersistentAgentThread thread = await agentClient.Threads.CreateThreadAsync();

// Create message to thread
PersistentThreadMessage message = await agentClient.Messages.CreateMessageAsync(
    thread.Id,
    MessageRole.User,
    "Please summarize the Cosmos DB Per-Region Per Partition Feature");

MCPToolResource mcpToolResource = new(mcpServerLabel);
// By default all the tools require approvals. To set the absolute trust for the tool please uncomment the
// next code.
mcpToolResource.RequireApproval = new MCPApproval("never");

ToolResources toolResources = mcpToolResource.ToToolResources();

// Run the agent with MCP tool resources
ThreadRun run = await agentClient.Runs.CreateRunAsync(thread, agent, toolResources);

// Handle run execution and tool approvals
while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
{
    await Task.Delay(TimeSpan.FromMilliseconds(1000));
    run = await agentClient.Runs.GetRunAsync(thread.Id, run.Id);

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
            run = await agentClient.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolApprovals: toolApprovals);
        }
    }
}

// Verify run completed successfully
if (run.Status != RunStatus.Completed)
{
    Console.WriteLine($"Run did not complete successfully. Status: {run.Status}");
    if (run.LastError != null)
    {
        Console.WriteLine($"Error: {run.LastError.Message}");
    }
}

IReadOnlyList<RunStep> runSteps = [.. agentClient.Runs.GetRunSteps(run: run)];
PrintActivitySteps(runSteps);

List<PersistentThreadMessage> messagesList = [];
await foreach (var msg in agentClient.Messages.GetMessagesAsync(
    threadId: thread.Id,
    order: ListSortOrder.Ascending))
{
    messagesList.Add(msg);
}

PrintMessages(messagesList);

static void PrintActivitySteps(IReadOnlyList<RunStep> runSteps)
{
    foreach (RunStep step in runSteps)
    {
        if (step.StepDetails is RunStepActivityDetails activityDetails)
        {
            foreach (RunStepDetailsActivity activity in activityDetails.Activities)
            {
                foreach (KeyValuePair<string, ActivityFunctionDefinition> activityFunction in activity.Tools)
                {
                    Console.WriteLine($"The function {activityFunction.Key} with description \"{activityFunction.Value.Description}\" will be called.");
                    if (activityFunction.Value.Parameters.Properties.Count > 0)
                    {
                        Console.WriteLine("Function parameters:");
                        foreach (KeyValuePair<string, FunctionArgument> arg in activityFunction.Value.Parameters.Properties)
                        {
                            Console.WriteLine($"\t{arg.Key}");
                            Console.WriteLine($"\t\tType: {arg.Value.Type}");
                            if (!string.IsNullOrEmpty(arg.Value.Description))
                                Console.WriteLine($"\t\tDescription: {arg.Value.Description}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("This function has no parameters");
                    }
                }
            }
        }
    }
}

static void PrintMessages(IReadOnlyList<PersistentThreadMessage> messages)
{
    foreach (PersistentThreadMessage threadMessage in messages)
    {
        Console.Write($"{threadMessage.CreatedAt:yyyy-MM-dd HH:mm:ss} - {threadMessage.Role,10}: ");
        foreach (MessageContent contentItem in threadMessage.ContentItems)
        {
            if (contentItem is MessageTextContent textItem)
            {
                Console.Write(textItem.Text);
            }
            else if (contentItem is MessageImageFileContent imageFileItem)
            {
                Console.Write($"<image from ID: {imageFileItem.FileId}>");
            }
            Console.WriteLine();
        }
    }
}

// await agentClient.Threads.DeleteThreadAsync(threadId: thread.Id);
// await agentClient.Administration.DeleteAgentAsync(agentId: agent.Id);