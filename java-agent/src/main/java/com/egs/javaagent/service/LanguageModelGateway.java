package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentRequest;
import com.egs.javaagent.model.ConversationMemoryEntry;

import java.util.List;

public interface LanguageModelGateway {

    LanguageModelResult respond(
        AgentRequest request,
        String planSummary,
        List<ConversationMemoryEntry> memory,
        ProjectContextSnapshot projectSnapshot
    );
}
