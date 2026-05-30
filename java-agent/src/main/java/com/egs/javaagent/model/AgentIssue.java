package com.egs.javaagent.model;

public record AgentIssue(
    String severity,
    String category,
    String target,
    String detail
) {
}
