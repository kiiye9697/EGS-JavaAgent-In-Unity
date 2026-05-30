package com.egs.javaagent.service;

import com.egs.javaagent.model.ConversationMemoryEntry;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Collections;
import java.util.List;
import java.util.stream.Collectors;

public final class FileMemoryStore implements MemoryStore {

    private final Path filePath;
    private final ObjectMapper objectMapper;

    public FileMemoryStore(String filePath) {
        this.filePath = Path.of(filePath);
        this.objectMapper = new ObjectMapper().findAndRegisterModules();
    }

    @Override
    public synchronized void append(ConversationMemoryEntry entry) {
        try {
            Files.createDirectories(filePath.getParent());
            String line = objectMapper.writeValueAsString(entry) + System.lineSeparator();
            Files.writeString(
                filePath,
                line,
                Files.exists(filePath)
                    ? java.nio.file.StandardOpenOption.APPEND
                    : java.nio.file.StandardOpenOption.CREATE,
                java.nio.file.StandardOpenOption.WRITE
            );
        } catch (IOException exception) {
            throw new IllegalStateException("Failed to append memory entry", exception);
        }
    }

    @Override
    public synchronized List<ConversationMemoryEntry> loadRecent(String projectPath, String sessionId, int limit) {
        if (!Files.exists(filePath)) {
            return Collections.emptyList();
        }

        try {
            return Files.readAllLines(filePath).stream()
                .filter(line -> !line.isBlank())
                .map(this::deserialize)
                .filter(entry -> entry.sessionId().equals(sessionId))
                .skip(Math.max(0, countBySession(sessionId) - limit))
                .collect(Collectors.toList());
        } catch (IOException exception) {
            throw new IllegalStateException("Failed to load memory entries", exception);
        }
    }

    private long countBySession(String sessionId) throws IOException {
        return Files.readAllLines(filePath).stream()
            .filter(line -> !line.isBlank())
            .map(this::deserialize)
            .filter(entry -> entry.sessionId().equals(sessionId))
            .count();
    }

    private ConversationMemoryEntry deserialize(String line) {
        try {
            return objectMapper.readValue(line, ConversationMemoryEntry.class);
        } catch (IOException exception) {
            throw new IllegalStateException("Failed to deserialize memory entry", exception);
        }
    }
}
