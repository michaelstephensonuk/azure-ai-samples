# Claude Code vs GitHub Copilot: OTEL Telemetry in Application Insights

Both Claude Code and GitHub Copilot can export OpenTelemetry data into an Application Insights
resource, and on the surface that sounds like "the same thing twice." In practice the two exporters
reflect genuinely different design philosophies, and that shows up in almost every query you write
against them. This document captures what we learned building dashboards against both, as a reference
for anyone querying this data directly and as source material for a presentation/video walkthrough.

## At a glance

| | Claude Code | GitHub Copilot |
|---|---|---|
| Primary App Insights table | `traces` (discrete named events) | `dependencies` (one row per outbound call/span) |
| Per-user identity | Yes — `user.email`, `user.id`, `org.name` | None, anywhere |
| Cost | Pre-computed `cost_usd` per event | Not emitted |
| Prompt tracking | First-class `prompt.id`, events ordered by `event.sequence` | Best-effort — prompt text is buried inside a larger field, no ordered event chain |
| Session tracking | `session.id` | `chat_session_id` (VS Code) or `conversation.id` (CLI) |
| Client sources | One (Claude Code CLI / IDE integration) | Two — VS Code extension and CLI — must be explicitly distinguished in every query |
| Field naming | Flat, Anthropic-specific (`input_tokens`, `cost_usd`, `model`) | OTEL GenAI semantic convention (`gen_ai.usage.input_tokens`, `gen_ai.request.model`) |
| Tool call detail | Two events: `tool_decision` (approved/rejected/auto) + `tool_result` (succeeded/failed) | One `execute_tool` row with a plain `success` bool |
| Error telemetry | Dedicated `claude_code.api_error` event | No dedicated error event observed — inferred from `success == false` |
| Reasoning effort | Flat `effort` field | Nested three JSON parses deep (`copilot_chat.request.options` → `output_config.effort`) |
| MCP server telemetry | Dedicated `mcp_server_connection` event | Not observed |
| Sub-feature tracking | `skill.name` (Claude Skills) | `gen_ai.agent.name` (main chat / auto-title / Next Edit Suggestions / inline edit) |

The rest of this document walks through each of these differences in more depth.

## 1. Table choice: `traces` vs `dependencies`

Claude Code's exporter writes **everything to `traces`** — every prompt, every model call, every tool
decision is its own named log-style event, discriminated by the native `message` column:

```
claude_code.user_prompt
claude_code.api_request
claude_code.api_error
claude_code.tool_decision
claude_code.tool_result
claude_code.mcp_server_connection
```

GitHub Copilot's exporter instead writes its richest data to **`dependencies`** — one row per
outbound call, distinguished by `gen_ai.operation.name` (`chat`, `execute_tool`, `embeddings`). This
gives GitHub Copilot's rows native columns Claude's don't have on the same row: `duration`, `success`,
`resultCode`, and a pre-bucketed `performanceBucket` (e.g. `"500ms-1sec"`). GitHub Copilot only touches
`traces` for one thing — counting session starts (`event.name == "copilot_chat.session.start"`) —
because that's the one case `traces` handles more cleanly than `dependencies`.

**Why it matters**: `traces` reads like an event log (`where message == "..."`), `dependencies` reads
like a call log (`where <operation> == "..."` plus native duration/success). If you're used to one
product's schema, the other's queries will look structurally different even when asking the same
question ("how many tool calls failed today?").

## 2. Field naming: bespoke vs semantic convention

Claude's `customDimensions` fields are **flat and Anthropic-specific**: `input_tokens`, `output_tokens`,
`cache_creation_tokens`, `cache_read_tokens`, `cost_usd`, `model`, `effort`. Anthropic invented these
names for Claude Code specifically.

GitHub Copilot's fields follow the **OTEL GenAI semantic convention** — an industry-standard schema
(`gen_ai.usage.input_tokens`, `gen_ai.request.model`, `gen_ai.operation.name`, `gen_ai.agent.name`)
shared by many GenAI tools, not just Copilot. That has a real consequence: because the field *names*
are generic, they could collide with telemetry from an unrelated GenAI tool sharing the same
Application Insights resource. Every GitHub Copilot query in this app filters on
`instrumentationlibrary.name == "copilot-chat"` (VS Code) or `"github-copilot"` (CLI) *first*, before
touching any `gen_ai.*` field, specifically to guard against that. Claude's bespoke naming doesn't need
this guard — nothing else plausibly emits a field literally called `cost_usd` the same way.

## 3. Identity and privacy

Claude Code emits real user and organization identity on every event: `user.email`, `user.id`,
`org.name`. That's what makes a per-user leaderboard, an inactive-user ("waste") report, or an
org-level rollup possible at all.

GitHub Copilot's telemetry has **no per-user identity anywhere** — not a blank field, an *absent* one.
Every GitHub Copilot dashboard in this app is necessarily workspace- or session-scoped, never
per-user, and that's a hard constraint baked into the data itself, not a feature we chose not to
build. Worth calling out explicitly in a presentation: someone will ask "can we see which developer is
using the most tokens?" and the honest answer for GitHub Copilot's default telemetry is no.

## 4. Prompt and session correlation

Claude Code gives every user prompt a durable `prompt.id`. Every event that happens while answering
that prompt — the model request, any tool decisions, tool results, an API error — carries the *same*
`prompt.id`, ordered by an explicit `event.sequence`. That means you can pull one prompt's complete
story back in the exact order it happened:

```
traces
| extend prompt_id_ = tostring(customDimensions.["prompt.id"])
| where prompt_id_ == '<id>'
| order by toint(customDimensions.["event.sequence"]) asc
```

`session.id` then groups many prompts into one working session.

GitHub Copilot has no equivalent "prompt" concept or ordered event chain. The closest thing is
`chat_session_id` (VS Code) or `conversation.id` (CLI) — a flatter, session-level correlation key,
with individual rows tied together mostly by timestamp rather than an explicit sequence number.
Reconstructing "the trace of one request" means pulling every row sharing that session key and sorting
by `timestamp`, not walking a designed sequence.

*Aside: neither product's exporter uses Application Insights' own native `operation_Id`
trace-correlation column for this — both roll their own correlation key in `customDimensions`
instead. (The Microsoft Agent SDK's OTEL export, by contrast, *does* use native `operation_Id` for
this — see that feature's own notes if you're comparing all three.)*

## 5. Prompt text capture

Claude Code puts the prompt text in its own clean field:

```
customDimensions.prompt   // on the claude_code.user_prompt event
```

GitHub Copilot's prompt text lives inside `copilot_chat.user_request`, which can also contain
hundreds of lines of injected environment/workspace context wrapped around the actual question.
Getting the human-readable question back out means matching a wrapper tag:

```
tostring(customDimensions.["copilot_chat.user_request"])
// then regex-extract <userRequest>...</userRequest> — the raw field is not display-ready
```

This is one of the more surprising differences in practice — the same "what did the user ask?"
question is a direct field read for Claude and a regex extraction for Copilot.

## 6. Token and cost fields

Both products track the same four token categories conceptually — input, output, cache-write
("creation"), cache-read — just named differently:

| Concept | Claude Code | GitHub Copilot |
|---|---|---|
| Input tokens | `customDimensions.input_tokens` | `customDimensions.["gen_ai.usage.input_tokens"]` |
| Output tokens | `customDimensions.output_tokens` | `customDimensions.["gen_ai.usage.output_tokens"]` |
| Cache write tokens | `customDimensions.cache_creation_tokens` | `customDimensions.["gen_ai.usage.cache_creation.input_tokens"]` |
| Cache read tokens | `customDimensions.cache_read_tokens` | `customDimensions.["gen_ai.usage.cache_read.input_tokens"]` |
| Cost (USD) | `customDimensions.cost_usd` — **pre-computed** | Not emitted at all |

Claude Code's `cost_usd` is a genuine differentiator: Anthropic's own client computes and emits a
dollar figure per event, so cost reporting is a straight `sum()`. GitHub Copilot emits no cost field
whatsoever — any cost figure for Copilot has to be estimated externally from token counts and a
model's published rate card, and even then GitHub doesn't publish Copilot per-token pricing the way
OpenAI/Anthropic publish API pricing, so a Copilot cost estimate is inherently softer than a Claude one.

## 7. Tool and sub-agent usage

Claude Code splits one tool call into **two separate events**: `tool_decision` records *how* the call
was authorized (`decision.source` — auto-approved, user-approved, rejected), and `tool_result` records
whether it *succeeded*. That's a meaningful governance signal — you can answer "how often do users have
to manually approve a tool call?" as a first-class query.

GitHub Copilot represents a tool call as a single `execute_tool` operation row with `gen_ai.tool.name`
and a native `success` bool — no approval/decision concept at all, since Copilot's tool-use model
doesn't have a comparable per-call authorization step to record.

Both products do track *which sub-feature* made a call, under different names: Claude Code's
`skill.name` identifies which Claude Skill ran; GitHub Copilot's `gen_ai.agent.name` identifies which
Copilot feature made the call (`"GitHub Copilot Chat"` for main chat, `"title"` for auto-titling,
`"XtabProvider"` for Next Edit Suggestions inline completions, `"panel/editAgent"` for inline edit).

## 8. Client / source differentiation

GitHub Copilot ships from **two genuinely different clients** that both write into the same telemetry
shape: the VS Code extension (`instrumentationlibrary.name == "copilot-chat"`) and the Copilot CLI
(`"github-copilot"`). Forgetting to include both in a filter is the single easiest way to silently
under-count Copilot usage — every query in this app's GitHub Copilot feature filters on both.

Claude Code doesn't have this split in the same way — there's one product surface, and
`terminal.type` / `service.name` describe the *environment* Claude Code is running in (which IDE
integration, which terminal) rather than two entirely separate client applications emitting
independently-shaped telemetry.

## 9. Error telemetry

Claude Code emits a dedicated `claude_code.api_error` event with structured error detail, so "show me
recent errors" is a direct query against a purpose-built event type.

GitHub Copilot has no equivalent dedicated error event in the telemetry we've explored — reliability
has to be inferred from `success == false` on `dependencies` rows, or (unconfirmed against real
Copilot telemetry) the generic Application Insights `exceptions` table, which is always a valid table
to query but isn't guaranteed to carry anything Copilot-specific.

## 10. Practical takeaways

- **Both schemas need injection-style escaping.** Prompt text, tool arguments, and session/conversation
  IDs are all client-emitted content, not trusted application input — any value that gets interpolated
  into a follow-up KQL query needs the same escaping discipline you'd apply to user input in a SQL
  query, regardless of which product it came from.
- **Always scope GitHub Copilot queries by `instrumentationlibrary.name` before touching any
  `gen_ai.*` field.** Because those field names are a shared industry convention, not touching this
  filter first risks pulling in (or being contaminated by) an unrelated GenAI tool's telemetry sharing
  the same Application Insights resource.
- **Claude Code's flat, bespoke fields are simpler to query today, but track a single vendor's naming
  choices** that could change between Claude Code versions without notice. GitHub Copilot's adherence
  to the OTEL GenAI semantic convention is more likely to stay portable across tooling in the long run,
  at the cost of shallower per-product detail (no prompt-level narrative, no cost, no tool-approval
  signal).
- **If per-user or per-org reporting is a requirement, Claude Code's telemetry is the one of the two
  that supports it today.** GitHub Copilot's default export has no per-user identity to report against,
  full stop.
