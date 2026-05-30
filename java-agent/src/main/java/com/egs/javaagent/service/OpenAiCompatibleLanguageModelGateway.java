package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentRequest;
import com.egs.javaagent.model.ConversationMemoryEntry;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

public final class OpenAiCompatibleLanguageModelGateway implements LanguageModelGateway {

    private final ModelProviderSettings providerSettings;
    private final HttpClient httpClient;
    private final ObjectMapper objectMapper;

    public OpenAiCompatibleLanguageModelGateway(ModelProviderSettings providerSettings) {
        this.providerSettings = providerSettings;
        this.httpClient = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(20))
            .build();
        this.objectMapper = new ObjectMapper();
    }

    @Override
    public LanguageModelResult respond(
        AgentRequest request,
        String planSummary,
        List<ConversationMemoryEntry> memory,
        ProjectContextSnapshot projectSnapshot
    ) {
        if (!providerSettings.apiKeyPresent()) {
            throw new IllegalStateException("No API key configured for provider " + providerSettings.providerName());
        }

        try {
            String systemPrompt = """
                You are the backend agent for a Unity + Java harness.
                Keep responses concise and practical.
                Use the provided Unity context, selected assets, selected objects, and plan summary.
                If key context is missing, say what should be inspected next.
                Prioritize concrete evidence from discovered files, loaded snippets, and read-only action results.
                Do not claim a file or type is missing unless the discovered project file list and executed tool results support that claim.
                If a snippet appears truncated or incomplete, explicitly say the evidence is incomplete instead of inferring a hard failure.
                """;

            List<Map<String, String>> messages = new ArrayList<>();
            messages.add(Map.of(
                "role", "system",
                "content", systemPrompt
            ));

            for (ConversationMemoryEntry entry : memory) {
                messages.add(Map.of(
                    "role", "assistant",
                    "content", "Past memory summary: " + entry.assistantSummary()
                ));
            }

            messages.add(Map.of(
                "role", "user",
                "content", buildUserMessage(request, planSummary, projectSnapshot)
            ));

            Map<String, Object> payload = Map.of(
                "model", providerSettings.modelName(),
                "messages", messages,
                "temperature", 0.2
            );

            String requestJson = objectMapper.writeValueAsString(payload);
            HttpRequest httpRequest = HttpRequest.newBuilder()
                .uri(URI.create(normalizeChatCompletionsUrl(providerSettings.apiBaseUrl())))
                .timeout(Duration.ofSeconds(60))
                .header("Content-Type", "application/json")
                .header("Authorization", "Bearer " + providerSettings.apiKey())
                .POST(HttpRequest.BodyPublishers.ofString(requestJson))
                .build();

            HttpResponse<String> response = httpClient.send(httpRequest, HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() < 200 || response.statusCode() >= 300) {
                throw new IllegalStateException("Provider HTTP " + response.statusCode() + ": " + response.body());
            }

            JsonNode root = objectMapper.readTree(response.body());
            JsonNode contentNode = root.path("choices").path(0).path("message").path("content");
            if (contentNode.isMissingNode() || contentNode.asText().isBlank()) {
                throw new IllegalStateException("Provider returned an empty assistant message.");
            }

            return LanguageModelResult.of(contentNode.asText());
        } catch (IOException | InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Failed to call provider " + providerSettings.providerName(), exception);
        }
    }

    private String buildUserMessage(
        AgentRequest request,
        String planSummary,
        ProjectContextSnapshot projectSnapshot
    ) {
        String discoveredFiles = projectSnapshot.discoveredFiles().isEmpty()
            ? "none"
            : String.join("\n- ", projectSnapshot.discoveredFiles());

        String snippets = projectSnapshot.snippets().isEmpty()
            ? "none"
            : String.join(
                "\n\n",
                projectSnapshot.snippets().stream()
                    .map(snippet -> "File: " + snippet.relativePath() + "\n" + snippet.contentPreview())
                    .toList()
            );

        String references = projectSnapshot.references().isEmpty()
            ? "none"
            : String.join(
                "\n\n",
                projectSnapshot.references().stream()
                    .map(reference -> "Source: " + reference.source() + "\n" + reference.content())
                    .toList()
            );

        return """
            User request:
            %s

            Unity version: %s
            Active scene: %s
            Scene context: %s
            Selected assets: %s
            Selected objects: %s
            Client project snapshot:
            %s
            Compile state:
            %s

            Compiler messages:
            %s

            Discovered project files:
            - %s

            Loaded file snippets:
            %s

            Reference material:
            %s

            Plan summary:
            %s
            """.formatted(
            request.userMessage(),
            request.unityVersion(),
            request.activeSceneName(),
            request.sceneContext(),
            request.selectedAssets(),
            request.selectedObjects(),
            request.projectSnapshot(),
            request.compileState(),
            request.compilerMessages().isEmpty() ? "none" : String.join("\n", request.compilerMessages()),
            discoveredFiles,
            snippets,
            references,
            planSummary
        );
    }

    private String normalizeChatCompletionsUrl(String baseUrl) {
        String normalized = baseUrl.endsWith("/") ? baseUrl.substring(0, baseUrl.length() - 1) : baseUrl;
        if (normalized.endsWith("/chat/completions")) {
            return normalized;
        }
        if (normalized.endsWith("/v1")) {
            return normalized + "/chat/completions";
        }
        return normalized + "/chat/completions";
    }
}
