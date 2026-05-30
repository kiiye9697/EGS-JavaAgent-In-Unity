package com.egs.javaagent.model;

public record AgentDiagnostics(
    int memoryHits,
    String mode,
    String requestType,
    String activeSceneName,
    int selectedAssetCount,
    int selectedObjectCount,
    String providerName,
    String modelName,
    String gatewayKind,
    String effectiveModelName,
    boolean apiKeyPresent,
    String compileState,
    int compilerMessageCount
) {
}
