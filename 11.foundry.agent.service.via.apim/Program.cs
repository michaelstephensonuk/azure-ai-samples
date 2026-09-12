// =============================================================================
// Sample 11 – Foundry Agent Service via APIM
// =============================================================================
// Calls an agent that has already been created/deployed in Azure AI Foundry
// (Agent Service) through an Azure API Management gateway that fronts the
// Foundry project endpoint, instead of calling Foundry directly (see sample 10).
//
// Key differences vs sample 10:
//   • Endpoint points at the APIM gateway, which proxies through to the real
//     Foundry project endpoint.
//   • Auth is the APIM subscription key (api-key header), not an Entra ID
//     credential — APIM handles authorization, so no managed identity /
//     az login is involved.
//   • AIProjectClient still requires an AuthenticationTokenProvider, so a
//     no-op provider is supplied purely to satisfy that requirement; it's
//     never actually relied on for authorization.
// =============================================================================

using Azure.AI.Projects;
using Azure.AI.Extensions.OpenAI;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;
using System.ClientModel;
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
var apiManagementKey = config["Foundry:ApiKey"]
    ?? throw new InvalidOperationException("Foundry:ApiKey is not set in appsettings.json");

// ---------------------------------------------------------------------------
// 2. Connect to the Foundry project via APIM
// ---------------------------------------------------------------------------
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("x-correlation-id", Guid.NewGuid().ToString());
httpClient.DefaultRequestHeaders.Add("x-customer-id", "customer-12345");
httpClient.DefaultRequestHeaders.Add("x-username", "test-user");

var options = new AIProjectClientOptions
{
    Transport = new HttpClientPipelineTransport(httpClient)
};

// APIM subscription key, sent as an "api-key" header via the SDK's own
// pipeline policy rather than a plain default header on the HttpClient.
options.AddPolicy(
    ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(new ApiKeyCredential(apiManagementKey), "api-key", keyPrefix: null),
    PipelinePosition.PerTry);

// Note: AIProjectClient also has an AIProjectClientSettings-based constructor
// (marked [Experimental("SCME0002")]), but it never initializes the SDK's
// internal connection-cache manager, so `projectClient.OpenAI` always throws
// a NullReferenceException on that path. The plain (endpoint, tokenProvider,
// options) constructor used below is the only one that wires it up.
AIProjectClient projectClient = new(
    endpoint: new Uri(endpoint),
    tokenProvider: new NoOpTokenProvider(), //This is a workaround as we need a token provider
    options: options);

// ---------------------------------------------------------------------------
// 3. Reference the deployed agent and get a Responses client scoped to it
// ---------------------------------------------------------------------------
AgentReference agentReference = new(name: agentName, version: agentVersion);
ProjectResponsesClient responseClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(agentReference);

Console.WriteLine("Azure AI Foundry Agent Service – Hello World via APIM");
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

// A token provider that never issues a real token: APIM's subscription key
// header handles authorization, so no Entra ID token is ever needed, but the
// SDK's constructor requires a non-null AuthenticationTokenProvider.
sealed class NoOpTokenProvider : AuthenticationTokenProvider
{
    public override GetTokenOptions CreateTokenOptions(IReadOnlyDictionary<string, object> properties)
        => new(properties);

    public override AuthenticationToken GetToken(GetTokenOptions options, CancellationToken cancellationToken = default)
        => new("unused", "Bearer", DateTimeOffset.MaxValue, null);

    public override ValueTask<AuthenticationToken> GetTokenAsync(GetTokenOptions options, CancellationToken cancellationToken = default)
        => new(GetToken(options, cancellationToken));
}
