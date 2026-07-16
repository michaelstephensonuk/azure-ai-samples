# helloworld.agent

A .NET 10 console app demonstrating an **AI agent** using the **Microsoft.Extensions.AI** abstraction layer, **Azure OpenAI**, and full **OpenTelemetry** observability exported to **Azure Application Insights**.

The agent is equipped with three tools and uses the `UseFunctionInvocation()` middleware to automatically handle the multi-step tool-call loop — calling tools, feeding results back to the model, and looping until the model produces a final text answer.

## What this sample shows

- Defining agent tools using `AIFunctionFactory.Create()` with `[Description]` attributes for parameter metadata
- Wiring tools into `ChatOptions` with `ToolMode = ChatToolMode.Auto`
- Using `UseFunctionInvocation()` middleware so the tool-call loop is handled automatically — no manual loop code required
- Enabling sensitive data logging (`EnableSensitiveData = true`) so prompt, tool calls, tool results, and the final response all appear in traces
- Exporting every leg of the agentic conversation as nested OpenTelemetry spans to Application Insights

## Agent tools

| Tool | What it does |
|---|---|
| `GetCurrentDateTime` | Returns the current UTC date and time |
| `GetWeather` | Returns simulated weather for London, New York, Tokyo, Sydney, Paris |
| `Calculate` | Performs `+`, `-`, `*`, `/` on two numbers |

## Telemetry produced

| Signal | Name | What it captures |
|---|---|---|
| Trace (request) | `run-ai-agent` | Root span for the agentic operation |
| Trace (dependency) | `chat gpt-4o` | One child span **per model call** — each tool-call round trip appears as a separate span |
| Metric | `gen_ai.client.token.usage` | Token counts per model call (accumulates across the loop) |
| Metric | `gen_ai.client.operation.duration` | Latency per model call |

Because `EnableSensitiveData = true`, each `chat gpt-4o` span also carries `gen_ai.prompt` and `gen_ai.completion` attributes showing the exact tool call and result JSON.

Activity source and meter are both under `Experimental.Microsoft.Extensions.AI` (the prefix used by ME.AI 10.x while the GenAI semantic conventions are still stabilising).

## Key NuGet packages

| Package | Purpose |
|---|---|
| `Azure.AI.OpenAI` 2.9.0-beta.1 | Azure OpenAI client |
| `Microsoft.Extensions.AI` 10.7.0 | `IChatClient`, `AIFunctionFactory`, `UseFunctionInvocation()` middleware |
| `Microsoft.Extensions.AI.OpenAI` 10.7.0 | Bridge: `AsIChatClient()` extension |
| `Azure.Monitor.OpenTelemetry.Exporter` 1.8.2 | Exports OTel traces and metrics to App Insights |
| `OpenTelemetry.Exporter.Console` 1.16.0 | Prints spans and metrics to the terminal during local dev |

## Configuration

Copy `appsettings.template.json` to `appsettings.json` and fill in your values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-azure-openai-key>",
    "DeploymentName": "gpt-4o"
  },
  "ApplicationInsights": {
    "ConnectionString": "<your-app-insights-connection-string>"
  }
}
```

`appsettings.json` is gitignored. Secrets can also be supplied via environment variables:

```
AzureOpenAI__ApiKey=...
ApplicationInsights__ConnectionString=...
```

## Running locally

Press **F5** in VS Code (select the `helloworld.agent` configuration) or:

```bash
dotnet run
```

## Querying telemetry in App Insights

**Transaction search** → filter by Request → `run-ai-agent` — expand the request to see each `chat gpt-4o` dependency span, one per model round-trip.

**Token usage across all tool-call legs (KQL):**
```kql
customMetrics
| where name == "gen_ai.client.token.usage"
| extend tokenType = tostring(customDimensions["gen_ai.token.type"])
| summarize totalTokens = sum(value) by tokenType, bin(timestamp, 1h)
| order by timestamp desc
```

## Notes

- `UseFunctionInvocation()` is added **before** `UseOpenTelemetry()` in the middleware chain so OpenTelemetry wraps the entire agentic loop including all tool-call legs.
- `SamplingRatio = 1.0f` ensures all traces ship to App Insights — the default adaptive sampler drops ~2/3 of traces at low volume.
- `ForceFlush()` is called before exit to drain the Azure Monitor exporter's background queue.
- The root `run-ai-agent` span uses `ActivityKind.Server` so App Insights classifies it as a request.
