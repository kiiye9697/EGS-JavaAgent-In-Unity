package com.egs.javaagent.service;

public record ReferenceDocument(
    String source,
    String content,
    boolean truncated
) {
}
