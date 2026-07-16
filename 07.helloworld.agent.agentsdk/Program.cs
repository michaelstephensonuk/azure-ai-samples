// =============================================================================
// Sample 07 – Hello World Agent SDK (Microsoft Agent Framework)
// =============================================================================
// This sample replicates sample 05 (Microsoft.Extensions.AI) using the
// Microsoft Agent Framework (MAF) – a higher-level abstraction built on top
// of Microsoft.Extensions.AI.
//
// Key differences vs sample 05:
//   • Uses AIAgent / .AsAIAgent() instead of IChatClient builder pipeline
//   • System prompt is passed as `instructions:` to AsAIAgent() – not as a ChatMessage
//   • Tools are registered at agent-creation time (not in ChatOptions per call)
//   • UseFunctionInvocation() middleware is NOT needed – MAF manages the loop internally
//   • .RunAsync(userPrompt) replaces .GetResponseAsync(messages, options)
//   • Token counts are NOT returned by RunAsync() – use streaming or custom metrics
// =============================================================================

using System.ComponentModel;
using System.Diagnostics;
using Azure;
using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ---------------------------------------------------------------------------
// 1. Configuration
// ---------------------------------------------------------------------------
// appsettings.json is gitignored (contains real keys).
// Environment variables override it for CI/CD and container deployments.
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
// MAF is built on top of Microsoft.Extensions.AI and emits traces under the
// same activity source names.  The gen_ai semantic-convention spans come from
// the ME.AI layer regardless of whether you use IChatClient directly (sample 05)
// or wrap it with AIAgent (this sample).
//
// NOTE: unlike sample 05, we do NOT call .UseOpenTelemetry() on the inner
// IChatClient here.  This is intentional – it lets you compare "out-of-the-box"
// MAF telemetry vs the explicitly wired ME.AI pipeline in sample 05.
// ---------------------------------------------------------------------------

var sourceName = "helloworld-agentsdk";
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
// 3. Agent tools  (identical to sample 05 – same AIFunctionFactory pattern)
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
// 4. Build the MAF AIAgent
// ---------------------------------------------------------------------------
// Key difference vs sample 05:
//   • Tools are registered HERE at construction time via tools: []
//   • The system prompt becomes the agent's `instructions`
//   • No ChatOptions, no UseFunctionInvocation() – MAF owns the tool-call loop
// ---------------------------------------------------------------------------

const string systemPrompt =
    "You are a helpful assistant with access to tools for getting the current date/time, " +
    "checking weather conditions, and performing calculations. " +
    "Use your tools whenever they would help you give a more accurate answer.";

const string userPrompt =
    "What is the weather like in London and Tokyo right now? " +
    "If it is currently past noon UTC, also tell me the temperature difference between the two cities in Fahrenheit.";

// AsAIAgent() is an extension method from Microsoft.Agents.AI on IChatClient.
// We must first convert the OpenAI ChatClient to IChatClient via .AsIChatClient()
// (from Microsoft.Extensions.AI.OpenAI), then call .AsAIAgent().
AIAgent agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient()           // convert OpenAI ChatClient → IChatClient (ME.AI) 
    .AsBuilder()
    .UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)
    .Build()   
    .AsAIAgent(
        instructions: systemPrompt,
        name: "helloworld-agent-agentsdk",
        tools: [getDateTimeTool, getWeatherTool, calculateTool]);

// ---------------------------------------------------------------------------
// 5. Run the agent
// ---------------------------------------------------------------------------

Console.WriteLine("Azure OpenAI Agent – Hello World Agent SDK Sample (Microsoft Agent Framework)");
Console.WriteLine($"Deployment: {deploymentName}");
Console.WriteLine($"Tools     : GetCurrentDateTime, GetWeather, Calculate");
Console.WriteLine();
Console.WriteLine($"User: {userPrompt}");
Console.WriteLine();

long inputTokens  = 0;
long outputTokens = 0;

// Scoped using block so the parent span is CLOSED before ForceFlush is called.
using (var operation = appSource.StartActivity("run-ai-agent", ActivityKind.Server))
{
    // Custom dependency span – same pattern as sample 05.
    // This makes the agent visible in App Insights Agent Preview by carrying
    // the gen_ai.agent.name and gen_ai.operation.name tags directly on this span.
    AgentResponse agentResponse;
    using (var agentSpan = appSource.StartActivity("Invoke Hello World Agent", 
        ActivityKind.Client))
    {
        agentSpan?.SetTag("gen_ai.operation.name", "invoke_agent");
        agentSpan?.SetTag("gen_ai.agent.name", "helloworld-agent-agentsdk");
        agentSpan?.SetTag("gen_ai.system", "openai");
        agentSpan?.SetTag("gen_ai.request.model", deploymentName);

        // MAF handles the entire tool-call loop internally.
        // RunAsync() blocks until the agent reaches a final text response.
        // Unlike GetResponseAsync() (sample 05), it returns AgentResponse –
        // token usage is not surfaced here.  Use RunStreamingAsync() for
        // streaming updates, or add custom OTel metrics if you need usage counts.
        agentResponse = await agent.RunAsync(userPrompt);

        agentSpan?.SetStatus(ActivityStatusCode.Ok);

        if (agentResponse.Usage is not null)
        {
            inputTokens  = agentResponse.Usage.InputTokenCount  ?? 0;
            outputTokens = agentResponse.Usage.OutputTokenCount ?? 0;
            Console.WriteLine($"Token usage — Input: {inputTokens}, Output: {outputTokens}, Total: {inputTokens + outputTokens}");
            
            //Set token use on the custom agent dependency
            agentSpan?.SetTag("ai.input_tokens",  inputTokens);
            agentSpan?.SetTag("ai.output_tokens", outputTokens);            
        }
    }

    Console.WriteLine("Agent:");
    Console.WriteLine(agentResponse.Text);
    Console.WriteLine();

    //Set tokens on the parent request
    operation?.SetTag("ai.input_tokens",  inputTokens);
    operation?.SetTag("ai.output_tokens", outputTokens);

    operation?.SetStatus(ActivityStatusCode.Ok);
} // ← parent span ends here, queued for export

// ---------------------------------------------------------------------------
// 6. Flush telemetry
// ---------------------------------------------------------------------------
Console.WriteLine("Flushing telemetry...");
tracerProvider.ForceFlush();
meterProvider.ForceFlush();

Thread.Sleep(5000); // give the exporter a moment to send before process exits
Console.WriteLine("Done. Traces sent to Application Insights.");
