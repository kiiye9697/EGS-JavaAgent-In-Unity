package com.egs.javaagent.model;

public record AgentSuggestedAction(
    String type,
    String target,
    String reason,
    String proposalPreview,
    boolean approvalRequired
) {
}
