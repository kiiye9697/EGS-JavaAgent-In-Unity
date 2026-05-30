package com.egs.javaagent.model;

import java.util.List;

public record AgentRequest(
    String userMessage,
    String mode,
    String sceneContext,
    String projectPath,
    String activeSceneName,
    List<String> selectedAssets,
    List<String> selectedObjects,
    String unityVersion,
    String projectSnapshot,
    String compileState,
    List<String> compilerMessages,
    List<String> referenceInputs,
    List<AgentApprovedAction> approvedActions
) {
}
