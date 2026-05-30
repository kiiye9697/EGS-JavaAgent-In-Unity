package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentActionExecutionResult;
import com.egs.javaagent.model.AgentRequest;
import com.egs.javaagent.model.AgentSuggestedAction;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;

public final class ReadOnlyActionExecutor {

    private static final int MAX_READ_CHARS = 1600;
    private static final int MAX_RANGE_LINES = 200;
    private static final int MAX_LIST_ITEMS = 40;
    private static final int MAX_LIST_DEPTH = 2;
    private final ProjectPathResolver pathResolver = new ProjectPathResolver();

    public List<AgentActionExecutionResult> execute(
        AgentRequest request,
        List<AgentSuggestedAction> actions
    ) {
        List<AgentActionExecutionResult> results = new ArrayList<>();
        for (AgentSuggestedAction action : actions) {
            switch (action.type()) {
                case "read_file" -> results.add(executeReadFile(request, action));
                case "read_file_range" -> results.add(executeReadFileRange(request, action));
                case "list_directory" -> results.add(executeListDirectory(request, action));
                default -> {
                    // Keep this executor strictly read-only.
                }
            }
        }
        return results;
    }

    private AgentActionExecutionResult executeReadFile(AgentRequest request, AgentSuggestedAction action) {
        Path projectPath = pathResolver.safeProjectPath(request.projectPath());
        if (projectPath == null) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Project path is unavailable.");
        }

        Path filePath = pathResolver.resolveTarget(projectPath, action.target());
        if (filePath == null || !Files.exists(filePath) || !Files.isRegularFile(filePath)) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Target file does not exist.");
        }

        try {
            String content = Files.readString(filePath, StandardCharsets.UTF_8);
            String preview = content.length() > MAX_READ_CHARS
                ? content.substring(0, MAX_READ_CHARS) + "\n...[truncated]"
                : content;
            return new AgentActionExecutionResult(action.type(), action.target(), true, preview);
        } catch (IOException exception) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, exception.getMessage());
        }
    }

    private AgentActionExecutionResult executeReadFileRange(AgentRequest request, AgentSuggestedAction action) {
        Path projectPath = pathResolver.safeProjectPath(request.projectPath());
        if (projectPath == null) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Project path is unavailable.");
        }

        ParsedRangeTarget parsedRangeTarget = parseRangeTarget(action.target());
        if (parsedRangeTarget == null) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Target range format is invalid. Expected path#Lstart-Lend.");
        }

        Path filePath = pathResolver.resolveTarget(projectPath, parsedRangeTarget.path());
        if (filePath == null || !Files.exists(filePath) || !Files.isRegularFile(filePath)) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Target file does not exist.");
        }

        try {
            List<String> lines = Files.readAllLines(filePath, StandardCharsets.UTF_8);
            if (lines.isEmpty()) {
                return new AgentActionExecutionResult(action.type(), action.target(), true, "[empty file]");
            }

            int startLine = Math.max(1, parsedRangeTarget.startLine());
            int endLine = Math.min(lines.size(), parsedRangeTarget.endLine());
            if (startLine > endLine) {
                return new AgentActionExecutionResult(action.type(), action.target(), false, "Requested line range is empty.");
            }

            List<String> numberedLines = new ArrayList<>();
            for (int index = startLine - 1; index < endLine; index++) {
                numberedLines.add((index + 1) + ": " + lines.get(index));
            }

            String output = "Lines %d-%d of %d\n%s".formatted(
                startLine,
                endLine,
                lines.size(),
                String.join("\n", numberedLines)
            );
            return new AgentActionExecutionResult(action.type(), action.target(), true, output);
        } catch (IOException exception) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, exception.getMessage());
        }
    }

    private AgentActionExecutionResult executeListDirectory(AgentRequest request, AgentSuggestedAction action) {
        Path projectPath = pathResolver.safeProjectPath(request.projectPath());
        if (projectPath == null) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Project path is unavailable.");
        }

        Path directory = action.target().equals(request.projectPath())
            ? projectPath
            : pathResolver.resolveTarget(projectPath, action.target());
        if (directory == null || !Files.exists(directory) || !Files.isDirectory(directory)) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, "Target directory does not exist.");
        }

        try (var stream = Files.walk(directory, MAX_LIST_DEPTH)) {
            List<String> entries = stream
                .filter(path -> !path.equals(directory))
                .map(path -> formatDirectoryEntry(directory, path))
                .sorted(Comparator.naturalOrder())
                .limit(MAX_LIST_ITEMS)
                .toList();

            return new AgentActionExecutionResult(
                action.type(),
                action.target(),
                true,
                String.join("\n", entries)
            );
        } catch (IOException exception) {
            return new AgentActionExecutionResult(action.type(), action.target(), false, exception.getMessage());
        }
    }

    private String formatDirectoryEntry(Path baseDirectory, Path candidate) {
        String relative = baseDirectory.relativize(candidate).toString().replace('\\', '/');
        String kind = Files.isDirectory(candidate) ? "[dir]" : "[file]";
        return kind + " " + relative;
    }

    private ParsedRangeTarget parseRangeTarget(String target) {
        if (target == null || target.isBlank()) {
            return null;
        }

        int separatorIndex = target.lastIndexOf("#L");
        if (separatorIndex <= 0 || separatorIndex >= target.length() - 2) {
            return null;
        }

        String path = target.substring(0, separatorIndex);
        String rangeText = target.substring(separatorIndex + 2);
        String[] parts = rangeText.split("-L");
        if (parts.length != 2) {
            return null;
        }

        try {
            int startLine = Integer.parseInt(parts[0]);
            int endLine = Integer.parseInt(parts[1]);
            if (startLine <= 0 || endLine < startLine) {
                return null;
            }

            if (endLine - startLine + 1 > MAX_RANGE_LINES) {
                endLine = startLine + MAX_RANGE_LINES - 1;
            }

            return new ParsedRangeTarget(path, startLine, endLine);
        } catch (NumberFormatException exception) {
            return null;
        }
    }

    private record ParsedRangeTarget(
        String path,
        int startLine,
        int endLine
    ) {
    }
}
