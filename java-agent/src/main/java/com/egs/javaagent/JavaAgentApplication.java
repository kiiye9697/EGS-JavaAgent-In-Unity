package com.egs.javaagent;

import com.egs.javaagent.http.AgentHttpServer;
import com.egs.javaagent.service.AgentOrchestrator;
import com.egs.javaagent.service.FileMemoryStore;
import com.egs.javaagent.service.LangChain4jLanguageModelGateway;
import com.egs.javaagent.service.ModelProviderSettingsLoader;
import com.egs.javaagent.service.StubLanguageModelGateway;

public final class JavaAgentApplication {

    private JavaAgentApplication() {
    }

    public static void main(String[] args) throws Exception {
        var memoryStore = new FileMemoryStore("data/memory-log.jsonl");
        var providerSettings = ModelProviderSettingsLoader.loadFromEnvironment();
        var gateway = createGateway(providerSettings);
        var orchestrator = new AgentOrchestrator(memoryStore, gateway, providerSettings);
        var server = new AgentHttpServer(8765, orchestrator);

        server.start();
        System.out.println("EGS Java Agent server started on http://localhost:8765");
        System.out.printf(
            "Provider=%s, Model=%s, Gateway=%s, ApiKeyPresent=%s%n",
            providerSettings.providerName(),
            providerSettings.modelName(),
            providerSettings.gatewayKind(),
            providerSettings.apiKeyPresent()
        );
    }

    private static com.egs.javaagent.service.LanguageModelGateway createGateway(
        com.egs.javaagent.service.ModelProviderSettings providerSettings
    ) {
        if (!providerSettings.apiKeyPresent()) {
            return new StubLanguageModelGateway(providerSettings);
        }

        if (!"langchain4j".equalsIgnoreCase(providerSettings.gatewayKind())) {
            throw new IllegalStateException(
                "This build is configured for langchain4j-only execution. " +
                "Set EGS_AGENT_GATEWAY=langchain4j before starting the service."
            );
        }

        return new LangChain4jLanguageModelGateway(providerSettings);
    }
}
