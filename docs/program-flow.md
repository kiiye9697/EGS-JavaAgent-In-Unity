# Program Flow

## Unity Request Flow

```mermaid
sequenceDiagram
    actor User
    participant Window as JavaAgentWindow
    participant State as JavaAgentSessionState
    participant Launcher as LocalJavaAgentController
    participant Client as JavaAgentClient
    participant Server as Java Agent HTTP Server
    participant Orchestrator as AgentOrchestrator
    participant Gateway as LLM Gateway

    User->>Window: Enter request and click Send
    Window->>State: SendPromptAsync()
    State->>Launcher: EnsureAgentRunningAsync()
    Launcher-->>State: health/start result
    State->>State: Build AgentEnvelope from scene, selection, compile state
    State->>Client: ExecuteAsync(endpoint, envelope)
    Client->>Server: POST /v1/agent/execute
    Server->>Orchestrator: handle(envelope)
    Orchestrator->>Gateway: respond(request, plan, memory, projectSnapshot)
    Gateway-->>Orchestrator: assistant message, tool traces, proposals
    Orchestrator-->>Server: AgentResponse
    Server-->>Client: JSON response
    Client-->>State: AgentResponse
    State->>State: Rebuild approval queue and update diagnostics
    Window-->>User: Show nodes, transcript, approvals, issues
```

## Approval and Apply Flow

```mermaid
flowchart TD
    A["Agent returns suggested action"] --> B{"Approval required?"}
    B -- No --> C["Show result only"]
    B -- Yes --> D["Add to approval queue"]
    D --> E["User reviews proposal and diff"]
    E --> F{"Approve?"}
    F -- No --> G["Reject and remove"]
    F -- Yes --> H["Snapshot existing target"]
    H --> I["Send approved action to backend"]
    I --> J["WriteActionExecutor writes file"]
    J --> K["Refresh AssetDatabase"]
    K --> L["Record AppliedChangeRecord"]
    L --> M{"Auto attach script?"}
    M -- Yes --> N["Attach MonoBehaviour to selected GameObject after compile"]
    M -- No --> O["Wait for next request"]
```

## Compile Repair Flow

```mermaid
flowchart LR
    A["Unity compiler diagnostics"] --> B{"Has errors?"}
    B -- No --> C["Stable"]
    B -- Yes --> D{"Auto repair enabled?"}
    D -- No --> E["User opens Debug Console and clicks Repair"]
    D -- Yes --> F["Queue repair prompt"]
    E --> G["Send repair request"]
    F --> G
    G --> H["Agent proposes smallest fix"]
    H --> I["Approval queue"]
    I --> J["Apply and recompile"]
```

## Node Meaning

- Skill: selected built-in profile such as Shader, Material, Function, Validation, Scene, or Project.
- Reference: local files or URLs attached to the request.
- Inspect: file reads, scene selection, project scan, and compiler context.
- Approve: proposed writes waiting for human or safe auto approval.
- Apply: approved writes, snapshots, focus/open helpers, and script attach.
- Repair: compiler-error feedback loop.
