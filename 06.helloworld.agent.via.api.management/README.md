# helloworld.agent.via.api.management

A .NET 10 console app combining the **AI agent** pattern with routing through **Azure API Management (APIM)**, plus full **OpenTelemetry** observability exported to **Azure Application Insights**.

This is the "everything together" sample: agentic tool use + APIM gateway + message body logging + Application Insights telemetry.

## What this sample shows

- Routing all AI calls through APIM using `OpenAIClient` with an `Ocp-Apim-Subscription-Key` header
- Running an agent loop with three tools (`GetCurrentDateTime`, `GetWeather`, `Calculate`) via `UseFunctionInvocation()` middleware
- Enabling sensitive data logging so tool calls, tool results, and the final response appear in traces
- Exporting every leg of the multi-step agentic conversation to Application Insights as nested spans

## Agent tools

| Tool | What it does |
|---|---|
| `GetCurrentDateTime` | Returns the current UTC date and time |
| `GetWeather` | Returns simulated weather for London, New York, Tokyo, Sydney, Paris |
| `Calculate` | Performs `+`, `-`, `*`, `/` on two numbers |

## How authentication works

| Hop | Auth mechanism |
|---|---|
| Code → APIM | `Ocp-Apim-Subscription-Key` header (added to `HttpClient.DefaultRequestHeaders`) |
| APIM → Azure OpenAI | `api-key` header injected by APIM inbound policy (key stored as Named Value) |

## Telemetry produced

| Signal | Name | What it captures |
|---|---|---|
| Trace (request) | `run-ai-agent` | Root span for the entire agentic operation |
| Trace (dependency) | `chat gpt-4o` | One span per model call — each tool-call round trip is a separate span |
| Metric | `gen_ai.client.token.usage` | Token counts per model call |
| Metric | `gen_ai.client.operation.duration` | Latency per model call including APIM overhead |

Activity source and meter are both under `Experimental.Microsoft.Extensions.AI`.

## Key NuGet packages

| Package | Purpose |
|---|---|
| `Azure.AI.OpenAI` 2.9.0-beta.1 | Provides the base `OpenAIClient` |
| `Microsoft.Extensions.AI` 10.7.0 | `IChatClient`, `AIFunctionFactory`, `UseFunctionInvocation()` middleware |
| `Microsoft.Extensions.AI.OpenAI` 10.7.0 | Bridge: `AsIChatClient()` extension |
| `Azure.Monitor.OpenTelemetry.Exporter` 1.8.2 | Exports OTel traces and metrics to App Insights |
| `OpenTelemetry.Exporter.Console` 1.16.0 | Prints spans and metrics to the terminal during local dev |

## Configuration

Copy `appsettings.template.json` to `appsettings.json` and fill in your values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-apim-gateway>.azure-api.net/<your-api-path>/",
    "ApiKey": "<your-apim-subscription-key>",
    "DeploymentName": "gpt-4o"
  },
  "ApplicationInsights": {
    "ConnectionString": "<your-app-insights-connection-string>"
  }
}
```

> `Endpoint` is your **APIM gateway URL**. `ApiKey` is your **APIM subscription key**.

`appsettings.json` is gitignored. Secrets can also be supplied via environment variables:

```
AzureOpenAI__ApiKey=...
ApplicationInsights__ConnectionString=...
```

## Running locally

Press **F5** in VS Code (select the `helloworld.agent.via.api.management` configuration) or:

```bash
dotnet run
```

## APIM policy required

The APIM API needs an inbound policy to rewrite the URL and inject the Azure OpenAI key. See `ReadMe.APIM.md` in the workspace root for a full policy example.

## Querying telemetry in App Insights

**Transaction search** → filter by Request → `run-ai-agent` — each `chat gpt-4o` dependency represents one model round-trip through APIM.

**Token usage (KQL):**
```kql
customMetrics
| where name == "gen_ai.client.token.usage"
| extend tokenType = tostring(customDimensions["gen_ai.token.type"])
| summarize totalTokens = sum(value) by tokenType, bin(timestamp, 1h)
| order by timestamp desc
```

## Notes

- `OpenAIClient` is used instead of `AzureOpenAIClient` — see `helloworld.via.api.management` for the explanation.
- `UseFunctionInvocation()` is registered before `UseOpenTelemetry()` so OpenTelemetry captures each tool-call leg as a separate child span.
- `SamplingRatio = 1.0f` ensures all traces ship to App Insights.
- `ForceFlush()` is called before exit to drain the Azure Monitor exporter's background queue.
- The root `run-ai-agent` span uses `ActivityKind.Server` so App Insights classifies it as a request.
