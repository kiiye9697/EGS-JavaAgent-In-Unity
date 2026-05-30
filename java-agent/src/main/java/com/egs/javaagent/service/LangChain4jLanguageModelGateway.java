package com.egs.javaagent.service;

import com.egs.javaagent.model.AgentSuggestedAction;
import com.egs.javaagent.model.AgentRequest;
import com.egs.javaagent.model.ConversationMemoryEntry;
import dev.langchain4j.agent.tool.Tool;
import dev.langchain4j.memory.chat.MessageWindowChatMemory;
import dev.langchain4j.model.chat.ChatLanguageModel;
import dev.langchain4j.model.openai.OpenAiChatModel;
import dev.langchain4j.service.AiServices;
import dev.langchain4j.service.MemoryId;
import dev.langchain4j.service.SystemMessage;
import dev.langchain4j.service.UserMessage;
import dev.langchain4j.service.V;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.ArrayList;
import java.util.List;

public final class LangChain4jLanguageModelGateway implements LanguageModelGateway {

    private static final int MAX_TOOL_READ_CHARS = 3200;
    private static final int MAX_TOOL_RANGE_LINES = 200;
    private final ModelProviderSettings providerSettings;
    private final ChatLanguageModel chatModel;
    private final LangChainAgent assistant;
    private final ThreadLocal<RequestContext> currentContext = new ThreadLocal<>();

    public LangChain4jLanguageModelGateway(ModelProviderSettings providerSettings) {
        this.providerSettings = providerSettings;
        this.chatModel = OpenAiChatModel.builder()
            .apiKey(providerSettings.apiKey())
            .baseUrl(normalizeBaseUrl(providerSettings.apiBaseUrl()))
            .modelName(resolveEffectiveModelName(providerSettings))
            .timeout(Duration.ofSeconds(60))
            .build();
        this.assistant = AiServices.builder(LangChainAgent.class)
            .chatLanguageModel(chatModel)
            .chatMemoryProvider(memoryId -> MessageWindowChatMemory.withMaxMessages(20))
            .tools(new ProjectTools())
            .build();
    }

    @Override
    public LanguageModelResult respond(
        AgentRequest request,
        String planSummary,
        List<ConversationMemoryEntry> memory,
        ProjectContextSnapshot projectSnapshot
    ) {
        RequestContext requestContext = new RequestContext(request, projectSnapshot, memory, new ArrayList<>(), new ArrayList<>());
        currentContext.set(requestContext);
        try {
            String assistantMessage = assistant.respond(
                memoryIdFor(request),
                request.userMessage(),
                buildRequestSummary(request, planSummary, projectSnapshot, memory)
            );
            return LanguageModelResult.of(
                assistantMessage,
                List.copyOf(requestContext.toolTraces()),
                List.copyOf(requestContext.suggestedActions())
            );
        } finally {
            currentContext.remove();
        }
    }

    private String memoryIdFor(AgentRequest request) {
        String projectPath = request.projectPath() == null || request.projectPath().isBlank()
            ? "no-project"
            : request.projectPath();
        String scene = request.activeSceneName() == null || request.activeSceneName().isBlank()
            ? "no-scene"
            : request.activeSceneName();
        return projectPath + "::" + scene;
    }

    private String buildRequestSummary(
        AgentRequest request,
        String planSummary,
        ProjectContextSnapshot projectSnapshot,
        List<ConversationMemoryEntry> memory
    ) {
        String discoveredFiles = projectSnapshot.discoveredFiles().isEmpty()
            ? "none"
            : String.join("\n- ", projectSnapshot.discoveredFiles());

        String selectedSnippetOverview = projectSnapshot.snippets().isEmpty()
            ? "none"
            : String.join(
                "\n",
                projectSnapshot.snippets().stream()
                    .map(snippet -> "- %s (lines=%d, truncated=%s)".formatted(
                        snippet.relativePath(),
                        snippet.lineCount(),
                        snippet.truncated()
                    ))
                    .toList()
            );

        String recentMemory = memory.isEmpty()
            ? "none"
            : String.join(
                "\n",
                memory.stream()
                    .map(entry -> "- User: %s".formatted(entry.userMessage()))
                    .toList()
            );

        String references = projectSnapshot.references().isEmpty()
            ? "none"
            : String.join(
                "\n\n",
                projectSnapshot.references().stream()
                    .map(reference -> "- Source: %s\n  Truncated: %s\n%s".formatted(
                        reference.source(),
                        reference.truncated(),
                        reference.content()
                    ))
                    .toList()
            );

        return """
            Mode: %s
            Unity version: %s
            Active scene: %s
            Scene context: %s
            Selected assets: %s
            Selected objects: %s
            Project snapshot: %s
            Compile state: %s
            Compiler messages:
            %s

            Plan summary:
            %s

            Discovered files:
            - %s

            Selected snippet overview:
            %s

            Recent memory:
            %s

            Reference material:
            %s
            """.formatted(
            request.mode(),
            request.unityVersion(),
            request.activeSceneName(),
            request.sceneContext(),
            request.selectedAssets(),
            request.selectedObjects(),
            request.projectSnapshot(),
            request.compileState(),
            request.compilerMessages().isEmpty() ? "none" : String.join("\n", request.compilerMessages()),
            planSummary,
            discoveredFiles,
            selectedSnippetOverview,
            recentMemory,
            references
        );
    }

    private String normalizeBaseUrl(String baseUrl) {
        if (baseUrl == null || baseUrl.isBlank()) {
            return "https://api.openai.com/v1";
        }

        String normalized = baseUrl.endsWith("/") ? baseUrl.substring(0, baseUrl.length() - 1) : baseUrl;
        return normalized.endsWith("/v1") ? normalized : normalized + "/v1";
    }

    private String resolveEffectiveModelName(ModelProviderSettings providerSettings) {
        if ("deepseek".equalsIgnoreCase(providerSettings.providerName())
            && "deepseek-v4-flash".equalsIgnoreCase(providerSettings.modelName())) {
            // DeepSeek's non-thinking alias avoids reasoning_content replay requirements that
            // LangChain4j beta3 cannot fully propagate during tool-calling turns.
            return "deepseek-chat";
        }

        return providerSettings.modelName();
    }

    @SystemMessage("""
        You are the backend agent for a Unity + Java harness.
        Use the available tools whenever you need stronger evidence from project files.
        Prefer concrete evidence over guesses.
        Never claim a file was inspected unless you actually used a tool or it was explicitly listed in the request summary.
        If mode is agent and the task likely requires editing files, inspect relevant files first, then use suggest_create_file or suggest_replace_file to register a reviewable proposal in the same turn whenever enough evidence exists.
        In agent mode, do not stop after presenting only a plan if you already have enough evidence to produce a safe draft.
        If the user gave a concrete target path, prefer producing a proposal for that target rather than answering abstractly.
        If reference material was attached, read it before proposing a shader, script, or repair implementation.
        Never write that a file was changed. You can only propose changes for later approval.
        """)
    interface LangChainAgent {

        @UserMessage("""
            User request:
            {{userMessage}}

            Request summary:
            {{requestSummary}}

            Use tools when the request needs more file evidence than the summary already provides.
            """)
        String respond(@MemoryId String memoryId, @V("userMessage") String userMessage, @V("requestSummary") String requestSummary);
    }

    final class ProjectTools {

        private final ProjectPathResolver pathResolver = new ProjectPathResolver();

        @Tool("Read a UTF-8 text file from the Unity project. Use this when you need exact file contents.")
        String read_file(String target) {
            RequestContext context = requireContext();
            Path projectPath = pathResolver.safeProjectPath(context.request().projectPath());
            if (projectPath == null) {
                return "Project path is unavailable.";
            }

            Path filePath = pathResolver.resolveTarget(projectPath, target);
            if (filePath == null || !Files.exists(filePath) || !Files.isRegularFile(filePath)) {
                return "Target file does not exist.";
            }

            try {
                String content = Files.readString(filePath, StandardCharsets.UTF_8);
                String preview = content.length() > MAX_TOOL_READ_CHARS
                    ? content.substring(0, MAX_TOOL_READ_CHARS) + "\n...[truncated]"
                    : content;
                context.toolTraces().add(new ModelToolTrace(
                    "read_file",
                    projectPath.relativize(filePath).toString().replace('\\', '/'),
                    "Loaded UTF-8 file preview for direct evidence."
                ));
                return "File: %s\n%s".formatted(projectPath.relativize(filePath).toString().replace('\\', '/'), preview);
            } catch (IOException exception) {
                return exception.getMessage();
            }
        }

        @Tool("Read a specific line range from a UTF-8 text file in the Unity project. Use this for long files.")
        String read_file_range(String target, int startLine, int endLine) {
            RequestContext context = requireContext();
            Path projectPath = pathResolver.safeProjectPath(context.request().projectPath());
            if (projectPath == null) {
                return "Project path is unavailable.";
            }

            Path filePath = pathResolver.resolveTarget(projectPath, target);
            if (filePath == null || !Files.exists(filePath) || !Files.isRegularFile(filePath)) {
                return "Target file does not exist.";
            }

            try {
                List<String> lines = Files.readAllLines(filePath, StandardCharsets.UTF_8);
                if (lines.isEmpty()) {
                    return "[empty file]";
                }

                int safeStart = Math.max(1, startLine);
                int safeEnd = Math.max(safeStart, endLine);
                if (safeEnd - safeStart + 1 > MAX_TOOL_RANGE_LINES) {
                    safeEnd = safeStart + MAX_TOOL_RANGE_LINES - 1;
                }
                safeEnd = Math.min(safeEnd, lines.size());

                StringBuilder builder = new StringBuilder();
                builder.append("File: ")
                    .append(projectPath.relativize(filePath).toString().replace('\\', '/'))
                    .append('\n')
                    .append("Lines ")
                    .append(safeStart)
                    .append('-')
                    .append(safeEnd)
                    .append(" of ")
                    .append(lines.size())
                    .append('\n');

                for (int index = safeStart - 1; index < safeEnd; index++) {
                    builder.append(index + 1).append(": ").append(lines.get(index)).append('\n');
                }

                context.toolTraces().add(new ModelToolTrace(
                    "read_file_range",
                    projectPath.relativize(filePath).toString().replace('\\', '/'),
                    "Loaded numbered lines %d-%d.".formatted(safeStart, safeEnd)
                ));
                return builder.toString().trim();
            } catch (IOException exception) {
                return exception.getMessage();
            }
        }

        @Tool("List files and directories from the Unity project. Use this to discover nearby files.")
        String list_directory(String target) {
            RequestContext context = requireContext();
            Path projectPath = pathResolver.safeProjectPath(context.request().projectPath());
            if (projectPath == null) {
                return "Project path is unavailable.";
            }

            Path directory = target == null || target.isBlank() || target.equals(context.request().projectPath())
                ? projectPath
                : pathResolver.resolveTarget(projectPath, target);
            if (directory == null || !Files.exists(directory) || !Files.isDirectory(directory)) {
                return "Target directory does not exist.";
            }

            try (var stream = Files.walk(directory, 2)) {
                String output = stream
                    .filter(path -> !path.equals(directory))
                    .map(path -> {
                        String relative = directory.relativize(path).toString().replace('\\', '/');
                        String kind = Files.isDirectory(path) ? "[dir]" : "[file]";
                        return kind + " " + relative;
                    })
                    .sorted()
                    .limit(40)
                    .reduce((left, right) -> left + "\n" + right)
                    .orElse("[empty directory]");
                context.toolTraces().add(new ModelToolTrace(
                    "list_directory",
                    directory.toString(),
                    "Listed nearby files and folders for discovery."
                ));
                return output;
            } catch (IOException exception) {
                return exception.getMessage();
            }
        }

        @Tool("Return a quick overview of files and selected snippets already discovered in the current Unity request.")
        String project_overview() {
            RequestContext context = requireContext();
            String files = context.projectSnapshot().discoveredFiles().isEmpty()
                ? "none"
                : String.join("\n- ", context.projectSnapshot().discoveredFiles());
            String snippets = context.projectSnapshot().snippets().isEmpty()
                ? "none"
                : String.join(
                    "\n",
                    context.projectSnapshot().snippets().stream()
                        .map(snippet -> "- %s (lines=%d, truncated=%s)".formatted(
                            snippet.relativePath(),
                            snippet.lineCount(),
                            snippet.truncated()
                        ))
                        .toList()
                );
            context.toolTraces().add(new ModelToolTrace(
                "project_overview",
                context.request().projectPath(),
                "Read the pre-scanned overview of discovered files and selected snippets."
            ));
            return "Discovered files:\n- %s\n\nSelected snippets:\n%s".formatted(files, snippets);
        }

        @Tool("Return the loaded reference documents or URLs attached to this Unity request. Use this before implementing shaders, functions, or repairs based on external instructions.")
        String read_reference_material() {
            RequestContext context = requireContext();
            if (context.projectSnapshot().references().isEmpty()) {
                return "No reference material was attached to this request.";
            }

            context.toolTraces().add(new ModelToolTrace(
                "read_reference_material",
                context.request().projectPath(),
                "Loaded attached reference document or URL content."
            ));

            return context.projectSnapshot().references().stream()
                .map(reference -> "Source: %s\nTruncated: %s\n%s".formatted(
                    reference.source(),
                    reference.truncated(),
                    reference.content()
                ))
                .reduce((left, right) -> left + "\n\n---\n\n" + right)
                .orElse("No reference material was attached to this request.");
        }

        @Tool("Find shader-related Unity assets such as .shader, .cginc, .hlsl, .mat, or shader graph files in the current project.")
        String list_shader_assets(String root) {
            return listFilteredAssets(root, new String[]{".shader", ".cginc", ".hlsl", ".mat", ".shadergraph"});
        }

        @Tool("Find function or script-related Unity assets such as MonoBehaviour, utility, or asmdef files in the current project.")
        String list_function_assets(String root) {
            return listFilteredAssets(root, new String[]{".cs", ".asmdef"});
        }

        @Tool("Return validation context including compile state, compiler messages, and whether a script can likely be attached after compilation.")
        String validation_overview() {
            RequestContext context = requireContext();
            context.toolTraces().add(new ModelToolTrace(
                "validation_overview",
                context.request().projectPath(),
                "Loaded compile and validation context from the Unity request."
            ));
            String compilerMessages = context.request().compilerMessages().isEmpty()
                ? "none"
                : String.join("\n", context.request().compilerMessages());
            return """
                Compile state: %s
                Compiler messages:
                %s
                Active scene: %s
                Selected assets: %s
                Selected objects: %s
                """.formatted(
                context.request().compileState(),
                compilerMessages,
                context.request().activeSceneName(),
                context.request().selectedAssets(),
                context.request().selectedObjects()
            );
        }

        @Tool("Return current scene and selection context for scene-aware shader, material, or function operations.")
        String scene_selection_overview() {
            RequestContext context = requireContext();
            context.toolTraces().add(new ModelToolTrace(
                "scene_selection_overview",
                context.request().activeSceneName(),
                "Loaded current Unity scene and selection context."
            ));
            return """
                Active scene: %s
                Scene context: %s
                Selected assets: %s
                Selected objects: %s
                """.formatted(
                context.request().activeSceneName(),
                context.request().sceneContext(),
                context.request().selectedAssets(),
                context.request().selectedObjects()
            );
        }

        @Tool("Return a formal capability catalog for the current Unity agent grouped by Shader, Material, Function, Validation, Scene, and Project.")
        String capability_catalog() {
            RequestContext context = requireContext();
            context.toolTraces().add(new ModelToolTrace(
                "capability_catalog",
                context.request().projectPath(),
                "Loaded formal Unity capability catalog."
            ));
            return """
                Shader:
                - read shader, hlsl, cginc files
                - list shader assets
                - propose create/replace shader files

                Material:
                - inspect .mat files as project assets
                - list nearby material assets
                - propose edits that align materials to shader expectations

                Function:
                - read MonoBehaviour or utility scripts
                - list function/script assets
                - propose create/replace C# files

                Validation:
                - inspect compile state
                - inspect compiler messages
                - repair from current compile diagnostics

                Scene:
                - inspect active scene name
                - inspect selected assets and objects
                - generate scene-aware proposals

                Project:
                - list directories
                - read file ranges
                - inspect request-scoped reference documents or URLs
                """;
        }

        private String listFilteredAssets(String root, String[] extensions) {
            RequestContext context = requireContext();
            Path projectPath = pathResolver.safeProjectPath(context.request().projectPath());
            if (projectPath == null) {
                return "Project path is unavailable.";
            }

            Path directory = root == null || root.isBlank() || root.equals(context.request().projectPath()) || root.equals("Assets")
                ? projectPath
                : pathResolver.resolveTarget(projectPath, root);
            if (directory == null || !Files.exists(directory) || !Files.isDirectory(directory)) {
                return "Target directory does not exist.";
            }

            try (var stream = Files.walk(directory, 5)) {
                String output = stream
                    .filter(Files::isRegularFile)
                    .filter(path -> hasAnyExtension(path, extensions))
                    .map(path -> projectPath.relativize(path).toString().replace('\\', '/'))
                    .sorted()
                    .limit(60)
                    .reduce((left, right) -> left + "\n" + right)
                    .orElse("[no matching assets]");

                context.toolTraces().add(new ModelToolTrace(
                    "list_filtered_assets",
                    directory.toString(),
                    "Listed domain-specific Unity assets for shader or function work."
                ));
                return output;
            } catch (IOException exception) {
                return exception.getMessage();
            }
        }

        private boolean hasAnyExtension(Path path, String[] extensions) {
            String normalized = path.getFileName().toString().toLowerCase();
            for (String extension : extensions) {
                if (normalized.endsWith(extension)) {
                    return true;
                }
            }
            return false;
        }

        @Tool("Suggest creating a new UTF-8 text file in the Unity project. Provide target path, reason, and full file content.")
        String suggest_create_file(String target, String reason, String content) {
            RequestContext context = requireContext();
            Path projectPath = pathResolver.safeProjectPath(context.request().projectPath());
            Path targetPath = projectPath == null ? null : pathResolver.resolveWritableTarget(projectPath, target);
            if (targetPath == null) {
                return "Target path is not writable inside the Unity project.";
            }

            context.suggestedActions().add(new AgentSuggestedAction(
                "suggest_create_file",
                normalizeTarget(projectPath, targetPath),
                reason,
                buildProposalPreview("create new file", normalizeTarget(projectPath, targetPath), reason, content),
                true
            ));
            return "Registered create-file proposal for review.";
        }

        @Tool("Suggest replacing the full contents of an existing UTF-8 text file in the Unity project. Provide target path, reason, and full replacement content.")
        String suggest_replace_file(String target, String reason, String content) {
            RequestContext context = requireContext();
            Path projectPath = pathResolver.safeProjectPath(context.request().projectPath());
            Path targetPath = projectPath == null ? null : pathResolver.resolveWritableTarget(projectPath, target);
            if (targetPath == null) {
                return "Target path is not writable inside the Unity project.";
            }

            context.suggestedActions().add(new AgentSuggestedAction(
                "suggest_replace_file",
                normalizeTarget(projectPath, targetPath),
                reason,
                buildProposalPreview("replace existing file", normalizeTarget(projectPath, targetPath), reason, content),
                true
            ));
            return "Registered replacement proposal for review.";
        }

        private RequestContext requireContext() {
            RequestContext context = currentContext.get();
            if (context == null) {
                throw new IllegalStateException("LangChain tool context is unavailable.");
            }
            return context;
        }

        private String normalizeTarget(Path projectPath, Path targetPath) {
            if (projectPath == null || targetPath == null) {
                return "";
            }

            return projectPath.relativize(targetPath).toString().replace('\\', '/');
        }

        private String buildProposalPreview(String proposalType, String target, String reason, String content) {
            String languageHint = target.endsWith(".cs")
                ? "csharp"
                : target.endsWith(".shader")
                    ? "shaderlab"
                    : "text";
            return """
                Approval required: yes
                Proposal type: %s
                Target: %s
                Reason: %s

                Content:
                ```%s
                %s
                ```
                """.formatted(proposalType, target, reason, languageHint, content);
        }
    }

    private record RequestContext(
        AgentRequest request,
        ProjectContextSnapshot projectSnapshot,
        List<ConversationMemoryEntry> memory,
        List<ModelToolTrace> toolTraces,
        List<AgentSuggestedAction> suggestedActions
    ) {
    }
}
