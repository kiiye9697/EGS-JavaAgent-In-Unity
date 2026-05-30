package com.egs.javaagent.http;

import com.egs.javaagent.model.AgentEnvelope;
import com.egs.javaagent.model.AgentResponse;
import com.egs.javaagent.service.AgentOrchestrator;
import com.egs.javaagent.service.InvalidAgentRequestException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;

public final class AgentHttpServer {

    private final HttpServer server;
    private final ObjectMapper objectMapper;
    private final AgentOrchestrator orchestrator;

    public AgentHttpServer(int port, AgentOrchestrator orchestrator) throws IOException {
        this.server = HttpServer.create(new InetSocketAddress(port), 0);
        this.objectMapper = new ObjectMapper();
        this.orchestrator = orchestrator;
        registerContexts();
    }

    public void start() {
        server.start();
    }

    private void registerContexts() {
        server.createContext("/health", this::handleHealth);
        server.createContext("/v1/agent/execute", this::handleExecute);
    }

    private void handleHealth(HttpExchange exchange) throws IOException {
        if (!"GET".equalsIgnoreCase(exchange.getRequestMethod())) {
            writeJson(exchange, 405, new AgentResponse(false, "Method not allowed", "", "", null, null, null, null, null, null));
            return;
        }

        writeJson(exchange, 200, new AgentResponse(true, "ok", "healthy", "No issues detected.", null, null, null, null, null, null));
    }

    private void handleExecute(HttpExchange exchange) throws IOException {
        if (!"POST".equalsIgnoreCase(exchange.getRequestMethod())) {
            writeJson(exchange, 405, new AgentResponse(false, "Method not allowed", "", "", null, null, null, null, null, null));
            return;
        }

        try (var inputStream = exchange.getRequestBody()) {
            AgentEnvelope envelope = objectMapper.readValue(inputStream, AgentEnvelope.class);
            AgentResponse response = orchestrator.handle(envelope);
            writeJson(exchange, 200, response);
        } catch (InvalidAgentRequestException exception) {
            logException("Invalid agent request", exception);
            writeJson(exchange, 400, new AgentResponse(
                false,
                "Invalid request",
                "",
                exception.getMessage(),
                null,
                null,
                null,
                null,
                null,
                null
            ));
        } catch (Exception exception) {
            logException("Agent execution failed", exception);
            String errorMessage = exception.getMessage() == null || exception.getMessage().isBlank()
                ? exception.getClass().getSimpleName()
                : exception.getClass().getSimpleName() + ": " + exception.getMessage();
            writeJson(exchange, 500, new AgentResponse(
                false,
                "Agent execution failed",
                "",
                errorMessage,
                null,
                null,
                null,
                null,
                null,
                null
            ));
        }
    }

    private void writeJson(HttpExchange exchange, int statusCode, Object body) throws IOException {
        byte[] bytes = objectMapper.writerWithDefaultPrettyPrinter()
            .writeValueAsString(body)
            .getBytes(StandardCharsets.UTF_8);

        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.sendResponseHeaders(statusCode, bytes.length);
        try (var outputStream = exchange.getResponseBody()) {
            outputStream.write(bytes);
        }
    }

    private void logException(String label, Exception exception) {
        System.err.println(label + ": " + exception.getMessage());
        exception.printStackTrace(System.err);
    }
}
