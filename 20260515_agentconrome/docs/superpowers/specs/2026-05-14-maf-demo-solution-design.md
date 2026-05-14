# MAF Demo Solution — Design

**Date:** 2026-05-14
**Talk:** AgentCon Rome — 2026-05-15
**Goal:** A .NET 10 solution (`MAF/MAF.slnx`) with 7 small, didactic demo projects showcasing
Microsoft Agent Framework features, each instrumented with OpenTelemetry.

## Decisions (from brainstorming)

- **LLM backend:** Azure OpenAI accessed through the OpenAI SDK shape:
  `new OpenAIClient(new ApiKeyCredential(key), new() { Endpoint = new Uri(endpoint) }).GetChatClient("gpt-5.4-mini").AsIChatClient()`.
  Endpoint: `https://foundryws2025.openai.azure.com/openai/v1`.
- **Observability:** OpenTelemetry with **OTLP exporter** to `localhost:4317`. The user collects the
  data with their own collector — no Docker/Aspire setup is part of this solution.
- **Dependencies:** NuGet packages `Microsoft.Agents.AI.*`, latest preview `1.6.1-preview.260514.1`
  (verify each package version at implementation time).
- **Declarative YAML (#5):** Both a declarative *agent* from YAML and a declarative *workflow* from YAML.

## Security

The Azure API key lives in `MAF/appsettings.json`, which is **git-ignored**. Only
`MAF/appsettings.template.json` (placeholder values) is committed. The key pasted into the
brainstorming chat should be **rotated** after the talk.

## Architecture

```
MAF/
  MAF.slnx
  appsettings.json            (git-ignored — real endpoint + key)
  appsettings.template.json   (committed — placeholders)
  .gitignore
  Maf.Demo.Shared/            (class library)
    ChatClientFactory.cs      builds IChatClient from config
    Telemetry.cs              OTel TracerProvider/MeterProvider/logging + OTLP exporter
    DemoConfig.cs             reads MAF/appsettings.json
  D1.SequentialDevUI/         (ASP.NET Web)
  D2.NestedOrchestration/     (Console)
  D3.Middleware/              (Console)
  D4.TokensGuardrails/        (Console)
  D5.DeclarativeYaml/         (Console)
  D6.ObservableWorkflow/      (Console)
  D7.HumanInTheLoop/          (Console)
```

All projects target `net10.0`, reference `Maf.Demo.Shared`, and use NuGet `Microsoft.Agents.AI.*`.
`appsettings.json` is linked into each project with `CopyToOutputDirectory`.

### Shared library — `Maf.Demo.Shared`

- `DemoConfig` — binds `appsettings.json` (endpoint, key, model, OTLP endpoint), env-var override.
- `ChatClientFactory.Create()` — returns the configured `IChatClient`.
- `Telemetry.ConfigureConsole(serviceName)` — builds OTel providers for console apps with sources
  `Microsoft.Agents.AI.*`, `Microsoft.Extensions.AI.*`, OTLP exporter. Web app uses
  `builder.Services.AddOpenTelemetry()` directly.

## The 7 demo projects

Agents are intentionally trivial (writing/translation assistants) so the focus stays on the
*mechanism*. Each project ships a `README.md` with example chat prompts for live demo.

### D1 — Sequential orchestrator in DevUI (`D1.SequentialDevUI`, ASP.NET Web)
3-agent sequential pipeline Brainstormer → Writer → Editor built with
`AgentWorkflowBuilder.BuildSequential`, registered via the hosting packages and exposed through
`MapDevUI()`. Packages: `Microsoft.Agents.AI.DevUI`, `.Hosting`, `.Hosting.OpenAI`.
OTel via `builder.Services.AddOpenTelemetry()...UseOtlpExporter()`.

### D2 — Custom nested orchestration (`D2.NestedOrchestration`, Console)
Outer **Sequential** workflow: [Triage agent] → [inner **GroupChat** sub-workflow of 2 specialist
agents] → [Summarizer agent]. Demonstrates composing `AgentWorkflowBuilder` patterns and nesting a
workflow inside another.

### D3 — The three middleware types (`D3.Middleware`, Console)
One simple agent with one function tool, wired with all three middleware kinds:
1. **Chat client middleware** — `ChatClientBuilder.Use(...)`, intercepts LLM calls.
2. **Agent run middleware** — `AIAgentBuilder.Use(runFunc, ...)`, intercepts a full agent run.
3. **Function invocation middleware** — `AIAgentBuilder.Use(funcInvocationFunc)`, intercepts tool calls.
Each middleware logs on enter/exit so the pipeline order is visible in the console.

### D4 — Token controls & guardrails (`D4.TokensGuardrails`, Console)
- **Tokens:** read `response.Usage` (input/output token counts), set `MaxOutputTokens`, and an
  agent-run middleware enforcing a cumulative token budget that aborts when exceeded.
- **Guardrails:** an input-guardrail middleware (blocks disallowed topics before the LLM call) and
  an output-guardrail middleware (redacts/refuses unsafe content after the response).

### D5 — Declarative YAML (`D5.DeclarativeYaml`, Console)
Two parts:
(a) Agent loaded from a `.yaml` file via `ChatClientPromptAgentFactory.CreateFromYamlAsync`.
(b) Declarative workflow loaded from a `.yaml` file.
**Risk:** the official declarative-workflow sample uses `Microsoft.Agents.AI.Workflows.Declarative.Foundry`
and appears to need an Azure AI Foundry project endpoint. Investigate at implementation time. If
Foundry is required, the agent-YAML part stays fully functional and the workflow-YAML part is
documented with its requirement plus a minimal example.

### D6 — Fully observable workflow (`D6.ObservableWorkflow`, Console)
A simple 4-agent workflow with OpenTelemetry enabled at **every level** (chat client, agent,
workflow) using `UseOpenTelemetry(... EnableSensitiveData = true)`. The trace shows messages passed
between agents, tool calls, and turn changes — "behind the scenes" made explicit.

### D7 — Human in the loop (`D7.HumanInTheLoop`, Console)
HITL via the workflow `RequestPort` / `ExternalRequest` / `ExternalResponse` mechanism: an agent
proposes a sensitive action, the workflow emits a `RequestInfoEvent` and pauses, the console asks
the human Y/N, and the response is fed back into the workflow. OTel-instrumented.

## Build / run

- `dotnet build MAF/MAF.slnx`
- Console demos: `dotnet run --project MAF/D2.NestedOrchestration`
- D1: `dotnet run --project MAF/D1.SequentialDevUI`, then open the printed DevUI URL.

## Implementation order

Shared lib + solution → D1 → D2 → D3 → D4 → D5 → D6 → D7. One project at a time, confirming with
the user when non-trivial doubts arise.
