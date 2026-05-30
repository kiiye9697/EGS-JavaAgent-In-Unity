package com.egs.javaagent.model;

import java.util.List;

public record ConversationMemoryEntry(
    String sessionId,
    String createdAt,
    String userMessage,
    String assistantSummary,
    List<String> tags
) {
}
