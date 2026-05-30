package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentSuggestedAction;

import java.util.List;

public record LanguageModelResult(
    String assistantMessage,
    List<ModelToolTrace> toolTraces,
    List<AgentSuggestedAction> suggestedActions
) {

    public static LanguageModelResult of(String assistantMessage) {
        return new LanguageModelResult(assistantMessage, List.of(), List.of());
    }

    public static LanguageModelResult of(
        String assistantMessage,
        List<ModelToolTrace> toolTraces,
        List<AgentSuggestedAction> suggestedActions
    ) {
        return new LanguageModelResult(assistantMessage, toolTraces, suggestedActions);
    }
}
