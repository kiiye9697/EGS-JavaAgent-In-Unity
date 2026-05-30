package com.egs.javaagent.model;

public record AgentToolExecutionSummary(
    int attemptedActions,
    int successfulActions,
    int failedActions,
    String summary
) {
}
