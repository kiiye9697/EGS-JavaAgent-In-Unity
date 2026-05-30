package com.egs.javaagent.service;

import java.util.List;

public record ProjectContextSnapshot(
    List<String> allDiscoveredFiles,
    List<String> discoveredFiles,
    List<ProjectFileSnippet> snippets,
    List<ReferenceDocument> references
) {
}
