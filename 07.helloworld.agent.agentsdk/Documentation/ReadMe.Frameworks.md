# Framework Comparison: Microsoft.Extensions.AI vs Microsoft Agent Framework

This document compares the two agent frameworks used in this sample series, to help you choose the right abstraction for your use case.

---

## What are these frameworks?

### Microsoft.Extensions.AI (`Microsoft.Extensions.AI`)
A **low-level abstraction** over AI model providers.  It defines standard interfaces (`IChatClient`, `IEmbeddingGenerator`) and a composable middleware pipeline.  Think of it as the `HttpClient` of the AI world — flexible, low-level, and close to the wire.

- **GitHub**: [dotnet/extensions](https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.AI)
- **NuGet**: `Microsoft.Extensions.AI`
- **Target audience**: Library authors, teams building custom agent loops, anyone who needs fine-grained control

### Microsoft Agent Framework (`Microsoft.Agents.AI`)
A **high-level agent abstraction** built on top of `Microsoft.Extensions.AI`.  It provides the `AIAgent` type and manages the tool-call loop, session management, and streaming for you.

- **GitHub**: [microsoft/agent-framework](https://github.com/microsoft/agent-framework)
- **NuGet**: `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`
- **Target audience**: Application developers who want a productive, opinionated agent API without building the loop themselves

---

## Side-by-side API comparison

### Creating a chat client / agent

**Microsoft.Extensions.AI (Sample 05)**
```csharp
IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()          // handles tool-call loop
    .UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)
    .Build();
```

**Microsoft Agent Framework (Sample 07)**
```csharp
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: systemPrompt,   // system prompt baked in at creation
        name: "helloworld-agent",
        tools: [tool1, tool2, tool3]  // tools registered once
    );
```

---

### Running the agent

**Microsoft.Extensions.AI (Sample 05)**
```csharp
var messages = new List<ChatMessage>
{
    new(ChatRole.System, systemPrompt),
    new(ChatRole.User, userPrompt),
};
var options = new ChatOptions { Tools = [tool1, tool2, tool3], ToolMode = ChatToolMode.Auto };

ChatResponse response = await chatClient.GetResponseAsync(messages, options);
string text = response.Text;
int inputTokens = response.Usage?.InputTokenCount ?? 0;
```

**Microsoft Agent Framework (Sample 07)**
```csharp
// System prompt and tools are already registered on the agent
string response = await agent.RunAsync(userPrompt);
// Token counts not returned by RunAsync() — use RunStreamingAsync() if needed
```

---

## Pros and cons

### Microsoft.Extensions.AI

| ✅ Pros | ❌ Cons |
|---------|---------|
| Fine-grained control over every call | More boilerplate for tool-call loops |
| Access to token usage per call | Must manage message history manually |
| Composable middleware (logging, caching, retry, OTel) | OTel wiring is explicit — easy to get wrong |
| Works with any `IChatClient`-compatible provider | Lower-level = more surface area |
| Stable, part of .NET ecosystem | |

### Microsoft Agent Framework

| ✅ Pros | ❌ Cons |
|---------|---------|
| Minimal boilerplate — `AsAIAgent()` + `RunAsync()` | Token usage not returned from `RunAsync()` |
| Tool-call loop managed internally | Less control over individual LLM calls |
| Session management built in (`CreateSessionAsync`) | OTel still not fully automatic (wired at IChatClient level internally) |
| Streaming support via `RunStreamingAsync()` | Newer, smaller community |
| Built on ME.AI — same OTel activity sources | Requires both `Microsoft.Agents.AI` + provider package |
| Clear agent identity (`name:`, `instructions:`) | |

---

## OpenTelemetry / Application Insights comparison

Both frameworks ultimately emit the same `gen_ai.*` semantic-convention spans because MAF uses `Microsoft.Extensions.AI` internally.

| Telemetry aspect | Sample 05 (ME.AI) | Sample 07 (MAF) |
|------------------|-------------------|-----------------|
| `gen_ai.system` attribute | ✅ (from `UseOpenTelemetry()`) | ✅ (same, via internal ME.AI layer) |
| `gen_ai.request.model` | ✅ | ✅ |
| `gen_ai.usage.input_tokens` metric | ✅ | ✅ |
| Token counts on `ChatResponse` | ✅ (`response.Usage`) | ❌ (`RunAsync` returns string) |
| `gen_ai.agent.name` on spans | ❌ (requires custom span) | ❌ (requires custom span) |
| App Insights Agent Preview | ✅ (with custom dependency span) | ✅ (same custom dependency span) |
| `genAIContent` table | ✅ (with `EnableSensitiveData`) | ✅ (same configuration) |

**Conclusion**: The telemetry is functionally equivalent between the two samples.  The custom `Invoke Hello World Agent` dependency span with `gen_ai.agent.name` / `gen_ai.operation.name = "invoke_agent"` is required in **both** samples to appear in the App Insights Agent Preview blade — neither framework sets these tags automatically.

---

## When to choose which framework

| Use case | Recommended |
|----------|-------------|
| Building a library or SDK | `Microsoft.Extensions.AI` |
| Rapid application prototyping | **Microsoft Agent Framework** |
| Custom retry, caching, or rate-limiting middleware | `Microsoft.Extensions.AI` |
| Multi-turn conversation sessions | **Microsoft Agent Framework** (built-in session support) |
| Strict token budget management | `Microsoft.Extensions.AI` (usage per call) |
| Simple "ask a question with tools" pattern | **Microsoft Agent Framework** |
| Max provider portability | `Microsoft.Extensions.AI` (`IChatClient` works with any provider) |

---

## Further reading

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions)
- [Microsoft Agent Framework docs](https://learn.microsoft.com/en-us/agent-framework/)
- [MAF GitHub samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples)
- [Gen AI semantic conventions (OpenTelemetry)](https://opentelemetry.io/docs/specs/semconv/gen-ai/)
