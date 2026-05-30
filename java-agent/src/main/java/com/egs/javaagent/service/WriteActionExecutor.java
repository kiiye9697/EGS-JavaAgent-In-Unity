package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentActionExecutionResult;
import com.egs.javaagent.model.AgentApprovedAction;
import com.egs.javaagent.model.AgentRequest;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public final class WriteActionExecutor {

    private final ProjectPathResolver pathResolver = new ProjectPathResolver();

    public List<AgentActionExecutionResult> execute(
        AgentRequest request,
        List<AgentApprovedAction> approvedActions
    ) {
        if (approvedActions == null || approvedActions.isEmpty()) {
            return List.of();
        }

        List<AgentActionExecutionResult> results = new ArrayList<>();
        for (AgentApprovedAction action : approvedActions) {
            if (action == null) {
                continue;
            }

            switch (action.type()) {
                case "suggest_create_file" -> results.add(executeCreateFile(request, action));
                case "suggest_replace_file", "suggest_write_patch" -> results.add(executeReplaceFile(request, action));
                default -> results.add(new AgentActionExecutionResult(
                    "approved_action",
                    action.target(),
                    false,
                    "Unsupported approved action type: " + action.type()
                ));
            }
        }
        return results;
    }

    private AgentActionExecutionResult executeCreateFile(AgentRequest request, AgentApprovedAction action) {
        Path projectPath = pathResolver.safeProjectPath(request.projectPath());
        if (projectPath == null) {
            return new AgentActionExecutionResult("approved_create_file", action.target(), false, "Project path is unavailable.");
        }

        Path targetPath = pathResolver.resolveWritableTarget(projectPath, action.target());
        if (targetPath == null) {
            return new AgentActionExecutionResult("approved_create_file", action.target(), false, "Target path is not writable inside the Unity project.");
        }

        if (Files.exists(targetPath)) {
            return new AgentActionExecutionResult("approved_create_file", action.target(), false, "Target file already exists.");
        }

        String content = extractCodeBlock(action.proposalPreview());
        if (content == null) {
            return new AgentActionExecutionResult("approved_create_file", action.target(), false, "Proposal preview does not contain a fenced code block to write.");
        }

        try {
            Files.createDirectories(targetPath.getParent());
            Files.writeString(targetPath, content, StandardCharsets.UTF_8);
            return new AgentActionExecutionResult(
                "approved_create_file",
                action.target(),
                true,
                "Created file at " + targetPath
            );
        } catch (IOException exception) {
            return new AgentActionExecutionResult("approved_create_file", action.target(), false, exception.getMessage());
        }
    }

    private AgentActionExecutionResult executeReplaceFile(AgentRequest request, AgentApprovedAction action) {
        Path projectPath = pathResolver.safeProjectPath(request.projectPath());
        if (projectPath == null) {
            return new AgentActionExecutionResult("approved_replace_file", action.target(), false, "Project path is unavailable.");
        }

        Path targetPath = pathResolver.resolveWritableTarget(projectPath, action.target());
        if (targetPath == null) {
            return new AgentActionExecutionResult("approved_replace_file", action.target(), false, "Target path is not writable inside the Unity project.");
        }

        if (!Files.exists(targetPath) || !Files.isRegularFile(targetPath)) {
            return new AgentActionExecutionResult("approved_replace_file", action.target(), false, "Target file does not exist.");
        }

        String content = extractCodeBlock(action.proposalPreview());
        if (content == null) {
            return new AgentActionExecutionResult("approved_replace_file", action.target(), false, "Proposal preview does not contain a fenced code block to write.");
        }

        try {
            Files.writeString(targetPath, content, StandardCharsets.UTF_8);
            return new AgentActionExecutionResult(
                "approved_replace_file",
                action.target(),
                true,
                "Replaced file contents at " + targetPath
            );
        } catch (IOException exception) {
            return new AgentActionExecutionResult("approved_replace_file", action.target(), false, exception.getMessage());
        }
    }

    private String extractCodeBlock(String proposalPreview) {
        if (proposalPreview == null || proposalPreview.isBlank()) {
            return null;
        }

        int firstFence = proposalPreview.indexOf("```");
        if (firstFence < 0) {
            return null;
        }

        int lineBreak = proposalPreview.indexOf('\n', firstFence);
        if (lineBreak < 0) {
            return null;
        }

        int closingFence = proposalPreview.indexOf("```", lineBreak + 1);
        if (closingFence < 0) {
            return null;
        }

        String content = proposalPreview.substring(lineBreak + 1, closingFence);
        return content.endsWith("\r\n")
            ? content.substring(0, content.length() - 2)
            : content.endsWith("\n")
                ? content.substring(0, content.length() - 1)
                : content;
    }
}
