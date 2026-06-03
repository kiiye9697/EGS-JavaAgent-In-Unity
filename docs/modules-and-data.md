# Modules and Data Structures

## Functional Modules

| Module | Main Classes | Responsibility |
|---|---|---|
| Workspace UI | `JavaAgentWindow` | Main bilingual UI, responsive layout, node flow, prompt input, transcript and status display. |
| Settings | `JavaAgentSettings`, `JavaAgentSettingsProvider` | Endpoint, provider, model, local Java launch config, API key storage. |
| Approval | `JavaAgentApprovalWindow`, `PendingApprovalItem` | Review generated proposals, diff previews, approve or reject writes. |
| Debug and Repair | `JavaAgentDebugWindow`, `CompileDiagnosticsTracker` | Compiler messages, repair trigger, rollback, asset refresh, script attach. |
| Unity Client | `JavaAgentClient`, `AgentContracts` | JSON HTTP communication with the Java backend. |
| Java Server | `AgentHttpServer`, `JavaAgentApplication` | Local HTTP service and gateway selection. |
| Agent Core | `AgentOrchestrator` | Request validation, project scan, memory, suggested actions, write execution, diagnostics. |
| Model Gateway | `LangChain4jLanguageModelGateway`, `OpenAiCompatibleLanguageModelGateway`, `StubLanguageModelGateway` | Real or fallback LLM responses. |

## Core Data Structures

| Data Structure | Location | Purpose |
|---|---|---|
| `AgentEnvelope` | Unity Runtime | Request wrapper with request id, session id, payload and metadata. |
| `AgentPayload` | Unity Runtime | User message, scene context, selected assets, compiler messages, reference inputs and approved actions. |
| `AgentResponse` | Unity Runtime / Java model | Status, plan, assistant message, diagnostics, suggested actions, action results and issues. |
| `AgentSuggestedAction` | Unity Runtime / Java model | A reviewable model proposal such as create/replace file. |
| `AgentApprovedAction` | Unity Runtime / Java model | A user-approved action that the backend may execute. |
| `AppliedChangeRecord` | `JavaAgentSessionState` | Snapshot metadata used for rollback and asset focus. |
| `CompileSnapshot` | `CompileDiagnosticsTracker` | Unity compile status, error count, warning count and compiler messages. |
| `ModelProviderSettings` | Java service | Provider, model, API key, base URL and gateway kind. |

## Key Algorithms

### Proposal Extraction

1. Detect whether the user request has write intent.
2. Ask the selected gateway for an assistant response and tool suggestions.
3. Extract fenced code blocks from assistant text when needed.
4. Infer target path from explicit `Assets/...` paths, class names, or shader declarations.
5. Convert the result into `suggest_create_file` or `suggest_replace_file`.
6. Queue the proposal for approval instead of writing immediately.

### Safe Apply and Rollback

1. Resolve the target path inside the Unity project.
2. Capture whether the target existed and its previous content.
3. Send approved action to the backend.
4. Write file only when the action is approved.
5. Store `AppliedChangeRecord`.
6. Rollback restores previous content or deletes the newly created file.

### Local Java Launch

1. Resolve Java command, working directory and classpath relative to the Unity project.
2. Inject `EGS_AGENT_PROVIDER`, `EGS_AGENT_MODEL`, `EGS_AGENT_GATEWAY`.
3. Inject provider key as `DEEPSEEK_API_KEY`, `OPENAI_API_KEY`, or `GLM_API_KEY`.
4. Start the local Java process without a visible console window.
5. Poll `/health` before sending requests.
