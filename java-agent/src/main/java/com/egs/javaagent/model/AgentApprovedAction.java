package com.egs.javaagent.model;

public record AgentApprovedAction(
    String type,
    String target,
    String reason,
    String proposalPreview
) {
}
