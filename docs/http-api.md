# HTTP API

## Endpoints

### `GET /health`

Health check for the Java service.

### `POST /v1/agent/execute`

Main Unity-to-Java agent endpoint.

## Request Shape

The request envelope contains:

- `requestId`
- `sessionId`
- `type`
- `payload`
- `metadata`

The payload typically includes:

- `userMessage`
- `mode`
- `sceneContext`
- `activeSceneName`
- `projectPath`
- `selectedAssets`
- `selectedObjects`
- `projectSnapshot`
- `approvedActions`

## Response Shape

The response may include:

- `assistantMessage`
- `planSteps`
- `diagnostics`
- `suggestedActions`
- `actionExecutionResults`
- `toolExecutionSummary`
- `detectedIssues`

## Approval Flow

If `approvedActions` is not empty:

1. the orchestrator skips the model call
2. reviewed file actions are applied locally
3. execution results are returned to Unity
