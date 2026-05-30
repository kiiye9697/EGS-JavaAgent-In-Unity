package com.egs.javaagent.service;

public record ProjectFileSnippet(
    String relativePath,
    String contentPreview,
    int lineCount,
    boolean truncated
) {
}
