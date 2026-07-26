# Sample 09 – Hello World Agent SDK via API Management (Microsoft Agent Framework)

This sample combines two patterns from earlier in the series:

- **Sample 07** – Microsoft Agent Framework (`AIAgent` / `.AsAIAgent()`) instead of raw `Microsoft.Extensions.AI`
- **Sample 06** – routing all model calls through **Azure API Management (APIM)** instead of directly to Azure OpenAI

The result is the same hello-world multi-tool agent as sample 07, but with every LLM call passing through APIM.

---

## What this sample demonstrates

- How to route MAF agent calls through Azure API Management
- How to use `OpenAIClient` (not `AzureOpenAIClient`) to match APIM operation paths
- How to pass the APIM subscription key as `Ocp-Apim-Subscription-Key` — keeping the real Azure OpenAI `api-key` inside APIM, never in the application
- How the MAF `AIAgent` / `AsAIAgent()` abstraction works regardless of whether the underlying transport is direct or via a gateway

---

## Sample 07 vs Sample 09 – Side-by-side comparison

| Aspect | Sample 07 – MAF direct | Sample 09 – MAF via APIM |
|--------|------------------------|--------------------------|
| **Transport** | `AzureOpenAIClient` → Azure OpenAI directly | `OpenAIClient` → APIM gateway → Azure OpenAI |
| **Credential** | Azure OpenAI `api-key` in `appsettings.json` | APIM subscription key as `Ocp-Apim-Subscription-Key` header |
| **Real api-key location** | `appsettings.json` (gitignored) | APIM inbound policy — app never sees it |
| **URL path** | `/openai/deployments/{name}/chat/completions` | `{apim-endpoint}/chat/completions` (APIM rewrites) |
| **AIAgent / tools** | Identical | Identical |
| **UseFunctionInvocation()** | Not needed (MAF owns loop) | Not needed (MAF owns loop) |
| **Telemetry** | Same gen_ai spans via MAF → ME.AI layer | Same gen_ai spans — APIM hop is transparent to OTel |
| **Extra NuGet** | `Azure.AI.OpenAI` | `OpenAI` (no Azure prefix) |

---

## Sample 06 vs Sample 09 – Side-by-side comparison

| Aspect | Sample 06 – ME.AI via APIM | Sample 09 – MAF via APIM |
|--------|-----------------------------|---------------------------|
| **Core type** | `IChatClient` | `AIAgent` |
| **Agent creation** | `.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` | `.AsBuilder().UseOpenTelemetry().Build().AsAIAgent(instructions, name, tools)` |
| **System prompt** | `new ChatMessage(ChatRole.System, ...)` in message list | `instructions:` parameter on `AsAIAgent()` |
| **Tools registration** | `ChatOptions.Tools` per call | Registered once at `AsAIAgent(tools: [...])` |
| **Tool-call loop** | `.UseFunctionInvocation()` middleware | Managed internally by MAF |
| **Invocation** | `await chatClient.GetResponseAsync(messages, options)` | `await agent.RunAsync(userPrompt)` |
| **APIM routing** | `OpenAIClient` + `Ocp-Apim-Subscription-Key` header | `OpenAIClient` + `Ocp-Apim-Subscription-Key` header (identical) |

---

## How APIM routing works

```
App (OpenAIClient)
  └─► APIM Gateway  /chat/completions
        └─ inbound policy: set-header api-key, set-backend-service
              └─► Azure OpenAI  /openai/deployments/{name}/chat/completions
```

- The app sends `Ocp-Apim-Subscription-Key` and a placeholder SDK credential.
- APIM validates the subscription key, injects the real `api-key`, and rewrites the backend URL.
- The app holds **zero** Azure OpenAI credentials.

---

## Running the sample

1. Copy `appsettings.template.json` → `appsettings.json` and fill in:
   - `AzureOpenAI:Endpoint` – your APIM gateway URL (e.g. `https://<apim-name>.azure-api.net/<api-path>`)
   - `AzureOpenAI:ApiKey` – your APIM subscription key
   - `AzureOpenAI:DeploymentName` – the deployment name exposed by APIM (e.g. `gpt-4o`)
   - `ApplicationInsights:ConnectionString` – your App Insights connection string

2. Build and run:
   ```bash
   dotnet run
   ```

3. Open Application Insights → **Transaction Search** or **Agent Preview** to view traces.

---

## Packages used

| Package | Version | Purpose |
|---------|---------|---------|
| `OpenAI` | 2.9.0-beta.1 | OpenAI client (no Azure prefix) for APIM routing |
| `Microsoft.Agents.AI` | 1.13.0 | MAF core – `AIAgent` type |
| `Microsoft.Agents.AI.OpenAI` | 1.13.0 | Adds `AsAIAgent()` on `ChatClient` |
| `Microsoft.Extensions.AI` | 10.7.0 | `AIFunctionFactory.Create()` for tools |
| `Azure.Monitor.OpenTelemetry.Exporter` | 1.8.2 | Application Insights exporter |
| `OpenTelemetry` | 1.16.0 | Tracing and metrics pipeline |

---

## Further reading

- [Sample 06 – ME.AI via APIM](../06.helloworld.agent.via.api.management/README.md)
- [Sample 07 – MAF direct](../07.helloworld.agent.agentsdk/README.md)
- [ReadMe.APIM.Proxy.to.AzureOpenAI.md](../ReadMe.APIM.Proxy.to.AzureOpenAI.md) – APIM setup guide
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/en-us/agent-framework/)
