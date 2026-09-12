// =============================================================================
// Sample 10 – Foundry Agent Service via Agent SDK
// =============================================================================
// Calls an agent that has already been created/deployed in Azure AI Foundry
// (Agent Service) directly via the Azure.AI.Projects SDK — no Microsoft Agent
// Framework (MAF) layer involved.
//
// Key differences vs samples 05/07/09:
//   • The agent (instructions, model, tools) lives in Azure AI Foundry, not in
//     this app – we reference it by name + version.
//   • AIProjectClient / ProjectResponsesClient (Azure.AI.Projects[.OpenAI]) is
//     used instead of AzureOpenAIClient/OpenAIClient + IChatClient or AIAgent.
//   • Auth is AzureCliCredential against the Foundry project endpoint –
//     run `az login` first so the CLI's cached identity can be picked up.
//     There is no api-key involved.
// =============================================================================

using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using System.ClientModel.Primitives;

#pragma warning disable OPENAI001

// ---------------------------------------------------------------------------
// 1. Configuration
// ---------------------------------------------------------------------------
// appsettings.json is gitignored (contains your real Foundry project details).
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["Foundry:Endpoint"]
    ?? throw new InvalidOperationException("Foundry:Endpoint is not set in appsettings.json");
var agentName = config["Foundry:AgentName"]
    ?? throw new InvalidOperationException("Foundry:AgentName is not set in appsettings.json");
var agentVersion = config["Foundry:AgentVersion"] ?? "1";
var userPrompt = config["Foundry:UserPrompt"] ?? "Hello! Tell me a joke.";

// ---------------------------------------------------------------------------
// 2. Connect to the Foundry project
// ---------------------------------------------------------------------------
// AzureCliCredential is pinned explicitly (rather than DefaultAzureCredential)
// because other cached identities — from VS Code's or Visual Studio's own
// Azure sign-in — can otherwise take precedence and lack RBAC on the Foundry
// project. `az login` already has the correct roles assigned.
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("x-correlation-id", Guid.NewGuid().ToString());
httpClient.DefaultRequestHeaders.Add("x-customer-id", "customer-12345");
httpClient.DefaultRequestHeaders.Add("x-username", "test-user");

var options = new AIProjectClientOptions
{
    Transport = new HttpClientPipelineTransport(httpClient)
};

AIProjectClient projectClient = new(
    endpoint: new Uri(endpoint),
    tokenProvider: new AzureCliCredential(),
    options: options);

// ---------------------------------------------------------------------------
// 3. Reference the deployed agent and get a Responses client scoped to it
// ---------------------------------------------------------------------------
AgentReference agentReference = new(name: agentName, version: agentVersion);
ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);

Console.WriteLine("Azure AI Foundry Agent Service – Hello World via Agent SDK");
Console.WriteLine($"Endpoint     : {endpoint}");
Console.WriteLine($"Agent        : {agentName} (v{agentVersion})");
Console.WriteLine();
Console.WriteLine($"User: {userPrompt}");
Console.WriteLine();

// ---------------------------------------------------------------------------
// 4. Call the agent
// ---------------------------------------------------------------------------
ResponseResult response = await responseClient.CreateResponseAsync(userPrompt);

Console.WriteLine("Agent:");
Console.WriteLine(response.GetOutputText());
