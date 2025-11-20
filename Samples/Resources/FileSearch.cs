using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Demonstrates the file search tool in Azure AI Foundry.
/// Shows how to upload files, create vector stores, and use file search capabilities.
/// </summary>
public class FileSearch : Base
{
    public FileSearch(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "File Search Tool";

    public override string Description =>
        "Demonstrates file search capabilities using vector stores. Shows how to upload files, " +
        "create vector stores, attach them to agents, and query file contents using AI search.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;
        PersistentAgentsVectorStore? vectorStore = null;
        PersistentAgentFileInfo? uploadedFile = null;
        string sampleFilePath = "sample_file_for_upload.txt";

        try
        {
            // Step 1: Create a local sample file
            Console.WriteLine("Creating sample file...");
            await File.WriteAllTextAsync(
                path: sampleFilePath,
                contents: "The word 'apple' uses the code 442345, while the word 'banana' uses the code 673457.");
            Console.WriteLine($"Sample file created: {sampleFilePath}");

            // Step 2: Upload the file to the agent
            Console.WriteLine("\nUploading file to agent...");
            uploadedFile = await AgentClient.Files.UploadFileAsync(
                filePath: sampleFilePath,
                purpose: PersistentAgentFilePurpose.Agents);
            Console.WriteLine($"File uploaded successfully: {uploadedFile.Filename} (ID: {uploadedFile.Id})");

            // Step 3: Create a vector store with the uploaded file
            Console.WriteLine("\nCreating vector store...");
            vectorStore = await AgentClient.VectorStores.CreateVectorStoreAsync(
                fileIds: new List<string> { uploadedFile.Id },
                name: "my_vector_store");
            Console.WriteLine($"Vector store created: {vectorStore.Name} (ID: {vectorStore.Id})");

            // Step 4: Wait for vector store to process the file
            Console.WriteLine("Waiting for vector store to process files...");
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Step 5: Create tool definition for File Search
            FileSearchToolResource fileSearchToolResource = new FileSearchToolResource();
            fileSearchToolResource.VectorStoreIds.Add(vectorStore.Id);

            // Step 6: Create an agent with file search capabilities
            Console.WriteLine("\nCreating agent with file search tool...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "fileSearch-retrieval-agent",
                instructions: "You are a helpful agent that can help fetch data from files you know about.",
                tools: new List<ToolDefinition> { new FileSearchToolDefinition() },
                toolResources: new ToolResources() { FileSearch = fileSearchToolResource });
            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");

            // Step 7: Create a thread for communication
            Console.WriteLine("\nCreating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");

            // Step 8: Send a message asking about the file contents
            string query = "Can you give me the documented codes for 'banana' and 'orange'?";
            Console.WriteLine($"\n[User]: {query}");
            await AgentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                query);

            // Step 9: Run the agent
            Console.WriteLine("Running agent...");
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id);
            
            // Wait for completion
            run = await WaitForRunCompletionAsync(thread, run);

            // Step 10: Check run status
            if (run.Status != RunStatus.Completed)
            {
                throw new Exception($"Run did not complete successfully. Status: {run.Status}, Error: {run.LastError?.Message}");
            }

            Console.WriteLine("Agent completed processing!");

            // Step 11: Display all messages with proper annotation handling
            await DisplayMessagesWithAnnotationsAsync(thread.Id, new Dictionary<string, string> 
            { 
                { uploadedFile.Id, uploadedFile.Filename } 
            });

            Console.WriteLine("\nFile Search sample completed successfully!");
            Console.WriteLine("The agent successfully searched the uploaded file and retrieved relevant information.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during file search: {ex.Message}");
        }
        finally
        {
            // // Clean up resources
            // Console.WriteLine("\nCleaning up resources...");
            
            // if (vectorStore != null)
            // {
            //     Console.WriteLine("Deleting vector store...");
            //     await AgentClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Id);
            // }

            // if (uploadedFile != null)
            // {
            //     Console.WriteLine("Deleting uploaded file...");
            //     await AgentClient.Files.DeleteFileAsync(uploadedFile.Id);
            // }

            // // Delete local sample file
            // if (File.Exists(sampleFilePath))
            // {
            //     File.Delete(sampleFilePath);
            // }

            // await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Displays messages with proper handling of file annotations (citations and paths).
    /// </summary>
    private async Task DisplayMessagesWithAnnotationsAsync(string threadId, Dictionary<string, string> fileIds)
    {
        Console.WriteLine("\n=== Conversation with File References ===");

        await foreach (var message in AgentClient.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Ascending))
        {
            Console.Write($"{message.CreatedAt:yyyy-MM-dd HH:mm:ss} - {message.Role,10}: ");

            foreach (MessageContent contentItem in message.ContentItems)
            {
                if (contentItem is MessageTextContent textItem)
                {
                    if (message.Role == MessageRole.Agent && textItem.Annotations.Count > 0)
                    {
                        string messageText = textItem.Text;

                        // Process annotations to replace file IDs with file names
                        foreach (MessageTextAnnotation annotation in textItem.Annotations)
                        {
                            if (annotation is MessageTextFilePathAnnotation pathAnnotation)
                            {
                                messageText = ReplaceReferences(fileIds, pathAnnotation.FileId, 
                                    pathAnnotation.Text, messageText);
                            }
                            else if (annotation is MessageTextFileCitationAnnotation citationAnnotation)
                            {
                                messageText = ReplaceReferences(fileIds, citationAnnotation.FileId, 
                                    citationAnnotation.Text, messageText);
                            }
                        }

                        Console.Write(messageText);
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
    /// Helper method to replace file ID references with readable file names.
    /// </summary>
    private static string ReplaceReferences(Dictionary<string, string> fileIds, string fileId, 
        string placeholder, string text)
    {
        if (fileIds.TryGetValue(fileId, out string? replacement))
        {
            return text.Replace(placeholder, $" [{replacement}]");
        }
        else
        {
            return text.Replace(placeholder, $" [{fileId}]");
        }
    }
}
