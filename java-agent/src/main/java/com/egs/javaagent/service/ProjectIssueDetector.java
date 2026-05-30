package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentIssue;
import com.egs.javaagent.model.AgentRequest;

import java.util.ArrayList;
import java.util.List;
import java.util.regex.Pattern;

public final class ProjectIssueDetector {

    private static final Pattern CLIENT_PATTERN = Pattern.compile("\\bJavaAgentClient\\b");

    public List<AgentIssue> detect(AgentRequest request, ProjectContextSnapshot snapshot) {
        List<AgentIssue> issues = new ArrayList<>();

        boolean windowReferencesClient = snapshot.snippets().stream()
            .anyMatch(snippet -> snippet.relativePath().endsWith("JavaAgentWindow.cs")
                && CLIENT_PATTERN.matcher(snippet.contentPreview()).find());

        boolean clientExists = snapshot.allDiscoveredFiles().stream()
            .anyMatch(path -> path.endsWith("JavaAgentClient.cs"));

        if (windowReferencesClient && !clientExists) {
            issues.add(new AgentIssue(
                "warning",
                "missing_file_reference",
                "JavaAgentClient.cs",
                "JavaAgentWindow.cs references JavaAgentClient, but the scanned project files did not include a matching JavaAgentClient.cs file."
            ));
        }

        if (request.selectedAssets().isEmpty()) {
            issues.add(new AgentIssue(
                "info",
                "limited_selection",
                request.projectPath(),
                "No assets were selected, so the agent is working from broad project context instead of targeted file context."
            ));
        }

        if (request.compilerMessages() != null && !request.compilerMessages().isEmpty()) {
            request.compilerMessages().stream()
                .filter(message -> message != null && message.startsWith("ERROR"))
                .limit(5)
                .forEach(message -> issues.add(new AgentIssue(
                    "error",
                    "compiler_error",
                    request.projectPath(),
                    message
                )));
        }

        return issues;
    }
}
