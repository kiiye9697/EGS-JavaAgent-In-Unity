# Architecture Review Draft

This file is the current approval-facing architecture summary.

## High-Level Architecture

```mermaid
flowchart LR
    U["Unity Editor Window"] --> C["JavaAgentClient"]
    C --> H["AgentHttpServer"]
    H --> O["AgentOrchestrator"]
    O --> S["ProjectContextScanner"]
    O --> M["FileMemoryStore"]
    O --> R["ReadOnlyActionExecutor"]
    O --> W["WriteActionExecutor"]
    O --> I["ProjectIssueDetector"]
    O --> P["ProjectPathResolver"]
    O --> G1["HTTP Gateway"]
    O --> G2["LangChain4j Gateway"]
    G2 --> T["LangChain Project Tools"]
    G1 --> API["LLM Provider API"]
    G2 --> API
    O --> RESP["Structured AgentResponse"]
    RESP --> C
    C --> U
```

## Two-Phase Edit Flow

```mermaid
flowchart TD
    A["User prompt in Unity"] --> B["Java scans project context"]
    B --> C["LangChain4j inspects files with tools"]
    C --> D["Model returns assistant text and reviewed proposal"]
    D --> E["Unity shows proposal preview"]
    E --> F{"User approves?"}
    F -- "No" --> G["Stop after review"]
    F -- "Yes" --> H["Unity sends approvedActions"]
    H --> I["Java skips model call"]
    I --> J["WriteActionExecutor applies file write locally"]
    J --> K["Unity shows execution result"]
    K --> L["Unity compiler diagnostics become next-turn context"]
```

## UML

```mermaid
classDiagram
    class AgentEnvelope {
      +requestId
      +sessionId
      +type
      +payload
      +metadata
    }

    class AgentPayload {
      +userMessage
      +mode
      +sceneContext
      +activeSceneName
      +projectPath
      +selectedAssets
      +selectedObjects
      +projectSnapshot
      +compileState
      +compilerMessages
      +approvedActions
    }

    class AgentApprovedAction {
      +type
      +target
      +reason
      +proposalPreview
    }

    class AgentSuggestedAction {
      +type
      +target
      +reason
      +proposalPreview
      +approvalRequired
    }

    class AgentResponse {
      +success
      +status
      +planSummary
      +assistantMessage
      +planSteps
      +diagnostics
      +suggestedActions
      +actionExecutionResults
      +toolExecutionSummary
      +detectedIssues
    }

    class AgentOrchestrator
    class LangChain4jLanguageModelGateway
    class WriteActionExecutor
    class ProjectPathResolver

    AgentEnvelope --> AgentPayload
    AgentPayload --> AgentApprovedAction
    AgentResponse --> AgentSuggestedAction
    AgentOrchestrator --> LangChain4jLanguageModelGateway
    AgentOrchestrator --> WriteActionExecutor
    WriteActionExecutor --> ProjectPathResolver
```

## Approval Notes

This architecture is ready for testing if you accept these current boundaries:

1. approved writes are full-file create or full-file replace
2. proposal generation is model-driven
3. proposal application is local and deterministic
4. compile errors are captured on the Unity side and returned as next-turn context
