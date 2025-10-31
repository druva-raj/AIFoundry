using Azure.AI.Agents.Persistent;
using Samples.Common;

namespace Samples.Resources;

/// <summary>
/// Error handling and recovery capabilities for Azure AI Foundry agents.
/// Demonstrates how to retrieve an existing agent and handle various error scenarios.
/// </summary>
public class ErrorHandling : Base
{
    public ErrorHandling(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "Error Handling & Recovery";

    public override string Description => 
        "Error handling patterns including retrieving existing agents, handling failures, " +
        "timeouts, and implementing retry logic for robust AI applications.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;
        string agentId = "asst_Wm7BwZCJbFEEZBb1iH6LQoJm";

        try
        {

            // Get the agent by ID
            Console.WriteLine("\nRetrieving agent...");
            var retrievedAgent = await AgentClient.Administration.GetAgentAsync(agentId);
            Console.WriteLine($"Agent retrieved: {retrievedAgent.Value.Name}");

            // Create thread
            Console.WriteLine("\nCreating thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");

            // Send message and run
            Console.WriteLine("\nSending message...");
            await AgentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, "Explore Azure AI Foundry Models\r\n09/04/2025\r\nAzure AI Foundry Models is your one-stop destination for discovering, evaluating, and deploying powerful AI models—whether you're building a custom copilot, building an agent, enhancing an existing application, or exploring new AI capabilities.\r\n\r\nWith Foundry Models, you can:\r\n\r\nExplore a rich catalog of cutting-edge models from Microsoft, OpenAI, DeepSeek, Hugging Face, Meta, and more.\r\nCompare and evaluate models side-by-side using real-world tasks and your own data.\r\nDeploy with confidence, thanks to built-in tools for fine-tuning, observability, and responsible AI.\r\nChoose your path—bring your own model, use a hosted one, or integrate seamlessly with Azure services.\r\nWhether you're a developer, data scientist, or enterprise architect, Foundry Models gives you the flexibility and control to build AI solutions that scale—securely, responsibly, and fast.\r\nAzure AI Foundry offers a comprehensive catalog of AI models. There are over 1900+ models ranging from Foundation Models, Reasoning Models, Small Language Models, Multimodal Models, Domain Specific Models, Industry Models and more.\r\n\r\nOur catalog is organized into two main categories:\r\n\r\nModels sold directly by Azure\r\nModels from Partners and Community\r\nUnderstanding the distinction between these categories helps you choose the right models based on your specific requirements and strategic goals.\r\n\r\n Note\r\n\r\nFor all models, Customers remain responsible for (i) complying with the law in their use of any model or system; (ii) reviewing model descriptions in the model catalog, model cards made available by the model provider, and other relevant documentation; (iii) selecting an appropriate model for their use case, and (iv) implementing appropriate measures (including use of Azure AI Content Safety) to ensure Customer's use of the Microsoft AI Services complies with the Acceptable Use Policy in Microsoft’s Product Terms and the Microsoft Enterprise AI Services Code of Conduct.\r\nModels Sold Directly by Azure\r\nThese are models that are hosted and sold by Microsoft under Microsoft Product Terms. Microsoft has evaluated these models and they are deeply integrated into Azure's AI ecosystem. The models come from a variety of providers and they offer enhanced integration, optimized performance, and direct Microsoft support, including enterprise-grade Service Level Agreements (SLAs).\r\n\r\nCharacteristics of models sold directly by Azure:\r\n\r\nSupport available from Microsoft.\r\nHigh level of integration with Azure services and infrastructure.\r\nSubject to internal review based on Microsoft’s Responsible AI standards.\r\nModel documentation and transparency reports provide customer visibility to model risks, mitigations, and limitations.\r\nEnterprise-grade scalability, reliability, and security.\r\nSome of these Models also have the benefit of fungible Provisioned Throughput, meaning you can flexibly use your quota and reservations across any of these models.\r\n\r\nModels from Partners and Community\r\nThese models constitute the vast majority of the Azure AI Foundry Models and are provided by trusted third-party organizations, partners, research labs, and community contributors. These models offer specialized and diverse AI capabilities, covering a wide array of scenarios, industries, and innovations.\r\n\r\nExamples of models from Partners and community are Open models from the Hugging Face hub. These include hundreds of models from the Hugging Face hub for real-time inference with managed compute. Hugging Face creates and maintains models listed in this collection. For help with the Hugging Face models, use the Hugging Face forum or Hugging Face support. Learn how to deploy Hugging Face models in Deploy open models with Azure AI Foundry.\r\n\r\nCharacteristics of Models from Partners and Community:\r\n\r\nDeveloped and supported by external partners and community contributors\r\nDiverse range of specialized models catering to niche or broad use cases\r\nTypically validated by providers themselves, with integration guidelines provided by Azure\r\nCommunity-driven innovation and rapid availability of cutting-edge models\r\nStandard Azure AI integration, with support and maintenance managed by the respective providers\r\nModels from Partners and Community are deployable as Managed Compute or serverless API deployment options. The model provider selects how the models are deployable.\r\n\r\nRequesting a model to be included in the model catalog\r\nYou can request that we add a model to the model catalog, right from the model catalog page in the Azure AI Foundry portal. From the search bar of the model catalog page, a search for a model that doesn't exist in the catalog, such as mymodel, returns the Request a model button. Select this button to open up a form where you can share details about the model you're requesting.\r\n\r\nA screenshot showing where to request inclusion of a model in the model catalog.\r\n\r\nChoosing Between direct models and partner & community models\r\nWhen selecting models from Azure AI Foundry Models, consider the following:\r\n\r\nUse Case and Requirements: Models sold directly by Azure are ideal for scenarios requiring deep Azure integration, guaranteed support, and enterprise SLAs. Models from Partners and Community excel in specialized use cases and innovation-led scenarios.\r\nSupport Expectations: Models sold directly by Azure come with robust Microsoft-provided support and maintenance. These models are supported by their providers, with varying levels of SLA and support structures.\r\nInnovation and Specialization: Models from Partners and Community offer rapid access to specialized innovations and niche capabilities often developed by leading research labs and emerging AI providers.\r\nOverview of Model Catalog capabilities\r\nThe model catalog in Azure AI Foundry portal is the hub to discover and use a wide range of models for building generative AI applications. The model catalog features hundreds of models across model providers such as Azure OpenAI, Mistral, Meta, Cohere, NVIDIA, and Hugging Face, including models that Microsoft trained. Models from providers other than Microsoft are Non-Microsoft Products as defined in Microsoft Product Terms and are subject to the terms provided with the models.\r\n\r\nYou can search and discover models that meet your need through keyword search and filters. Model catalog also offers the model performance leaderboard and benchmark metrics for select models. You can access them by selecting Browse leaderboard and Compare Models. Benchmark data is also accessible from the model card Benchmark tab.\r\n\r\nOn the model catalog filters, you'll find:\r\n\r\nCollection: you can filter models based on the model provider collection.\r\nIndustry: you can filter for the models that are trained on industry specific dataset.\r\nCapabilities: you can filter for unique model features such as reasoning and tool calling.\r\nDeployment options: you can filter for the models that support a specific deployment options.\r\nserverless API: this option allows you to pay per API call.\r\nProvisioned: best suited for real-time scoring for large consistent volume.\r\nBatch: best suited for cost-optimized batch jobs, and not latency. No playground support is provided for the batch deployment.\r\nManaged compute: this option allows you to deploy a model on an Azure virtual machine. You will be billed for hosting and inferencing.\r\nInference tasks: you can filter models based on the inference task type.\r\nFine-tune tasks: you can filter models based on the fine-tune task type.\r\nLicenses: you can filter models based on the license type.\r\nOn the model card, you'll find:\r\n\r\nQuick facts: you will see key information about the model at a quick glance.\r\nDetails: this page contains the detailed information about the model, including description, version info, supported data type, etc.\r\nBenchmarks: you will find performance benchmark metrics for select models.\r\nExisting deployments: if you have already deployed the model, you can find it under Existing deployments tab.\r\nLicense: you will find legal information related to model licensing.\r\nArtifacts: this tab will be displayed for open models only. You can see the model assets and download them via user interface.\r\nModel deployment: Managed compute and serverless API deployments\r\nIn addition to deploying to Azure OpenAI, the model catalog offers two distinct ways to deploy models for your use: managed compute and serverless API deployments.\r\n\r\nThe deployment options and features available for each model vary, as described in the following tables. Learn more about data processing with the deployment options.\r\n\r\nCapabilities of model deployment options\r\nFeatures\tManaged compute\tserverless API deployment\r\nDeployment experience and billing\tModel weights are deployed to dedicated virtual machines with managed compute. A managed compute, which can have one or more deployments, makes available a REST API for inference. You're billed for the virtual machine core hours that the deployments use.\tAccess to models is through a deployment that provisions an API to access the model. The API provides access to the model that Microsoft hosts and manages, for inference. You're billed for inputs and outputs to the APIs, typically in tokens. Pricing information is provided before you deploy.\r\nAPI authentication\tKeys and Microsoft Entra authentication.\tKeys only.\r\nContent safety\tUse Azure AI Content Safety service APIs.\tAzure AI Content Safety filters are available integrated with inference APIs. Azure AI Content Safety filters are billed separately.\r\nNetwork isolation\tConfigure managed networks for Azure AI Foundry hubs.\tManaged compute follow your hub's public network access (PNA) flag setting. For more information, see the Network isolation for models deployed via serverless API deployments section later in this article.\r\nAvailable models for supported deployment options\r\nFor Azure OpenAI models, see Azure OpenAI.\r\n\r\nTo view a list of supported models for serverless API deployment or Managed Compute, go to the home page of the model catalog in Azure AI Foundry. Use the Deployment options filter to select either serverless API deployment or Managed Compute.\r\n\r\n");
            
            Console.WriteLine("\nCreating run...");
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread.Id, retrievedAgent.Value.Id);
            Console.WriteLine($"Run created: {run.Id}");

            // Poll run status directly
            Console.WriteLine("\nPolling run status...");
            while (run.Status == RunStatus.Queued || 
                   run.Status == RunStatus.InProgress || 
                   run.Status == RunStatus.RequiresAction)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1000));
                Console.WriteLine($"Status: {run.Status}");
                
                run = await AgentClient.Runs.GetRunAsync(thread.Id, run.Id);
            }

            Console.WriteLine($"\nRun completed with status: {run.Status}");
            
            // If run failed, display error details and throw exception
            if (run.Status == RunStatus.Failed)
            {
                Console.WriteLine("\n=== RUN FAILED - DISPLAYING ERROR DETAILS ===");
                if (run.LastError != null)
                {
                    Console.WriteLine($"Error Code: {run.LastError.Code}");
                    Console.WriteLine($"Error Message: {run.LastError.Message}");
                }
                else
                {
                    Console.WriteLine("No LastError details available on run object.");
                }
                
                // Display all run properties for debugging
                Console.WriteLine($"\nRun ID: {run.Id}");
                Console.WriteLine($"Thread ID: {run.ThreadId}");
                Console.WriteLine($"Status: {run.Status}");
                Console.WriteLine($"Created At: {run.CreatedAt}");
                Console.WriteLine($"Started At: {run.StartedAt}");
                Console.WriteLine($"Failed At: {run.FailedAt}");
                
                string errorMessage = "Run failed";
                if (run.LastError != null)
                {
                    errorMessage = $"Run failed with error code '{run.LastError.Code}': {run.LastError.Message}";
                }
                throw new Exception(errorMessage);
            }

        }
        catch (Azure.RequestFailedException ex)
        {
            Console.WriteLine($"\n=== Azure Request Failed Exception ===");
            Console.WriteLine($"Status: {ex.Status}");
            Console.WriteLine($"Error Code: {ex.ErrorCode}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Source: {ex.Source}");
            Console.WriteLine($"HResult: {ex.HResult}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n=== General Exception ===");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Source: {ex.Source}");
            Console.WriteLine($"HResult: {ex.HResult}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        finally
        {
            //await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }


}
