package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentDiagnostics;
import com.egs.javaagent.model.AgentApprovedAction;
import com.egs.javaagent.model.AgentActionExecutionResult;
import com.egs.javaagent.model.AgentIssue;
import com.egs.javaagent.model.AgentEnvelope;
import com.egs.javaagent.model.AgentPlanStep;
import com.egs.javaagent.model.AgentRequest;
import com.egs.javaagent.model.AgentResponse;
import com.egs.javaagent.model.AgentSuggestedAction;
import com.egs.javaagent.model.AgentToolExecutionSummary;
import com.egs.javaagent.model.ConversationMemoryEntry;

import java.time.Instant;
import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public final class AgentOrchestrator {

    private static final int RANGE_READ_LINE_COUNT = 120;
    private static final Pattern CODE_BLOCK_PATTERN = Pattern.compile("```([A-Za-z0-9#+_-]*)\\s*(.*?)```", Pattern.DOTALL);
    private static final Pattern ASSET_PATH_PATTERN = Pattern.compile("(Assets/[A-Za-z0-9_ ./\\-]+\\.(cs|shader|txt|json|asmdef))");
    private static final Pattern CLASS_NAME_PATTERN = Pattern.compile("class\\s+([A-Za-z_][A-Za-z0-9_]*)");
    private final MemoryStore memoryStore;
    private final LanguageModelGateway languageModelGateway;
    private final ModelProviderSettings providerSettings;
    private final ProjectContextScanner projectContextScanner;
    private final ReadOnlyActionExecutor readOnlyActionExecutor;
    private final WriteActionExecutor writeActionExecutor;
    private final ProjectIssueDetector projectIssueDetector;

    public AgentOrchestrator(
        MemoryStore memoryStore,
        LanguageModelGateway languageModelGateway,
        ModelProviderSettings providerSettings
    ) {
        this.memoryStore = memoryStore;
        this.languageModelGateway = languageModelGateway;
        this.providerSettings = providerSettings;
        this.projectContextScanner = new ProjectContextScanner();
        this.readOnlyActionExecutor = new ReadOnlyActionExecutor();
        this.writeActionExecutor = new WriteActionExecutor();
        this.projectIssueDetector = new ProjectIssueDetector();
    }

    public AgentResponse handle(AgentEnvelope envelope) {
        validateEnvelope(envelope);
        AgentRequest request = toRequest(envelope);
        validateRequest(request);
        ProjectContextSnapshot projectSnapshot = projectContextScanner.scan(request);
        List<ConversationMemoryEntry> memory = memoryStore.loadRecent(request.projectPath(), envelope.getSessionId(), 5);
        String planSummary = buildPlanSummary(request, memory);
        List<AgentPlanStep> planSteps = buildPlanSteps(request, memory.size());
        boolean hasApprovedActions = request.approvedActions() != null && !request.approvedActions().isEmpty();
        LanguageModelResult languageModelResult = hasApprovedActions
            ? LanguageModelResult.of("Approved actions received. Executing reviewed file changes locally.")
            : languageModelGateway.respond(request, planSummary, memory, projectSnapshot);
        List<AgentSuggestedAction> suggestedActions = mergeSuggestedActions(
            buildSuggestedActions(request, projectSnapshot),
            mergeSuggestedActions(
                languageModelResult.suggestedActions(),
                inferSuggestedActionsFromAssistantMessage(
                    request,
                    projectSnapshot,
                    languageModelResult.assistantMessage()
                )
            )
        );
        List<AgentActionExecutionResult> readOnlyResults = readOnlyActionExecutor.execute(request, suggestedActions);
        List<AgentActionExecutionResult> writeResults = writeActionExecutor.execute(request, request.approvedActions());
        List<AgentActionExecutionResult> mergedActionExecutionResults = mergeModelToolTraces(
            mergeExecutionResults(readOnlyResults, writeResults),
            languageModelResult.toolTraces()
        );
        AgentToolExecutionSummary toolExecutionSummary = buildToolExecutionSummary(mergedActionExecutionResults);
        List<AgentIssue> detectedIssues = projectIssueDetector.detect(request, projectSnapshot);
        String assistantMessage = appendApprovedActionSummary(
            languageModelResult.assistantMessage(),
            writeResults
        );

        memoryStore.append(new ConversationMemoryEntry(
            envelope.getSessionId(),
            Instant.now().toString(),
            request.userMessage(),
            assistantMessage,
            List.of(request.mode(), envelope.getType())
        ));

        return new AgentResponse(
            true,
            "ok",
            planSummary,
            assistantMessage,
            planSteps,
            new AgentDiagnostics(
                memory.size(),
                request.mode(),
                envelope.getType(),
                request.activeSceneName(),
                request.selectedAssets().size(),
                request.selectedObjects().size(),
                providerSettings.providerName(),
                providerSettings.modelName(),
                providerSettings.gatewayKind(),
                providerSettings.effectiveModelName(),
                providerSettings.apiKeyPresent(),
                request.compileState(),
                request.compilerMessages().size()
            ),
            suggestedActions,
            mergedActionExecutionResults,
            toolExecutionSummary,
            detectedIssues
        );
    }

    private AgentRequest toRequest(AgentEnvelope envelope) {
        Map<String, Object> payload = envelope.getPayload();
        Map<String, Object> metadata = envelope.getMetadata();
        return new AgentRequest(
            stringValue(payload.get("userMessage")),
            stringValue(payload.getOrDefault("mode", "agent")),
            stringValue(payload.get("sceneContext")),
            stringValue(payload.get("projectPath")),
            stringValue(payload.get("activeSceneName")),
            listValue(payload.get("selectedAssets")),
            listValue(payload.get("selectedObjects")),
            stringValue(metadata.get("unityVersion")),
            stringValue(payload.get("projectSnapshot")),
            stringValue(payload.get("compileState")),
            listValue(payload.get("compilerMessages")),
            listValue(payload.get("referenceInputs")),
            approvedActionValue(payload.get("approvedActions"))
        );
    }

    private String buildPlanSummary(AgentRequest request, List<ConversationMemoryEntry> memory) {
        return """
            Phase 1: understand Unity-side request and project context
            Phase 2: inspect related files or scene data in scene "%s"
            Phase 3: propose or execute the smallest safe change
            Phase 4: record outcome for future repair and iteration
            Memory references: %d
            Reference documents: %d
            Current mode: %s
            Selected assets: %d
            Selected objects: %d
            Compile state: %s
            """.formatted(
            request.activeSceneName(),
            memory.size(),
            projectSnapshotReferenceCount(request),
            request.mode(),
            request.selectedAssets().size(),
            request.selectedObjects().size(),
            request.compileState()
        );
    }

    private List<AgentPlanStep> buildPlanSteps(AgentRequest request, int memoryHits) {
        List<AgentPlanStep> steps = new ArrayList<>();
        steps.add(new AgentPlanStep(
            "Understand request",
            "Parse the user goal and align it with mode " + request.mode() + ".",
            "completed"
        ));
        steps.add(new AgentPlanStep(
            "Inspect Unity context",
            "Review active scene, selected assets, selected objects and project path before code generation.",
            "completed"
        ));
        steps.add(new AgentPlanStep(
            "Prepare change strategy",
            "Use recalled memory entries (" + memoryHits + "), request type, and compile context to draft the next safe action or proposal.",
            "in_progress"
        ));
        return steps;
    }

    private List<AgentSuggestedAction> buildSuggestedActions(
        AgentRequest request,
        ProjectContextSnapshot projectSnapshot
    ) {
        if (request.approvedActions() != null && !request.approvedActions().isEmpty()) {
            return List.of();
        }

        List<AgentSuggestedAction> actions = new ArrayList<>();
        boolean langChainDriven = "langchain4j".equalsIgnoreCase(providerSettings.gatewayKind());
        boolean agentMode = "agent".equalsIgnoreCase(request.mode());

        if (!langChainDriven && !projectSnapshot.snippets().isEmpty()) {
            for (ProjectFileSnippet snippet : projectSnapshot.snippets()) {
                if (snippet.truncated() || snippet.lineCount() > RANGE_READ_LINE_COUNT) {
                    actions.add(new AgentSuggestedAction(
                        "read_file_range",
                        snippet.relativePath() + "#L1-L" + RANGE_READ_LINE_COUNT,
                        "Selected file is long, so load a numbered line range before proposing edits.",
                        "",
                        false
                    ));
                } else {
                    actions.add(new AgentSuggestedAction(
                        "read_file",
                        snippet.relativePath(),
                        "Selected file already looks relevant to the current request. Re-read or inspect it before proposing edits.",
                        "",
                        false
                    ));
                }
            }
        }

        if (!langChainDriven && !projectSnapshot.discoveredFiles().isEmpty()) {
            actions.add(new AgentSuggestedAction(
                "list_directory",
                request.projectPath(),
                "A broader directory scan can confirm whether related Unity scripts, shaders, or scene files already exist.",
                "",
                false
            ));
        }

        maybeAddWriteProposal(actions, request, projectSnapshot);
        maybeAddDomainReadSuggestions(actions, request, projectSnapshot);

        if (langChainDriven && actions.isEmpty() && !agentMode) {
            actions.add(new AgentSuggestedAction(
                "model_tool_driven",
                request.projectPath(),
                "LangChain4j is expected to decide which project tools to call based on the request.",
                "",
                false
            ));
        }

        if (actions.isEmpty() && agentMode && hasWriteIntent(request.userMessage())) {
            String inferredTarget = inferTargetPath(request, request.userMessage(), "");
            if (inferredTarget == null || inferredTarget.isBlank()) {
                inferredTarget = "Assets/Scripts/GeneratedBehaviour.cs";
            }

            actions.add(new AgentSuggestedAction(
                "suggest_create_file",
                inferredTarget,
                "Agent mode should bias toward a reviewable implementation draft when the request clearly asks for code or shader output.",
                buildCreateFileProposal(request, inferredTarget),
                true
            ));
        }

        if (actions.isEmpty()) {
            actions.add(new AgentSuggestedAction(
                "suggest_create_file",
                "Assets/Scripts/NewBehaviour.cs",
                "No relevant file context was found yet. If nothing suitable exists, create the smallest new script after inspection.",
                buildCreateFileProposal(request, "Assets/Scripts/NewBehaviour.cs"),
                true
            ));
        }

        return actions.size() > 6 ? actions.subList(0, 6) : actions;
    }

    private int projectSnapshotReferenceCount(AgentRequest request) {
        return request.referenceInputs() == null ? 0 : request.referenceInputs().size();
    }

    private AgentToolExecutionSummary buildToolExecutionSummary(List<AgentActionExecutionResult> results) {
        int attempted = results.size();
        int successful = (int) results.stream().filter(AgentActionExecutionResult::success).count();
        int failed = attempted - successful;
        String summary = "Attempted %d action(s); %d succeeded, %d failed.".formatted(attempted, successful, failed);
        return new AgentToolExecutionSummary(attempted, successful, failed, summary);
    }

    private List<AgentActionExecutionResult> mergeModelToolTraces(
        List<AgentActionExecutionResult> readOnlyResults,
        List<ModelToolTrace> toolTraces
    ) {
        if (toolTraces == null || toolTraces.isEmpty()) {
            return readOnlyResults;
        }

        List<AgentActionExecutionResult> merged = new ArrayList<>(readOnlyResults);
        for (ModelToolTrace toolTrace : toolTraces) {
            merged.add(new AgentActionExecutionResult(
                "model_tool:" + toolTrace.toolName(),
                toolTrace.target(),
                true,
                toolTrace.summary()
            ));
        }
        return merged;
    }

    private List<AgentActionExecutionResult> mergeExecutionResults(
        List<AgentActionExecutionResult> first,
        List<AgentActionExecutionResult> second
    ) {
        List<AgentActionExecutionResult> merged = new ArrayList<>(first);
        merged.addAll(second);
        return merged;
    }

    private List<AgentSuggestedAction> mergeSuggestedActions(
        List<AgentSuggestedAction> plannedActions,
        List<AgentSuggestedAction> modelActions
    ) {
        LinkedHashMap<String, AgentSuggestedAction> merged = new LinkedHashMap<>();
        for (AgentSuggestedAction action : plannedActions) {
            if (action == null) {
                continue;
            }
            merged.put(action.type() + "::" + action.target(), action);
        }

        for (AgentSuggestedAction action : modelActions) {
            if (action == null) {
                continue;
            }
            merged.put(action.type() + "::" + action.target(), action);
        }

        List<AgentSuggestedAction> actions = new ArrayList<>(merged.values());
        boolean hasConcreteProposal = actions.stream().anyMatch(action ->
            "suggest_create_file".equalsIgnoreCase(action.type())
                || "suggest_replace_file".equalsIgnoreCase(action.type())
                || "suggest_write_patch".equalsIgnoreCase(action.type())
        );
        if (hasConcreteProposal) {
            actions.removeIf(action ->
                "model_tool_driven".equalsIgnoreCase(action.type())
                    || isGenericGeneratedProposal(action)
            );
        }
        return actions.size() > 6 ? actions.subList(0, 6) : actions;
    }

    private List<AgentSuggestedAction> inferSuggestedActionsFromAssistantMessage(
        AgentRequest request,
        ProjectContextSnapshot projectSnapshot,
        String assistantMessage
    ) {
        if (assistantMessage == null || assistantMessage.isBlank() || !hasWriteIntent(request.userMessage())) {
            return List.of();
        }

        Matcher codeBlockMatcher = CODE_BLOCK_PATTERN.matcher(assistantMessage);
        if (!codeBlockMatcher.find()) {
            return List.of();
        }

        String languageHint = codeBlockMatcher.group(1) == null ? "" : codeBlockMatcher.group(1).trim();
        String content = codeBlockMatcher.group(2) == null ? "" : codeBlockMatcher.group(2).trim();
        if (content.isBlank()) {
            return List.of();
        }

        String targetPath = inferTargetPath(request, content, languageHint);
        if (targetPath == null || targetPath.isBlank()) {
            return List.of();
        }

        boolean fileExists = targetExists(projectSnapshot, targetPath);
        String proposalType = fileExists ? "suggest_replace_file" : "suggest_create_file";
        String reason = fileExists
            ? "The model produced a concrete replacement draft for an existing Unity project file."
            : "The model produced a concrete new-file draft for the requested Unity script.";

        return List.of(new AgentSuggestedAction(
            proposalType,
            targetPath,
            reason,
            buildModelDerivedProposalPreview(proposalType, targetPath, reason, content, languageHint),
            true
        ));
    }

    private String inferTargetPath(AgentRequest request, String content, String languageHint) {
        Matcher assetPathMatcher = ASSET_PATH_PATTERN.matcher(request.userMessage());
        if (assetPathMatcher.find()) {
            return assetPathMatcher.group(1).trim();
        }

        Matcher contentAssetPathMatcher = ASSET_PATH_PATTERN.matcher(content);
        if (contentAssetPathMatcher.find()) {
            return contentAssetPathMatcher.group(1).trim();
        }

        if (content.contains("class ")) {
            Matcher classNameMatcher = CLASS_NAME_PATTERN.matcher(content);
            if (classNameMatcher.find()) {
                return "Assets/Scripts/" + classNameMatcher.group(1).trim() + ".cs";
            }
        }

        if ("shaderlab".equalsIgnoreCase(languageHint) || content.contains("Shader \"")) {
            return "Assets/Shaders/GeneratedShader.shader";
        }

        return "Assets/Scripts/GeneratedBehaviour.cs";
    }

    private boolean targetExists(ProjectContextSnapshot projectSnapshot, String targetPath) {
        String normalizedTarget = targetPath.replace('\\', '/');
        String relativeTarget = normalizedTarget.startsWith("Assets/")
            ? normalizedTarget.substring("Assets/".length())
            : normalizedTarget;

        return projectSnapshot.allDiscoveredFiles().stream().anyMatch(discovered ->
            discovered.equalsIgnoreCase(normalizedTarget)
                || discovered.equalsIgnoreCase(relativeTarget)
                || discovered.endsWith("/" + relativeTarget)
        );
    }

    private String buildModelDerivedProposalPreview(
        String proposalType,
        String targetPath,
        String reason,
        String content,
        String languageHint
    ) {
        String normalizedLanguageHint = languageHint == null || languageHint.isBlank()
            ? (targetPath.endsWith(".cs") ? "csharp" : "text")
            : languageHint;

        return """
            Approval required: yes
            Proposal type: %s
            Target: %s
            Reason: %s

            Content:
            ```%s
            %s
            ```
            """.formatted(proposalType, targetPath, reason, normalizedLanguageHint, content);
    }

    private boolean isGenericGeneratedProposal(AgentSuggestedAction action) {
        if (action == null) {
            return false;
        }

        return "suggest_create_file".equalsIgnoreCase(action.type())
            && "Assets/Scripts/GeneratedBehaviour.cs".equalsIgnoreCase(action.target());
    }

    private void validateEnvelope(AgentEnvelope envelope) {
        if (envelope == null) {
            throw new InvalidAgentRequestException("Envelope body is required.");
        }

        if (isBlank(envelope.getRequestId())) {
            throw new InvalidAgentRequestException("requestId is required.");
        }

        if (isBlank(envelope.getSessionId())) {
            throw new InvalidAgentRequestException("sessionId is required.");
        }

        if (envelope.getPayload() == null) {
            throw new InvalidAgentRequestException("payload is required.");
        }
    }

    private void validateRequest(AgentRequest request) {
        if (isBlank(request.userMessage())) {
            throw new InvalidAgentRequestException("payload.userMessage must not be empty.");
        }

        if (isBlank(request.mode())) {
            throw new InvalidAgentRequestException("payload.mode must not be empty.");
        }
    }

    private String stringValue(Object value) {
        return value == null ? "" : String.valueOf(value);
    }

    private List<String> listValue(Object value) {
        if (value instanceof List<?> list) {
            return list.stream().map(String::valueOf).toList();
        }

        return Collections.emptyList();
    }

    private List<AgentApprovedAction> approvedActionValue(Object value) {
        if (!(value instanceof List<?> list)) {
            return Collections.emptyList();
        }

        List<AgentApprovedAction> approvedActions = new ArrayList<>();
        for (Object item : list) {
            if (!(item instanceof Map<?, ?> action)) {
                continue;
            }

            approvedActions.add(new AgentApprovedAction(
                stringValue(action.get("type")),
                stringValue(action.get("target")),
                stringValue(action.get("reason")),
                stringValue(action.get("proposalPreview"))
            ));
        }

        return approvedActions;
    }

    private boolean isBlank(String value) {
        return value == null || value.isBlank();
    }

    private String appendApprovedActionSummary(
        String assistantMessage,
        List<AgentActionExecutionResult> writeResults
    ) {
        if (writeResults == null || writeResults.isEmpty()) {
            return assistantMessage;
        }

        StringBuilder builder = new StringBuilder(assistantMessage == null ? "" : assistantMessage.trim());
        builder.append("\n\nApproved action execution:\n");
        for (AgentActionExecutionResult result : writeResults) {
            builder.append("- [")
                .append(result.success() ? "ok" : "failed")
                .append("] ")
                .append(result.type())
                .append(" -> ")
                .append(result.target())
                .append(": ")
                .append(result.output())
                .append('\n');
        }
        return builder.toString().trim();
    }

    private void maybeAddWriteProposal(
        List<AgentSuggestedAction> actions,
        AgentRequest request,
        ProjectContextSnapshot projectSnapshot
    ) {
        if (!hasWriteIntent(request.userMessage())) {
            return;
        }

        if (!projectSnapshot.snippets().isEmpty()) {
            ProjectFileSnippet snippet = projectSnapshot.snippets().getFirst();
            actions.add(new AgentSuggestedAction(
                "suggest_replace_file",
                snippet.relativePath(),
                "This request looks like it may need a code change. Review this full-file replacement before enabling any write action.",
                buildReplaceFileProposal(request, snippet),
                true
            ));
            return;
        }

        String targetPath = "Assets/Scripts/GeneratedBehaviour.cs";
        actions.add(new AgentSuggestedAction(
            "suggest_create_file",
            targetPath,
            "This request looks like it may need a new file. Review this proposed file draft before enabling any write action.",
            buildCreateFileProposal(request, targetPath),
            true
        ));
    }

    private void maybeAddDomainReadSuggestions(
        List<AgentSuggestedAction> actions,
        AgentRequest request,
        ProjectContextSnapshot projectSnapshot
    ) {
        String message = request.userMessage() == null ? "" : request.userMessage().toLowerCase(Locale.ROOT);
        if (message.contains("shader") || message.contains("material") || message.contains("npr")) {
            actions.add(new AgentSuggestedAction(
                "group:shader",
                "Assets",
                "The request looks shader- or material-oriented. Inspect shader, material, cginc, or hlsl assets before proposing changes.",
                "",
                false
            ));
        }

        if (message.contains("function") || message.contains("script") || message.contains("monobehaviour")) {
            actions.add(new AgentSuggestedAction(
                "group:function",
                "Assets",
                "The request looks code-oriented. Inspect related MonoBehaviour or utility scripts before proposing changes.",
                "",
                false
            ));
        }

        if (projectSnapshot.references() != null && !projectSnapshot.references().isEmpty()) {
            actions.add(new AgentSuggestedAction(
                "group:reference",
                projectSnapshot.references().getFirst().source(),
                "Reference material was attached to this request. Read it before implementing or repairing the target.",
                "",
                false
            ));
        }
    }

    private boolean hasWriteIntent(String userMessage) {
        if (userMessage == null || userMessage.isBlank()) {
            return false;
        }

        String normalized = userMessage.toLowerCase(Locale.ROOT);
        return normalized.contains("create")
            || normalized.contains("implement")
            || normalized.contains("scaffold")
            || normalized.contains("generate")
            || normalized.contains("write")
            || normalized.contains("modify")
            || normalized.contains("update")
            || normalized.contains("fix")
            || normalized.contains("refactor")
            || normalized.contains("新增")
            || normalized.contains("创建")
            || normalized.contains("生成")
            || normalized.contains("修改")
            || normalized.contains("修复")
            || normalized.contains("重构");
    }

    private String buildReplaceFileProposal(AgentRequest request, ProjectFileSnippet snippet) {
        return """
            Approval required: yes
            Proposal type: replace existing file
            Target: %s
            Reason: Replace the current file only after review.

            User intent:
            %s

            Replacement content:
            ```csharp
            // TODO: replace this draft with a task-specific implementation after review.
            %s
            ```
            """.formatted(
            snippet.relativePath(),
            request.userMessage(),
            snippet.contentPreview()
        );
    }

    private String buildCreateFileProposal(AgentRequest request, String targetPath) {
        return """
            Approval required: yes
            Proposal type: create new file
            Target: %s

            User intent:
            %s

            Suggested file stub:
            ```csharp
            using UnityEngine;

            public sealed class GeneratedBehaviour : MonoBehaviour
            {
                private void Start()
                {
                    Debug.Log("GeneratedBehaviour created after approval.");
                }
            }
            ```
            """.formatted(targetPath, request.userMessage());
    }
}
