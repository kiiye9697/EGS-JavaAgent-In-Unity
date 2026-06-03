# Architecture and UML

## Main Modules

- Unity Editor Client: workspace UI, settings, approval queue, debug console, compile diagnostics, and local Java process launcher.
- Unity Runtime Contracts: JSON DTOs shared by the editor client and Java HTTP API.
- Java HTTP Server: `/health` and `/v1/agent/execute` endpoints.
- Agent Orchestrator: validates requests, scans project context, manages memory, merges suggested actions, executes approved writes, and returns diagnostics.
- LLM Gateway: `LangChain4jLanguageModelGateway`, `OpenAiCompatibleLanguageModelGateway`, or `StubLanguageModelGateway`.

## UML Class Diagram

```mermaid
classDiagram
    class JavaAgentWindow {
        -Vector2 _windowScroll
        -Vector2 _promptScroll
        +OpenWorkspace()
        -OnGUI()
        -DrawPipeline(snapshot)
        -DrawSelectedNodeCard()
        -DrawPromptCard()
        -DrawActionCard()
    }

    class JavaAgentSessionState {
        +Prompt string
        +Response string
        +Mode AgentMode
        +SkillProfile JavaAgentSkillProfile
        +SelectedWorkflowNode WorkflowNode
        +SendPromptAsync()
        +ApplyApprovalAsync(item)
        +RepairFromCompilerErrorsAsync(triggeredAutomatically)
        +RollbackAppliedChange(changeId)
        +FocusTarget(target, openAsset)
    }

    class JavaAgentSettings {
        +endpoint string
        +provider string
        +gateway string
        +model string
        +HasConfiguredProviderApiKey() bool
        +GetLocalProviderApiKey() string
        +SetLocalProviderApiKey(apiKey)
    }

    class LocalJavaAgentController {
        +IsHealthyAsync(endpoint) Task~bool~
        +Start(settings, message) bool
    }

    class JavaAgentClient {
        +ExecuteAsync(endpoint, envelope) Task~AgentResponse~
    }

    class AgentEnvelope
    class AgentPayload
    class AgentResponse
    class AgentSuggestedAction
    class AgentActionExecutionResult

    class AgentHttpServer {
        +start()
        -handleHealth(exchange)
        -handleExecute(exchange)
    }

    class AgentOrchestrator {
        +handle(envelope) AgentResponse
        -buildSuggestedActions(request, snapshot)
        -inferSuggestedActionsFromAssistantMessage(...)
    }

    class LanguageModelGateway {
        <<interface>>
        +respond(request, planSummary, memory, snapshot) LanguageModelResult
    }

    class LangChain4jLanguageModelGateway
    class OpenAiCompatibleLanguageModelGateway
    class StubLanguageModelGateway

    JavaAgentWindow --> JavaAgentSessionState
    JavaAgentWindow --> JavaAgentSettings
    JavaAgentSessionState --> JavaAgentClient
    JavaAgentSessionState --> LocalJavaAgentController
    JavaAgentClient --> AgentEnvelope
    AgentResponse --> AgentSuggestedAction
    AgentResponse --> AgentActionExecutionResult
    AgentHttpServer --> AgentOrchestrator
    AgentOrchestrator --> LanguageModelGateway
    LanguageModelGateway <|.. LangChain4jLanguageModelGateway
    LanguageModelGateway <|.. OpenAiCompatibleLanguageModelGateway
    LanguageModelGateway <|.. StubLanguageModelGateway
```

## Key Design Choices

- The Unity plugin never writes files directly from free-form model text. It queues proposals, then applies only approved actions.
- Applied writes are snapshotted so the debug console can roll them back.
- Provider keys are not serialized into Unity assets.
- LangChain4j is the default tool-calling gateway. The HTTP gateway remains available for provider compatibility.
