package com.egs.javaagent.service;

public record ModelProviderSettings(
    String providerName,
    String modelName,
    String apiKey,
    String apiBaseUrl,
    String gatewayKind
) {

    public boolean apiKeyPresent() {
        return apiKey != null && !apiKey.isBlank();
    }

    public String effectiveModelName() {
        if ("langchain4j".equalsIgnoreCase(gatewayKind)
            && "deepseek".equalsIgnoreCase(providerName)
            && "deepseek-v4-flash".equalsIgnoreCase(modelName)) {
            return "deepseek-chat";
        }

        return modelName;
    }
}
