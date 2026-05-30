package com.egs.javaagent.service;

import com.egs.javaagent.model.ConversationMemoryEntry;

import java.util.List;

public interface MemoryStore {

    void append(ConversationMemoryEntry entry);

    List<ConversationMemoryEntry> loadRecent(String projectPath, String sessionId, int limit);
}
