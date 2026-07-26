// =============================================================================
// Sample 09 – Hello World Agent SDK via API Management (Microsoft Agent Framework)
// =============================================================================
// This sample replicates sample 07 (Microsoft Agent Framework) but routes all
// model calls through Azure API Management (APIM) instead of directly to
// Azure OpenAI – matching the same APIM routing pattern introduced in sample 06.
//
// Key differences vs sample 07 (MAF direct):
//   • Uses OpenAIClient (not AzureOpenAIClient) so the SDK calls {endpoint}/chat/completions
//     directly – AzureOpenAIClient would append /openai/deployments/{name}/chat/completions
//     which won't match the APIM operation path.
//   • ApiKey is the APIM subscription key, sent as Ocp-Apim-Subscription-Key header.
//   • The real Azure OpenAI api-key is injected by the APIM inbound policy – the app
//     never holds it.
//   • Everything else (AIAgent, AsAIAgent, tools, telemetry) is identical to sample 07.
//
// Key differences vs sample 06 (ME.AI via APIM):
//   • Uses AIAgent / .AsAIAgent() instead of IChatClient builder pipeline
//   • System prompt is passed as `instructions:` to AsAIAgent() – not as a ChatMessage
//   • Tools are registered at agent-creation time (not in ChatOptions per call)
//   • UseFunctionInvocation() middleware is NOT needed – MAF manages the loop internally
//   • .RunAsync(userPrompt) replaces .GetResponseAsync(messages, options)
// =============================================================================

using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ---------------------------------------------------------------------------
// 1. Configuration
// ---------------------------------------------------------------------------
// appsettings.json is gitignored (contains real keys).
// Endpoint  = your APIM gateway URL (e.g. https://<apim-name>.azure-api.net/<api-path>)
// ApiKey    = your APIM subscription key  (Ocp-Apim-Subscription-Key)
// The real Azure OpenAI api-key stays in the APIM policy – this app never sees it.
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not set in appsettings.json");
var apiKey = config["AzureOpenAI:ApiKey"]
    ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not set in appsettings.json");
var deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
var appInsightsConnectionString = config["ApplicationInsights:ConnectionString"]
    ?? throw new InvalidOperationException("ApplicationInsights:ConnectionString is not set in appsettings.json");

// ---------------------------------------------------------------------------
// 2. OpenTelemetry pipeline
// ---------------------------------------------------------------------------
var sourceName = "helloworld-agentsdk-via-apim";
var appSource = new ActivitySource(sourceName);

var resource = ResourceBuilder.CreateDefault()
    .AddService(sourceName);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resource)
    .SetSampler(new AlwaysOnSampler())
    .AddSource(sourceName)
    // MAF internally wraps the OpenAI ChatClient as an IChatClient and emits
    // traces under the Microsoft.Extensions.AI source names.
    .AddSource("Microsoft.Extensions.AI")
    .AddSource("Experimental.Microsoft.Extensions.AI")
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = appInsightsConnectionString;
        o.SamplingRatio = 1.0f;
    })
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resource)
    .AddMeter("Experimental.Microsoft.Extensions.AI")
    .AddConsoleExporter()
    .AddAzureMonitorMetricExporter(o => o.ConnectionString = appInsightsConnectionString)
    .Build();

// ---------------------------------------------------------------------------
// 3. APIM-aware HTTP client
// ---------------------------------------------------------------------------
// APIM requires the subscription key as a request header.
// We attach it to every outbound request via HttpClient.DefaultRequestHeaders.
// The SDK credential is set to a placeholder because the real auth is handled
// by the APIM inbound policy (set-header / set-backend-service).
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

var clientOptions = new OpenAIClientOptions
{
    Endpoint = new Uri(endpoint),
    Transport = new HttpClientPipelineTransport(httpClient)
};

// ---------------------------------------------------------------------------
// 4. Agent tools  (identical to sample 07)
// ---------------------------------------------------------------------------

AIFunction getDateTimeTool = AIFunctionFactory.Create(
    () => DateTime.UtcNow.ToString("R"),
    "GetCurrentDateTime",
    "Returns the current UTC date and time.");

AIFunction getWeatherTool = AIFunctionFactory.Create(
    ([Description("The city to get the weather for")] string city) =>
        city.Trim().ToLowerInvariant() switch
        {
            "london"   => "Overcast, 14°C, 80% chance of rain.",
            "new york" => "Partly cloudy, 22°C, mild breeze.",
            "tokyo"    => "Clear skies, 28°C, humid.",
            "sydney"   => "Sunny, 19°C, perfect beach weather.",
            "paris"    => "Light showers, 16°C.",
            _          => $"No weather data available for '{city}'."
        },
    "GetWeather",
    "Returns current weather conditions for a given city.");

AIFunction calculateTool = AIFunctionFactory.Create(
    ([Description("Left operand")] double a,
     [Description("Operator to apply: +, -, *, or /")] string op,
     [Description("Right operand")] double b) =>
        op switch
        {
            "+" => (a + b).ToString("G"),
            "-" => (a - b).ToString("G"),
            "*" => (a * b).ToString("G"),
            "/" => b == 0 ? "Error: division by zero" : (a / b).ToString("G"),
            _   => $"Unknown operator '{op}'. Use +, -, *, or /."
        },
    "Calculate",
    "Performs a basic arithmetic operation (+, -, *, /) on two numbers and returns the result.");

// ---------------------------------------------------------------------------
// 5. Build the MAF AIAgent (via APIM)
// ---------------------------------------------------------------------------
// Key difference vs sample 07:
//   • OpenAIClient (not AzureOpenAIClient) so the path is {endpoint}/chat/completions
//   • Credential is a placeholder "placeholder" – real auth is the APIM subscription key header
// Everything else – AsAIAgent(), instructions, tools – is identical to sample 07.

const string systemPrompt =
    "You are a helpful assistant with access to tools for getting the current date/time, " +
    "checking weather conditions, and performing calculations. " +
    "Use your tools whenever they would help you give a more accurate answer.";

const string userPrompt =
    "What is the weather like in London and Tokyo right now? " +
    "If it is currently past noon UTC, also tell me the temperature difference between the two cities in Fahrenheit.";

AIAgent agent = new OpenAIClient(
        new ApiKeyCredential("placeholder"), // subscription key already in HttpClient headers
        clientOptions)
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)
    .Build()
    .AsAIAgent(
        instructions: systemPrompt,
        name: "helloworld-agent-agentsdk-via-apim",
        tools: [getDateTimeTool, getWeatherTool, calculateTool]);

// ---------------------------------------------------------------------------
// 6. Run the agent
// ---------------------------------------------------------------------------

Console.WriteLine("Azure OpenAI Agent – Hello World Agent SDK via APIM (Microsoft Agent Framework)");
Console.WriteLine($"Deployment: {deploymentName}");
Console.WriteLine($"Endpoint  : {endpoint}");
Console.WriteLine($"Tools     : GetCurrentDateTime, GetWeather, Calculate");
Console.WriteLine();
Console.WriteLine($"User: {userPrompt}");
Console.WriteLine();

long inputTokens  = 0;
long outputTokens = 0;

using (var operation = appSource.StartActivity("run-ai-agent", ActivityKind.Server))
{
    AgentResponse agentResponse;
    using (var agentSpan = appSource.StartActivity("Invoke Hello World Agent",
        ActivityKind.Client))
    {
        agentSpan?.SetTag("gen_ai.operation.name", "invoke_agent");
        agentSpan?.SetTag("gen_ai.agent.name", "helloworld-agent-agentsdk-via-apim");
        agentSpan?.SetTag("gen_ai.system", "openai");
        agentSpan?.SetTag("gen_ai.request.model", deploymentName);

        // MAF handles the entire tool-call loop internally via APIM.
        agentResponse = await agent.RunAsync(userPrompt);

        if (agentResponse.Usage is not null)
        {
            inputTokens  = agentResponse.Usage.InputTokenCount  ?? 0;
            outputTokens = agentResponse.Usage.OutputTokenCount ?? 0;
            Console.WriteLine($"Token usage — Input: {inputTokens}, Output: {outputTokens}, Total: {inputTokens + outputTokens}");

            agentSpan?.SetTag("ai.input_tokens",  inputTokens);
            agentSpan?.SetTag("ai.output_tokens", outputTokens);
        }
    }

    Console.WriteLine("Agent:");
    Console.WriteLine(agentResponse.Text);
    Console.WriteLine();

    operation?.SetTag("ai.input_tokens",  inputTokens);
    operation?.SetTag("ai.output_tokens", outputTokens);
}

// ---------------------------------------------------------------------------
// 7. Flush telemetry
// ---------------------------------------------------------------------------
Console.WriteLine("Flushing telemetry...");
tracerProvider.ForceFlush();
meterProvider.ForceFlush();

Thread.Sleep(5000);
Console.WriteLine("Done. Traces sent to Application Insights.");
