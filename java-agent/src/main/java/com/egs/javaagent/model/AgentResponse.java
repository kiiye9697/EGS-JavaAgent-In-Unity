package com.egs.javaagent.model;

import java.util.List;

public record AgentResponse(
    boolean success,
    String status,
    String planSummary,
    String assistantMessage,
    List<AgentPlanStep> planSteps,
    AgentDiagnostics diagnostics,
    List<AgentSuggestedAction> suggestedActions,
    List<AgentActionExecutionResult> actionExecutionResults,
    AgentToolExecutionSummary toolExecutionSummary,
    List<AgentIssue> detectedIssues
) {
}
