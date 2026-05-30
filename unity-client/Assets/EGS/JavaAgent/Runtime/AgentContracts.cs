using System;
using UnityEngine;

namespace EGS.JavaAgent.Runtime
{
    public enum AgentMode
    {
        Ask,
        Agent,
        Plan
    }

    [Serializable]
    public sealed class AgentEnvelope
    {
        public string requestId;
        public string sessionId;
        public string type;
        public AgentPayload payload;
        public AgentMetadata metadata;
    }

    [Serializable]
    public sealed class AgentPayload
    {
        public string userMessage;
        public string mode = "agent";
        public string sceneContext;
        public string activeSceneName;
        public string projectPath;
        public string[] selectedAssets = Array.Empty<string>();
        public string[] selectedObjects = Array.Empty<string>();
        public string projectSnapshot;
        public string compileState;
        public string[] compilerMessages = Array.Empty<string>();
        public string[] referenceInputs = Array.Empty<string>();
        public AgentApprovedAction[] approvedActions = Array.Empty<AgentApprovedAction>();
    }

    [Serializable]
    public sealed class AgentMetadata
    {
        public string unityVersion;
        public string clientVersion;
        public string timestampUtc;
    }

    [Serializable]
    public sealed class AgentResponse
    {
        public bool success;
        public string status;
        public string planSummary;
        public string assistantMessage;
        public AgentPlanStep[] planSteps = Array.Empty<AgentPlanStep>();
        public AgentDiagnostics diagnostics;
        public AgentSuggestedAction[] suggestedActions = Array.Empty<AgentSuggestedAction>();
        public AgentActionExecutionResult[] actionExecutionResults = Array.Empty<AgentActionExecutionResult>();
        public AgentToolExecutionSummary toolExecutionSummary;
        public AgentIssue[] detectedIssues = Array.Empty<AgentIssue>();
    }

    [Serializable]
    public sealed class AgentPlanStep
    {
        public string title;
        public string detail;
        public string status;
    }

    [Serializable]
    public sealed class AgentDiagnostics
    {
        public int memoryHits;
        public string mode;
        public string requestType;
        public string activeSceneName;
        public int selectedAssetCount;
        public int selectedObjectCount;
        public string providerName;
        public string modelName;
        public string gatewayKind;
        public string effectiveModelName;
        public bool apiKeyPresent;
        public string compileState;
        public int compilerMessageCount;
    }

    [Serializable]
    public sealed class RequestHistoryItem
    {
        public string timestampUtc;
        public string mode;
        public string promptPreview;
        public string providerName;
        public string modelName;
        public string status;
    }

    [Serializable]
    public sealed class AgentSuggestedAction
    {
        public string type;
        public string target;
        public string reason;
        public string proposalPreview;
        public bool approvalRequired;
    }

    [Serializable]
    public sealed class AgentApprovedAction
    {
        public string type;
        public string target;
        public string reason;
        public string proposalPreview;
    }

    [Serializable]
    public sealed class AgentActionExecutionResult
    {
        public string type;
        public string target;
        public bool success;
        public string output;
    }

    [Serializable]
    public sealed class AgentToolExecutionSummary
    {
        public int attemptedActions;
        public int successfulActions;
        public int failedActions;
        public string summary;
    }

    [Serializable]
    public sealed class AgentIssue
    {
        public string severity;
        public string category;
        public string target;
        public string detail;
    }
}
