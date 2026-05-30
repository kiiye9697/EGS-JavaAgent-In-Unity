package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentRequest;
import com.egs.javaagent.model.ConversationMemoryEntry;

import java.util.List;

public final class StubLanguageModelGateway implements LanguageModelGateway {

    private final ModelProviderSettings providerSettings;

    public StubLanguageModelGateway(ModelProviderSettings providerSettings) {
        this.providerSettings = providerSettings;
    }

    @Override
    public LanguageModelResult respond(
        AgentRequest request,
        String planSummary,
        List<ConversationMemoryEntry> memory,
        ProjectContextSnapshot projectSnapshot
    ) {
        return LanguageModelResult.of("""
            This is the bootstrap agent response.
            Request: %s
            Active scene: %s
            Unity version: %s
            Selected assets: %d
            Selected objects: %d
            Provider: %s
            Token configured: %s
            Project files discovered: %d
            Selected file snippets loaded: %d
            Reference documents loaded: %d
            Suggested next action: inspect the related Unity assets or Java handlers before generating code.
            Plan:
            %s
            Memory recalled: %d entries
            """.formatted(
            request.userMessage(),
            request.activeSceneName(),
            request.unityVersion(),
            request.selectedAssets().size(),
            request.selectedObjects().size(),
            providerSettings.providerName(),
            providerSettings.apiKeyPresent() ? "yes" : "no",
            projectSnapshot.discoveredFiles().size(),
            projectSnapshot.snippets().size(),
            projectSnapshot.references().size(),
            planSummary.trim(),
            memory.size()
        ));
    }
}
