package com.egs.javaagent.model;

public record AgentActionExecutionResult(
    String type,
    String target,
    boolean success,
    String output
) {
}
