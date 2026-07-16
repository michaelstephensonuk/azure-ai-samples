# Sample 07 – Hello World Agent SDK (Microsoft Agent Framework)

This sample replicates [sample 05](../05.helloworld.agent) using the **Microsoft Agent Framework (MAF)** (`Microsoft.Agents.AI` v1.13.0) instead of `Microsoft.Extensions.AI` directly.

---

## What this sample demonstrates

- How to build an Azure OpenAI agent using the MAF high-level `AIAgent` abstraction
- How to wire up the same three tools (GetCurrentDateTime, GetWeather, Calculate)
- How Application Insights telemetry compares when using MAF vs the lower-level ME.AI pipeline
- The minimal code required to run a multi-tool agent with MAF

---

## Sample 05 vs Sample 07 – Side-by-side comparison

| Aspect | Sample 05 – `Microsoft.Extensions.AI` | Sample 07 – Microsoft Agent Framework |
|--------|----------------------------------------|----------------------------------------|
| **Core type** | `IChatClient` | `AIAgent` |
| **Agent creation** | `chatClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().Build()` | `chatClient.AsAIAgent(instructions, name, tools)` |
| **System prompt** | `new ChatMessage(ChatRole.System, ...)` added to the message list | `instructions:` parameter on `AsAIAgent()` |
| **Tools registration** | `ChatOptions.Tools` on every call | Registered once at `AsAIAgent(tools: [...])` |
| **Tool-call loop** | `.UseFunctionInvocation()` middleware handles it | Managed internally by MAF |
| **Invocation** | `await chatClient.GetResponseAsync(messages, options)` | `await agent.RunAsync(userPrompt)` |
| **Return type** | `ChatResponse` (includes `Usage` with token counts) | `string` (token counts not directly available) |
| **OTel wiring** | Explicit `.UseOpenTelemetry()` on the `IChatClient` builder | MAF re-uses the same ME.AI activity sources internally – gen_ai spans still appear |
| **NuGet packages** | `Microsoft.Extensions.AI`, `Azure.AI.OpenAI` | `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, `Azure.AI.OpenAI` |
| **Lines of code** | ~190 | ~150 |

---

## How MAF relates to Microsoft.Extensions.AI

MAF (`Microsoft.Agents.AI`) is built **on top of** `Microsoft.Extensions.AI`.  Internally, `AsAIAgent()` converts the `OpenAI.Chat.ChatClient` to an `IChatClient` and manages the tool-call loop for you.  Because of this layering:

- The same `gen_ai.*` semantic-convention activity attributes (from the ME.AI OTel middleware) are emitted under the same `Microsoft.Extensions.AI` / `Experimental.Microsoft.Extensions.AI` activity source names.
- App Insights captures the same `dependencies` table entries for each LLM call.
- The custom `Invoke Hello World Agent` dependency span (with `gen_ai.agent.name` and `gen_ai.operation.name = "invoke_agent"`) is still needed to appear in the **Agent Preview** blade – MAF does not set these tags automatically.

---

## Running the sample

1. Copy `appsettings.template.json` → `appsettings.json` and fill in your Azure OpenAI and Application Insights values.
2. Build and run:
   ```bash
   dotnet run
   ```
3. Open Application Insights → **Transaction Search** or **Agent Preview** to view traces.

---

## Packages used

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Agents.AI` | 1.13.0 | MAF core – `AIAgent` type |
| `Microsoft.Agents.AI.OpenAI` | 1.13.0 | Adds `AsAIAgent()` on `ChatClient` |
| `Microsoft.Extensions.AI` | 10.7.0 | `AIFunctionFactory.Create()` for tools |
| `Azure.AI.OpenAI` | 2.9.0-beta.1 | Azure OpenAI client |
| `Azure.Monitor.OpenTelemetry.Exporter` | 1.8.2 | Application Insights exporter |
| `OpenTelemetry` | 1.16.0 | Tracing and metrics pipeline |

---

## Further reading

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [MAF GitHub repository](https://github.com/microsoft/agent-framework)
- [Documentation/ReadMe.Frameworks.md](Documentation/ReadMe.Frameworks.md) – detailed framework comparison and pros/cons
