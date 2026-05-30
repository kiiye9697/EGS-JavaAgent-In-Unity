package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentRequest;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;
import java.util.stream.Stream;

public final class ProjectContextScanner {

    private static final int MAX_DISCOVERED_FILES = 30;
    private static final int MAX_SCAN_DEPTH = 6;
    private static final int MAX_SNIPPETS = 5;
    private static final int MAX_SNIPPET_CHARS = 1200;
    private final ProjectPathResolver pathResolver = new ProjectPathResolver();
    private final ReferenceDocumentLoader referenceDocumentLoader = new ReferenceDocumentLoader();

    public ProjectContextSnapshot scan(AgentRequest request) {
        Path projectPath = pathResolver.safeProjectPath(request.projectPath());
        List<ReferenceDocument> references = referenceDocumentLoader.load(request.referenceInputs());
        if (projectPath == null) {
            return new ProjectContextSnapshot(List.of(), List.of(), List.of(), references);
        }

        List<String> allDiscoveredFiles = discoverInterestingFiles(projectPath);
        List<String> discoveredFiles = allDiscoveredFiles.stream()
            .limit(MAX_DISCOVERED_FILES)
            .toList();
        List<ProjectFileSnippet> snippets = buildSnippets(projectPath, request.selectedAssets());
        return new ProjectContextSnapshot(allDiscoveredFiles, discoveredFiles, snippets, references);
    }

    private List<String> discoverInterestingFiles(Path projectPath) {
        try (Stream<Path> stream = Files.walk(projectPath, MAX_SCAN_DEPTH)) {
            return stream
                .filter(Files::isRegularFile)
                .filter(this::isInterestingTextFile)
                .map(projectPath::relativize)
                .map(path -> path.toString().replace('\\', '/'))
                .sorted(Comparator.naturalOrder())
                .toList();
        } catch (IOException exception) {
            return List.of();
        }
    }

    private List<ProjectFileSnippet> buildSnippets(Path projectPath, List<String> selectedAssets) {
        List<ProjectFileSnippet> snippets = new ArrayList<>();
        for (String selectedAsset : selectedAssets) {
            if (snippets.size() >= MAX_SNIPPETS) {
                break;
            }

            Path candidate = pathResolver.resolveTarget(projectPath, selectedAsset);
            if (candidate == null || !Files.exists(candidate) || !Files.isRegularFile(candidate) || !isInterestingTextFile(candidate)) {
                continue;
            }

            try {
                String content = Files.readString(candidate, StandardCharsets.UTF_8);
                boolean truncated = content.length() > MAX_SNIPPET_CHARS;
                String preview = truncated
                    ? content.substring(0, MAX_SNIPPET_CHARS) + "\n...[truncated]"
                    : content;
                int lineCount = (int) content.lines().count();

                snippets.add(new ProjectFileSnippet(
                    projectPath.relativize(candidate).toString().replace('\\', '/'),
                    preview,
                    lineCount,
                    truncated
                ));
            } catch (IOException ignored) {
                // Best-effort only: skip unreadable files instead of failing the whole request.
            }
        }

        return snippets;
    }

    private boolean isInterestingTextFile(Path path) {
        String name = path.getFileName().toString().toLowerCase(Locale.ROOT);
        return name.endsWith(".cs")
            || name.endsWith(".shader")
            || name.endsWith(".cginc")
            || name.endsWith(".hlsl")
            || name.endsWith(".json")
            || name.endsWith(".txt")
            || name.endsWith(".md")
            || name.endsWith(".unity")
            || name.endsWith(".asmdef");
    }
}
