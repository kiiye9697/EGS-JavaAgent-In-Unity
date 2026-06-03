using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EGS.JavaAgent.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EGS.JavaAgent.Editor
{
    [InitializeOnLoad]
    internal static class JavaAgentSessionState
    {
        internal enum WorkflowNode
        {
            Skill,
            Reference,
            Inspect,
            Approve,
            Apply,
            Repair
        }

        private const int MaxHistoryItems = 12;
        private const int MaxLogItems = 40;

        private static readonly JavaAgentClient Client = new();
        private static readonly List<RequestHistoryItem> HistoryItems = new();
        private static readonly List<PendingApprovalItem> ApprovalQueue = new();
        private static readonly List<string> EventLog = new();
        private static readonly List<AppliedChangeRecord> AppliedChangeHistory = new();
        private static readonly List<PlanTaskNode> PlanTaskNodes = new();

        private static string _prompt = "Create a player controller scaffold and list the required Unity files.";
        private static string _referenceInputsText = string.Empty;
        private static string _response = "Idle. Open the workspace, describe a task, then send it to the Java agent.";
        private static bool _isBusy;
        private static AgentMode _mode = AgentMode.Agent;
        private static JavaAgentSkillProfile _skillProfile = JavaAgentSkillProfile.GeneralAgent;
        private static bool _agentHealthKnown;
        private static bool _agentHealthy;
        private static string _agentHealthStatus = "Unknown";
        private static WorkflowNode _selectedWorkflowNode = WorkflowNode.Skill;
        private static AgentDiagnostics _lastDiagnostics;
        private static AgentSuggestedAction[] _lastSuggestedActions = Array.Empty<AgentSuggestedAction>();
        private static AgentActionExecutionResult[] _lastActionExecutionResults = Array.Empty<AgentActionExecutionResult>();
        private static AgentToolExecutionSummary _lastToolExecutionSummary;
        private static AgentIssue[] _lastDetectedIssues = Array.Empty<AgentIssue>();
        private static string[] _lastRequestCompilerMessages = Array.Empty<string>();
        private static string _lastAppliedAssetPath = string.Empty;
        private static string _lastWorkflowStatus = "Idle";
        private static string _selectedPlanNodeId = string.Empty;
        private static string _activeRequestText = string.Empty;
        private static string _lastAutoRepairCompileStamp = string.Empty;
        private static int _autoRepairAttempts;
        private static bool _attachAfterCompile;
        private static bool _approvalExecutionInFlight;
        private static bool _workflowRunnerInFlight;
        private static bool _planNodeExecutionInFlight;

        static JavaAgentSessionState()
        {
            _mode = ParsePreferredMode(JavaAgentSettings.instance.preferredMode);
            CompileDiagnosticsTracker.SnapshotChanged += OnCompileSnapshotChanged;
            LogEvent("Session initialized.");
        }

        internal static event Action Changed;

        internal static string Prompt
        {
            get => _prompt;
            set
            {
                if (_prompt == value)
                {
                    return;
                }

                _prompt = value ?? string.Empty;
                NotifyChanged();
            }
        }

        internal static string Response => _response;
        internal static string ReferenceInputsText
        {
            get => _referenceInputsText;
            set
            {
                if (_referenceInputsText == value)
                {
                    return;
                }

                _referenceInputsText = value ?? string.Empty;
                NotifyChanged();
            }
        }
        internal static bool IsBusy => _isBusy;
        internal static AgentMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                NotifyChanged();
            }
        }
        internal static JavaAgentSkillProfile SkillProfile
        {
            get => _skillProfile;
            set
            {
                if (_skillProfile == value)
                {
                    return;
                }

                _skillProfile = value;
                NotifyChanged();
            }
        }
        internal static WorkflowNode SelectedWorkflowNode
        {
            get => _selectedWorkflowNode;
            set
            {
                if (_selectedWorkflowNode == value)
                {
                    return;
                }

                _selectedWorkflowNode = value;
                NotifyChanged();
            }
        }

        internal static string LastWorkflowStatus => _lastWorkflowStatus;
        internal static bool AgentHealthKnown => _agentHealthKnown;
        internal static bool AgentHealthy => _agentHealthy;
        internal static string AgentHealthStatus => _agentHealthStatus;
        internal static AgentDiagnostics LastDiagnostics => _lastDiagnostics;
        internal static AgentSuggestedAction[] LastSuggestedActions => _lastSuggestedActions;
        internal static AgentActionExecutionResult[] LastActionExecutionResults => _lastActionExecutionResults;
        internal static AgentToolExecutionSummary LastToolExecutionSummary => _lastToolExecutionSummary;
        internal static AgentIssue[] LastDetectedIssues => _lastDetectedIssues;
        internal static string[] LastRequestCompilerMessages => _lastRequestCompilerMessages;
        internal static string LastAppliedAssetPath => _lastAppliedAssetPath;
        internal static IReadOnlyList<RequestHistoryItem> History => HistoryItems;
        internal static IReadOnlyList<PendingApprovalItem> PendingApprovals => ApprovalQueue;
        internal static IReadOnlyList<string> Logs => EventLog;
        internal static IReadOnlyList<AppliedChangeRecord> AppliedChanges => AppliedChangeHistory;
        internal static IReadOnlyList<PlanTaskNode> PlanNodes => PlanTaskNodes;
        internal static IReadOnlyList<CodeMemoryEntry> CodeMemories => CodeMemoryStore.RecentEntries;
        internal static string SelectedPlanNodeId => _selectedPlanNodeId;
        internal static bool WorkflowRunnerInFlight => _workflowRunnerInFlight;
        internal static bool PlanNodeExecutionInFlight => _planNodeExecutionInFlight;
        internal static IReadOnlyList<UnityToolDescriptor> UnityTools => UnityEditorActionExecutor.ToolCatalog;
        internal static int AutoRepairAttempts => _autoRepairAttempts;
        internal static CompileDiagnosticsTracker.CompileSnapshot CurrentCompileSnapshot => CompileDiagnosticsTracker.GetSnapshot();

        internal static string BuildSelectionSummary()
        {
            var assetCount = Selection.assetGUIDs?.Length ?? 0;
            var objectCount = Selection.objects?.Length ?? 0;
            var sceneName = SceneManager.GetActiveScene().name;
            return $"Scene: {sceneName} | Assets: {assetCount} | Objects: {objectCount}";
        }

        internal static PlanTaskNode SelectedPlanNode
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_selectedPlanNodeId))
                {
                    return PlanTaskNodes.FirstOrDefault();
                }

                return PlanTaskNodes.FirstOrDefault(node => string.Equals(node.Id, _selectedPlanNodeId, StringComparison.Ordinal));
            }
        }

        internal static void SelectPlanNode(string nodeId)
        {
            if (string.Equals(_selectedPlanNodeId, nodeId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedPlanNodeId = nodeId ?? string.Empty;
            NotifyChanged();
        }

        internal static async Task ExecuteSelectedPlanNodeAsync()
        {
            var node = SelectedPlanNode;
            if (node == null || _isBusy)
            {
                return;
            }

            await ExecutePlanNodeAsync(node, applySafeApprovals: false);
        }

        internal static async Task RunAllPlanNodesAsync(bool applySafeApprovals)
        {
            if (_workflowRunnerInFlight || _isBusy)
            {
                return;
            }

            if (PlanTaskNodes.Count == 0)
            {
                await SendPromptAsync();
                return;
            }

            _workflowRunnerInFlight = true;
            try
            {
                foreach (var node in PlanTaskNodes.ToArray())
                {
                    if (node.IsTerminal)
                    {
                        continue;
                    }

                    SelectPlanNode(node.Id);
                    node.Status = "running";
                    NotifyChanged();

                    await ExecutePlanNodeAsync(node, applySafeApprovals: false);

                    if (applySafeApprovals)
                    {
                        await ApplyApprovalsForNodeAsync(node.Id, safeOnly: true);
                    }
                }

                _lastWorkflowStatus = "Plan workflow finished";
                LogEvent("Plan workflow runner finished.");
            }
            finally
            {
                _workflowRunnerInFlight = false;
                NotifyChanged();
            }
        }

        private static async Task ExecutePlanNodeAsync(PlanTaskNode node, bool applySafeApprovals)
        {
            if (node == null || _isBusy)
            {
                return;
            }

            _planNodeExecutionInFlight = true;
            try
            {
                await ExecuteUserMessageAsync(
                    BuildPlanNodePrompt(node),
                    Array.Empty<AgentApprovedAction>(),
                    $"Executing plan node {node.Title}",
                    "Waiting for node response",
                    Array.Empty<PendingFileSnapshot>()
                );

                if (applySafeApprovals)
                {
                    await ApplyApprovalsForNodeAsync(node.Id, safeOnly: true);
                }
            }
            finally
            {
                _planNodeExecutionInFlight = false;
                NotifyChanged();
            }
        }

        internal static async Task ApplySelectedNodeApprovalsAsync(bool safeOnly)
        {
            var node = SelectedPlanNode;
            if (node == null)
            {
                return;
            }

            await ApplyApprovalsForNodeAsync(node.Id, safeOnly);
        }

        internal static async Task ApplyAllApprovalsAsync(bool safeOnly)
        {
            await ApplyApprovalsInternalAsync(ApprovalQueue.ToArray(), safeOnly);
        }

        private static async Task ApplyApprovalsForNodeAsync(string nodeId, bool safeOnly)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var candidates = ApprovalQueue
                .Where(item => item != null && string.Equals(item.NodeId, nodeId, StringComparison.Ordinal))
                .ToArray();
            await ApplyApprovalsInternalAsync(candidates, safeOnly);
        }

        private static async Task ApplyApprovalsInternalAsync(PendingApprovalItem[] candidates, bool safeOnly)
        {
            if (candidates == null || candidates.Length == 0 || _approvalExecutionInFlight)
            {
                return;
            }

            var executable = candidates
                .Where(item => item != null && (!safeOnly || item.IsSafeCreateCandidate || item.IsUnityEditorAction))
                .ToArray();
            if (executable.Length == 0)
            {
                LogEvent("No matching approvals were safe to apply.");
                NotifyChanged();
                return;
            }

            var unityActions = executable.Where(item => item.IsUnityEditorAction).ToArray();
            foreach (var item in unityActions)
            {
                await ApplyApprovalAsync(item);
            }

            var fileActions = executable.Where(item => !item.IsUnityEditorAction).ToArray();
            if (fileActions.Length == 0)
            {
                return;
            }

            _approvalExecutionInFlight = true;
            try
            {
                var approvedActions = fileActions
                    .Select(item => new AgentApprovedAction
                    {
                        type = item.Action.type,
                        target = item.Action.target,
                        reason = item.Action.reason,
                        proposalPreview = item.Action.proposalPreview
                    })
                    .ToArray();
                var snapshots = SnapshotApprovedActions(approvedActions);
                for (int index = 0; index < snapshots.Length && index < fileActions.Length; index++)
                {
                    snapshots[index].NodeId = fileActions[index].NodeId;
                }

                await ExecuteUserMessageAsync(
                    $"Apply {approvedActions.Length} approved proposal(s) from the node workflow.",
                    approvedActions,
                    "Applying node workflow approvals",
                    "Waiting for apply response",
                    snapshots
                );

                foreach (var item in fileActions)
                {
                    ApprovalQueue.RemoveAll(entry => entry.Id == item.Id);
                }

                LogEvent($"Applied {approvedActions.Length} file approval(s).");
            }
            finally
            {
                _approvalExecutionInFlight = false;
                NotifyChanged();
            }
        }

        internal static void UseCodeMemoryAsPrompt(string memoryId, bool useAfterContent)
        {
            var memory = CodeMemoryStore.Find(memoryId);
            if (memory == null)
            {
                return;
            }

            string content = useAfterContent ? memory.afterContent : memory.beforeContent;
            Prompt =
                "Continue iterating from this stored code memory.\n\n"
                + "Target: " + memory.target + "\n"
                + "Original request: " + memory.request + "\n"
                + "Memory summary: " + memory.summary + "\n\n"
                + "Code:\n```csharp\n"
                + (content ?? string.Empty)
                + "\n```";
            _lastWorkflowStatus = "Loaded code memory into prompt";
            LogEvent("Loaded code memory for " + memory.target + " into prompt.");
            NotifyChanged();
        }

        internal static void DeleteCodeMemory(string memoryId)
        {
            CodeMemoryStore.Remove(memoryId);
            _lastWorkflowStatus = "Code memory removed";
            NotifyChanged();
        }

        internal static async Task SendPromptAsync()
        {
            await ExecuteUserMessageAsync(BuildEffectivePrompt(_prompt), Array.Empty<AgentApprovedAction>(), "Sending request to Java agent", "Waiting for agent response", Array.Empty<PendingFileSnapshot>());
        }

        internal static async Task RefreshAgentHealthAsync()
        {
            var healthy = await LocalJavaAgentController.IsHealthyAsync(JavaAgentSettings.instance.endpoint);
            _agentHealthKnown = true;
            _agentHealthy = healthy;
            _agentHealthStatus = healthy ? "Healthy" : "Unavailable";
            LogEvent("Agent health check: " + _agentHealthStatus + ".");
            NotifyChanged();
        }

        internal static async Task EnsureAgentRunningAsync()
        {
            await RefreshAgentHealthAsync();
            if (_agentHealthy)
            {
                return;
            }

            if (LocalJavaAgentController.Start(JavaAgentSettings.instance, out var message))
            {
                LogEvent(message);
                _agentHealthStatus = "Starting";
                NotifyChanged();
                await Task.Delay(3000);
                await RefreshAgentHealthAsync();
            }
            else
            {
                _agentHealthKnown = true;
                _agentHealthy = false;
                _agentHealthStatus = message;
                LogEvent(message);
                NotifyChanged();
            }
        }

        internal static async Task RestartAgentAsync()
        {
            var settings = JavaAgentSettings.instance;
            _agentHealthStatus = "Restarting";
            _lastWorkflowStatus = "Restarting local Java agent";
            LogEvent("Restarting local Java agent.");
            NotifyChanged();

            bool started = await LocalJavaAgentController.RestartAsync(settings);
            if (!started)
            {
                _agentHealthKnown = true;
                _agentHealthy = false;
                _agentHealthStatus = "Restart failed";
                _lastWorkflowStatus = "Restart failed";
                LogEvent("Restart failed.");
                NotifyChanged();
                return;
            }

            await Task.Delay(3000);
            await RefreshAgentHealthAsync();
        }

        internal static async Task RepairFromCompilerErrorsAsync(bool triggeredAutomatically)
        {
            var compileSnapshot = CompileDiagnosticsTracker.GetSnapshot();
            var compilerLines = compileSnapshot.messages
                .Select(message => message.ToSummaryLine())
                .Where(line => line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (compilerLines.Length == 0)
            {
                _response = "No compiler errors are currently tracked. Trigger a compile first, then retry repair.";
                _lastWorkflowStatus = "Repair skipped";
                LogEvent("Repair skipped because no compile errors were available.");
                NotifyChanged();
                return;
            }

            var approvedTargets = _lastActionExecutionResults
                .Where(result => result != null
                    && result.success
                    && result.type != null
                    && result.type.StartsWith("approved_", StringComparison.OrdinalIgnoreCase))
                .Select(result => result.target)
                .Where(target => !string.IsNullOrWhiteSpace(target))
                .Distinct()
                .ToArray();

            var repairPrompt = BuildRepairPrompt(compilerLines, approvedTargets);
            if (!triggeredAutomatically)
            {
                Prompt = repairPrompt;
            }

            if (triggeredAutomatically)
            {
                _autoRepairAttempts++;
                _lastAutoRepairCompileStamp = compileSnapshot.timestampUtc ?? string.Empty;
                LogEvent($"Auto repair attempt {_autoRepairAttempts} queued for compile snapshot {compileSnapshot.timestampUtc}.");
            }

            await ExecuteUserMessageAsync(
                repairPrompt,
                Array.Empty<AgentApprovedAction>(),
                triggeredAutomatically ? "Auto repair request queued" : "Repair request queued",
                triggeredAutomatically ? "Waiting for auto repair proposal" : "Waiting for repair proposal",
                Array.Empty<PendingFileSnapshot>()
            );
        }

        internal static async Task ApplyApprovalAsync(PendingApprovalItem item)
        {
            if (item == null || item.Action == null || _approvalExecutionInFlight)
            {
                return;
            }

            _approvalExecutionInFlight = true;
            try
            {
                var approvedAction = new AgentApprovedAction
                {
                    type = item.Action.type,
                    target = item.Action.target,
                    reason = item.Action.reason,
                    proposalPreview = item.Action.proposalPreview
                };

                if (UnityEditorActionExecutor.IsUnityEditorAction(approvedAction.type))
                {
                    ApplyUnityEditorAction(item, approvedAction);
                    return;
                }

                var snapshots = SnapshotApprovedActions(new[] { approvedAction });
                foreach (var snapshot in snapshots)
                {
                    snapshot.NodeId = item.NodeId;
                }

                await ExecuteUserMessageAsync(
                    $"Apply the approved proposal for {item.Action.target}.",
                    new[] { approvedAction },
                    $"Applying approved action for {item.Action.target}",
                    "Waiting for apply response",
                    snapshots
                );

                ApprovalQueue.RemoveAll(entry => entry.Id == item.Id);
                LogEvent($"Approved and applied {item.Action.target}.");
                NotifyChanged();
            }
            finally
            {
                _approvalExecutionInFlight = false;
            }
        }

        private static void ApplyUnityEditorAction(PendingApprovalItem item, AgentApprovedAction approvedAction)
        {
            bool handled = UnityEditorActionExecutor.TryExecute(approvedAction, out var result);
            _lastActionExecutionResults = AppendExecutionResult(_lastActionExecutionResults, result);
            _lastToolExecutionSummary = BuildLocalToolSummary(_lastActionExecutionResults);
            UpdatePlanNodeAfterApproval(item, result, null);
            _lastWorkflowStatus = result.success ? "Unity action applied" : "Unity action failed";
            _response = result.success
                ? $"Unity action applied:\n[{result.type}] {result.target}\n{result.output}"
                : $"Unity action failed:\n[{result.type}] {result.target}\n{result.output}";

            if (handled && result.success)
            {
                _lastAppliedAssetPath = result.target;
                ApprovalQueue.RemoveAll(entry => entry.Id == item.Id);
                LogEvent($"Applied Unity editor action {approvedAction.type} -> {approvedAction.target}.");
            }
            else
            {
                LogEvent($"Unity editor action failed {approvedAction.type} -> {approvedAction.target}: {result.output}");
            }

            NotifyChanged();
        }

        private static void UpdatePlanNodeAfterApproval(PendingApprovalItem item, AgentActionExecutionResult result, AppliedChangeRecord change)
        {
            string nodeId = item?.NodeId;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var node = PlanTaskNodes.FirstOrDefault(entry => string.Equals(entry.Id, nodeId, StringComparison.Ordinal));
            if (node == null)
            {
                return;
            }

            if (result != null)
            {
                node.ExecutionResults.Add(result);
                node.Status = result.success ? "completed" : "failed";
            }

            if (change != null)
            {
                node.AppliedChanges.Insert(0, change);
            }
        }

        internal static async Task ApproveAllSafeCreatesAsync()
        {
            await ApplyAllApprovalsAsync(safeOnly: true);
        }

        internal static void RejectApproval(string approvalId)
        {
            if (string.IsNullOrWhiteSpace(approvalId))
            {
                return;
            }

            var removed = ApprovalQueue.RemoveAll(item => item.Id == approvalId);
            if (removed > 0)
            {
                LogEvent($"Rejected proposal {approvalId}.");
                NotifyChanged();
            }
        }

        internal static void RejectAllApprovals()
        {
            if (ApprovalQueue.Count == 0)
            {
                return;
            }

            ApprovalQueue.Clear();
            LogEvent("Cleared all pending approvals.");
            NotifyChanged();
        }

        internal static void RefreshAssetDatabase()
        {
            AssetDatabase.Refresh();
            _lastWorkflowStatus = "Asset database refreshed";
            LogEvent("Asset database refreshed.");
            NotifyChanged();
        }

        internal static void FocusLastAppliedAsset(bool openAsset)
        {
            FocusTarget(_lastAppliedAssetPath, openAsset);
        }

        internal static void FocusAppliedChange(string changeId, bool openAsset)
        {
            if (string.IsNullOrWhiteSpace(changeId))
            {
                return;
            }

            var record = AppliedChangeHistory.FirstOrDefault(item => item.Id == changeId);
            if (record == null)
            {
                return;
            }

            FocusTarget(record.Target, openAsset);
        }

        internal static void FocusTarget(string target, bool openAsset)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            string assetPath = ToUnityAssetPath(target);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            if (openAsset)
            {
                AssetDatabase.OpenAsset(asset);
            }
        }

        internal static void RollbackAppliedChange(string changeId)
        {
            if (string.IsNullOrWhiteSpace(changeId))
            {
                return;
            }

            var record = AppliedChangeHistory.FirstOrDefault(item => item.Id == changeId);
            if (record == null)
            {
                return;
            }

            try
            {
                if (record.ExistedBefore)
                {
                    File.WriteAllText(record.AbsolutePath, record.PreviousContent ?? string.Empty);
                }
                else if (File.Exists(record.AbsolutePath))
                {
                    File.Delete(record.AbsolutePath);
                }

                AssetDatabase.Refresh();
                _lastWorkflowStatus = "Rollback applied";
                _response = $"Rolled back {record.Target}.";
                LogEvent($"Rolled back {record.Target}.");
                AppliedChangeHistory.RemoveAll(item => item.Id == changeId);
                MarkNodeRolledBack(record.NodeId, record.Target);
                NotifyChanged();
            }
            catch (Exception exception)
            {
                _lastWorkflowStatus = "Rollback failed";
                _response = $"Rollback failed for {record.Target}:\n{exception.Message}";
                LogEvent($"Rollback failed for {record.Target}: {exception.Message}");
                NotifyChanged();
            }
        }

        internal static void RollbackLatestChanges(int count)
        {
            if (count <= 0 || AppliedChangeHistory.Count == 0)
            {
                return;
            }

            foreach (var item in AppliedChangeHistory.Take(count).ToArray())
            {
                RollbackAppliedChange(item.Id);
            }
        }

        internal static void RollbackAllAppliedChanges()
        {
            if (AppliedChangeHistory.Count == 0)
            {
                return;
            }

            foreach (var item in AppliedChangeHistory.ToArray())
            {
                RollbackAppliedChange(item.Id);
            }
        }

        internal static bool CanAttachLastAppliedScript()
        {
            if (string.IsNullOrWhiteSpace(_lastAppliedAssetPath) || !_lastAppliedAssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        internal static bool HasCompilerErrors()
        {
            var snapshot = CompileDiagnosticsTracker.GetSnapshot();
            return snapshot.errorCount > 0;
        }

        internal static void AttachLastAppliedScriptToSelection()
        {
            if (string.IsNullOrWhiteSpace(_lastAppliedAssetPath))
            {
                return;
            }

            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(_lastAppliedAssetPath);
            if (monoScript == null)
            {
                _response = $"Could not load MonoScript at {_lastAppliedAssetPath}.";
                _lastWorkflowStatus = "Attach failed";
                LogEvent($"Attach failed because {_lastAppliedAssetPath} could not be loaded as a MonoScript.");
                NotifyChanged();
                return;
            }

            var scriptType = monoScript.GetClass();
            if (scriptType == null || !typeof(MonoBehaviour).IsAssignableFrom(scriptType))
            {
                _response = $"The script at {_lastAppliedAssetPath} is not a valid MonoBehaviour type yet. Check compile errors first.";
                _lastWorkflowStatus = "Attach blocked by compile state";
                LogEvent($"Attach blocked because {_lastAppliedAssetPath} is not a valid MonoBehaviour yet.");
                NotifyChanged();
                return;
            }

            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                Undo.AddComponent(gameObject, scriptType);
                EditorUtility.SetDirty(gameObject);
            }

            EditorSceneManager.MarkAllScenesDirty();
            _attachAfterCompile = false;
            _lastWorkflowStatus = "Script attached to selected objects";
            _response = $"Attached {_lastAppliedAssetPath} to {Selection.gameObjects.Length} selected object(s).";
            LogEvent($"Attached {_lastAppliedAssetPath} to {Selection.gameObjects.Length} selected object(s).");
            NotifyChanged();
        }

        private static async Task ExecuteUserMessageAsync(
            string userMessage,
            AgentApprovedAction[] approvedActions,
            string sendingStatus,
            string waitingStatus,
            PendingFileSnapshot[] pendingSnapshots)
        {
            if (_isBusy)
            {
                return;
            }

            var settings = JavaAgentSettings.instance;
            if (!settings.HasConfiguredProviderApiKey())
            {
                _lastWorkflowStatus = "Provider API key missing";
                _response =
                    "Provider API key is missing.\n\n"
                    + "Open Edit > Project Settings > EGS Java Agent, paste the provider key into Local API Key, "
                    + "then click Save Local API Key and Start Agent again.\n\n"
                    + "Required environment variable for the current provider: "
                    + settings.ProviderKeyEnvironmentName;
                LogEvent("Request blocked because provider API key is missing.");
                NotifyChanged();
                return;
            }

            if (_lastDiagnostics != null && !_lastDiagnostics.apiKeyPresent)
            {
                LogEvent("Last diagnostics reported ApiKeyLoaded=false. Restarting local Java agent before retry.");
                await RestartAgentAsync();
            }

            _isBusy = true;
            _lastWorkflowStatus = sendingStatus;
            _activeRequestText = userMessage ?? string.Empty;
            _response = "Calling Java Agent...";
            LogEvent($"{sendingStatus}.");
            NotifyChanged();

            try
            {
                await EnsureAgentRunningAsync();
                var selectedAssets = Selection.assetGUIDs
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .ToArray();

                var selectedObjects = Selection.objects
                    .Where(item => item != null)
                    .Select(item => item.name)
                    .ToArray();

                var activeScene = SceneManager.GetActiveScene();
                var envelope = BuildEnvelope(
                    settings,
                    activeScene.name,
                    selectedAssets,
                    selectedObjects,
                    userMessage,
                    approvedActions
                );

                _lastWorkflowStatus = waitingStatus;
                NotifyChanged();

                var result = await Client.ExecuteAsync(settings.endpoint, envelope);
                ApplyAgentResult(result, envelope);
                TrackAppliedAssetFromResults(result, pendingSnapshots);
            }
            catch (Exception exception)
            {
                _lastWorkflowStatus = "Request failed";
                _response = $"Java Agent call failed:\n{exception.Message}";
                LogEvent($"Request failed: {exception.Message}");
            }
            finally
            {
                _isBusy = false;
                NotifyChanged();
            }
        }

        private static void ApplyAgentResult(AgentResponse result, AgentEnvelope envelope)
        {
            _lastDiagnostics = result.diagnostics;
            _lastSuggestedActions = MergeClientSuggestedActions(
                result.suggestedActions ?? Array.Empty<AgentSuggestedAction>(),
                envelope.payload.userMessage
            );
            _lastActionExecutionResults = result.actionExecutionResults ?? Array.Empty<AgentActionExecutionResult>();
            _lastToolExecutionSummary = result.toolExecutionSummary;
            _lastDetectedIssues = result.detectedIssues ?? Array.Empty<AgentIssue>();
            _lastRequestCompilerMessages = envelope.payload.compilerMessages ?? Array.Empty<string>();
            _lastWorkflowStatus = result.success ? "Agent response received" : "Agent response failed";
            bool isApplyTurn = envelope.payload.approvedActions != null && envelope.payload.approvedActions.Length > 0;
            bool isNodeTurn = _planNodeExecutionInFlight && !isApplyTurn;
            if (isApplyTurn)
            {
                AttachResultsToPlanNodes(_lastActionExecutionResults);
            }
            else if (isNodeTurn)
            {
                MergePlanNodeTurnResult(result);
            }
            else
            {
                RebuildPlanTaskNodes(result, envelope);
            }
            RebuildApprovalQueue(_lastSuggestedActions);
            AppendHistory(result, envelope);
            _response = result.success
                ? BuildResponseText(result)
                : $"Request failed: {result.status}\n{result.assistantMessage}";
            LogEvent($"Agent response {(result.success ? "succeeded" : "failed")} with {_lastSuggestedActions.Length} suggested action(s).");

            if (JavaAgentSettings.instance.autoApproveCreateFiles)
            {
                EditorApplication.delayCall += () => _ = ApproveAllSafeCreatesAsync();
            }

            NotifyChanged();
        }

        private static AgentSuggestedAction[] MergeClientSuggestedActions(AgentSuggestedAction[] serverActions, string userMessage)
        {
            var merged = new List<AgentSuggestedAction>();
            if (serverActions != null)
            {
                merged.AddRange(serverActions.Where(action => action != null));
            }

            foreach (var action in BuildClientSuggestedActions(userMessage))
            {
                bool exists = merged.Any(existing =>
                    string.Equals(existing.type, action.type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.target, action.target, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    merged.Add(action);
                }
            }

            return merged.ToArray();
        }

        private static IEnumerable<AgentSuggestedAction> BuildClientSuggestedActions(string userMessage)
        {
            string normalized = userMessage ?? string.Empty;
            bool wantsTestCharacter =
                normalized.IndexOf("capsule", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("third person", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.Contains("胶囊")
                || normalized.Contains("第三人称")
                || normalized.Contains("测试角色")
                || normalized.Contains("移动脚本");

            if (wantsTestCharacter)
            {
                yield return UnityEditorActionExecutor.CreateTestCharacterSuggestion(
                    "The request asks for a usable scene test character. This Unity editor action creates and selects it directly after approval."
                );
            }
        }

        private static void RebuildPlanTaskNodes(AgentResponse result, AgentEnvelope envelope)
        {
            PlanTaskNodes.Clear();

            var steps = result.planSteps ?? Array.Empty<AgentPlanStep>();
            if (steps.Length == 0)
            {
                PlanTaskNodes.Add(new PlanTaskNode(
                    Guid.NewGuid().ToString("N"),
                    "Agent response",
                    result.planSummary ?? "Review the latest agent response.",
                    "ready",
                    envelope.payload.userMessage
                ));
            }
            else
            {
                for (int index = 0; index < steps.Length; index++)
                {
                    var step = steps[index];
                    PlanTaskNodes.Add(new PlanTaskNode(
                        Guid.NewGuid().ToString("N"),
                        string.IsNullOrWhiteSpace(step.title) ? $"Step {index + 1}" : step.title,
                        step.detail ?? string.Empty,
                        step.status ?? "pending",
                        envelope.payload.userMessage
                    ));
                }
            }

            AttachActionsToPlanNodes(_lastSuggestedActions);
            AttachResultsToPlanNodes(_lastActionExecutionResults);
            if (PlanTaskNodes.Count > 0)
            {
                _selectedPlanNodeId = PlanTaskNodes[0].Id;
            }
        }

        private static void MergePlanNodeTurnResult(AgentResponse result)
        {
            var node = SelectedPlanNode;
            if (node == null)
            {
                return;
            }

            node.Status = result.success ? "review" : "failed";
            node.LastAssistantMessage = result.assistantMessage ?? string.Empty;
            foreach (var action in _lastSuggestedActions)
            {
                if (action == null)
                {
                    continue;
                }

                bool exists = node.SuggestedActions.Any(existing =>
                    string.Equals(existing.type, action.type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.target, action.target, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    node.SuggestedActions.Add(action);
                }
            }

            foreach (var executionResult in _lastActionExecutionResults)
            {
                if (executionResult != null)
                {
                    node.ExecutionResults.Add(executionResult);
                }
            }
        }

        private static void AttachActionsToPlanNodes(IEnumerable<AgentSuggestedAction> actions)
        {
            var nodes = PlanTaskNodes.ToArray();
            if (nodes.Length == 0)
            {
                return;
            }

            foreach (var action in actions ?? Array.Empty<AgentSuggestedAction>())
            {
                if (action == null)
                {
                    continue;
                }

                ResolveNodeForAction(action)?.SuggestedActions.Add(action);
            }
        }

        private static void AttachResultsToPlanNodes(IEnumerable<AgentActionExecutionResult> results)
        {
            foreach (var result in results ?? Array.Empty<AgentActionExecutionResult>())
            {
                if (result == null)
                {
                    continue;
                }

                var node = ResolveNodeForResult(result);
                if (node == null)
                {
                    continue;
                }

                node.ExecutionResults.Add(result);
                node.Status = result.success ? "completed" : "failed";
            }
        }

        private static PlanTaskNode ResolveNodeForAction(AgentSuggestedAction action)
        {
            if (PlanTaskNodes.Count == 0)
            {
                return null;
            }

            if (UnityEditorActionExecutor.IsUnityEditorAction(action.type))
            {
                return FindPlanNode("Apply") ?? PlanTaskNodes.Last();
            }

            if (action.approvalRequired)
            {
                return FindPlanNode("Approve") ?? FindPlanNode("Prepare") ?? PlanTaskNodes.Last();
            }

            return FindPlanNode("Inspect") ?? PlanTaskNodes.First();
        }

        private static PlanTaskNode ResolveNodeForResult(AgentActionExecutionResult result)
        {
            if (PlanTaskNodes.Count == 0)
            {
                return null;
            }

            if (result.type != null && result.type.StartsWith("approved_", StringComparison.OrdinalIgnoreCase))
            {
                return FindPlanNode("Apply") ?? PlanTaskNodes.Last();
            }

            if (result.type != null && result.type.Contains("read", StringComparison.OrdinalIgnoreCase))
            {
                return FindPlanNode("Inspect") ?? PlanTaskNodes.First();
            }

            return PlanTaskNodes.Last();
        }

        private static PlanTaskNode FindPlanNode(string titlePart)
        {
            return PlanTaskNodes.FirstOrDefault(node =>
                node.Title.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0
                || node.Detail.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildPlanNodePrompt(PlanTaskNode node)
        {
            string memorySummary = CodeMemoryStore.RecentEntries.Count == 0
                ? "No stored code memories yet."
                : string.Join(
                    "\n",
                    CodeMemoryStore.RecentEntries.Take(6).Select(memory =>
                        $"- {memory.timestampLocal} | {memory.target} | {memory.summary}"
                    )
                );

            return
                "Execute this selected plan node only. Keep the change small and reviewable.\n\n"
                + "Node: " + node.Title + "\n"
                + "Node detail: " + node.Detail + "\n"
                + "Original request: " + node.Request + "\n\n"
                + "Stored code memories:\n" + memorySummary;
        }

        private static void RebuildApprovalQueue(IEnumerable<AgentSuggestedAction> actions)
        {
            var existingKeys = ApprovalQueue
                .Where(item => item?.Action != null)
                .Select(item => item.Action.type + "::" + item.Action.target)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var action in actions ?? Array.Empty<AgentSuggestedAction>())
            {
                if (action == null || !action.approvalRequired || string.IsNullOrWhiteSpace(action.proposalPreview))
                {
                    continue;
                }

                string key = action.type + "::" + action.target;
                if (existingKeys.Contains(key))
                {
                    continue;
                }

                var node = ResolveNodeForAction(action);
                ApprovalQueue.Add(new PendingApprovalItem(action, node?.Id, node?.Title));
                existingKeys.Add(key);
            }
        }

        private static void TrackAppliedAssetFromResults(AgentResponse result, PendingFileSnapshot[] pendingSnapshots)
        {
            var writeResult = (result.actionExecutionResults ?? Array.Empty<AgentActionExecutionResult>())
                .LastOrDefault(entry =>
                    entry != null
                    && entry.success
                    && !string.IsNullOrWhiteSpace(entry.target)
                    && entry.type != null
                    && entry.type.StartsWith("approved_", StringComparison.OrdinalIgnoreCase));

            if (writeResult == null)
            {
                return;
            }

            _lastAppliedAssetPath = writeResult.target;
            _lastWorkflowStatus = "Approved file applied";
            _autoRepairAttempts = 0;
            _attachAfterCompile = JavaAgentSettings.instance.autoAttachLastAppliedScript;
            AssetDatabase.Refresh();
            LogEvent($"Applied file write to {_lastAppliedAssetPath}.");
            CommitPendingSnapshots(result, pendingSnapshots);
            AttachResultsToPlanNodes(result.actionExecutionResults ?? Array.Empty<AgentActionExecutionResult>());
        }

        private static void OnCompileSnapshotChanged(CompileDiagnosticsTracker.CompileSnapshot snapshot)
        {
            if (snapshot.status == "compiling")
            {
                _lastWorkflowStatus = "Unity is compiling";
                NotifyChanged();
                return;
            }

            if (_attachAfterCompile && snapshot.errorCount == 0 && CanAttachLastAppliedScript())
            {
                EditorApplication.delayCall += AttachLastAppliedScriptToSelection;
            }

            if (!_isBusy && snapshot.errorCount > 0)
            {
                TryQueueAutoRepair(snapshot);
            }

            NotifyChanged();
        }

        private static void TryQueueAutoRepair(CompileDiagnosticsTracker.CompileSnapshot snapshot)
        {
            var settings = JavaAgentSettings.instance;
            if (!settings.autoRepairOnCompileError)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_lastAppliedAssetPath))
            {
                return;
            }

            if (_autoRepairAttempts >= Math.Max(1, settings.maxAutoRepairAttempts))
            {
                LogEvent("Auto repair halted because the maximum attempt count was reached.");
                return;
            }

            if (string.Equals(snapshot.timestampUtc, _lastAutoRepairCompileStamp, StringComparison.Ordinal))
            {
                return;
            }

            EditorApplication.delayCall += () => _ = RepairFromCompilerErrorsAsync(true);
        }

        private static AgentEnvelope BuildEnvelope(
            JavaAgentSettings settings,
            string activeSceneName,
            string[] selectedAssets,
            string[] selectedObjects,
            string userMessage,
            AgentApprovedAction[] approvedActions)
        {
            var compileSnapshot = CompileDiagnosticsTracker.GetSnapshot();
            return new AgentEnvelope
            {
                requestId = Guid.NewGuid().ToString("N"),
                sessionId = settings.sessionId,
                type = "chat",
                payload = new AgentPayload
                {
                    userMessage = userMessage,
                    mode = _mode.ToString().ToLowerInvariant(),
                    sceneContext = BuildSceneContext(activeSceneName, selectedAssets, selectedObjects),
                    activeSceneName = activeSceneName,
                    projectPath = Application.dataPath,
                    selectedAssets = selectedAssets,
                    selectedObjects = selectedObjects,
                    projectSnapshot = BuildProjectSnapshot(activeSceneName, selectedAssets, selectedObjects),
                    compileState = BuildCompileStateSummary(compileSnapshot),
                    compilerMessages = compileSnapshot.messages.Select(message => message.ToSummaryLine()).ToArray(),
                    referenceInputs = ParseReferenceInputs(_referenceInputsText),
                    approvedActions = approvedActions ?? Array.Empty<AgentApprovedAction>()
                },
                metadata = new AgentMetadata
                {
                    unityVersion = Application.unityVersion,
                    clientVersion = "0.2.0",
                    timestampUtc = DateTime.UtcNow.ToString("O")
                }
            };
        }

        private static void AppendHistory(AgentResponse result, AgentEnvelope envelope)
        {
            var preview = envelope.payload.userMessage ?? string.Empty;
            if (preview.Length > 120)
            {
                preview = preview.Substring(0, 120) + "...";
            }

            HistoryItems.Insert(0, new RequestHistoryItem
            {
                timestampUtc = envelope.metadata.timestampUtc,
                mode = envelope.payload.mode,
                promptPreview = preview,
                providerName = result.diagnostics?.providerName ?? "unknown",
                modelName = result.diagnostics?.modelName ?? "unknown",
                status = result.status
            });

            if (HistoryItems.Count > MaxHistoryItems)
            {
                HistoryItems.RemoveAt(HistoryItems.Count - 1);
            }
        }

        private static void LogEvent(string message)
        {
            EventLog.Insert(0, $"{DateTime.Now:HH:mm:ss} | {message}");
            if (EventLog.Count > MaxLogItems)
            {
                EventLog.RemoveAt(EventLog.Count - 1);
            }
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static string BuildSceneContext(
            string activeSceneName,
            string[] selectedAssets,
            string[] selectedObjects)
        {
            var assetText = selectedAssets.Length == 0 ? "none" : string.Join(", ", selectedAssets);
            var objectText = selectedObjects.Length == 0 ? "none" : string.Join(", ", selectedObjects);
            return $"ActiveScene={activeSceneName}; SelectedAssets={assetText}; SelectedObjects={objectText}";
        }

        private static string BuildProjectSnapshot(
            string activeSceneName,
            string[] selectedAssets,
            string[] selectedObjects)
        {
            return $"Scene={activeSceneName}; AssetCount={selectedAssets.Length}; ObjectCount={selectedObjects.Length}; Assets=[{string.Join(", ", selectedAssets)}]; Objects=[{string.Join(", ", selectedObjects)}]";
        }

        private static string BuildEffectivePrompt(string basePrompt)
        {
            var prefix = JavaAgentSkillCatalog.GetInstructionPrefix(_skillProfile);
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return basePrompt ?? string.Empty;
            }

            return prefix + "\n\nUser task:\n" + (basePrompt ?? string.Empty);
        }

        private static string[] ParseReferenceInputs(string referenceInputsText)
        {
            if (string.IsNullOrWhiteSpace(referenceInputsText))
            {
                return Array.Empty<string>();
            }

            return referenceInputsText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct()
                .ToArray();
        }

        private static string BuildCompileStateSummary(CompileDiagnosticsTracker.CompileSnapshot compileSnapshot)
        {
            return $"status={compileSnapshot.status}; errors={compileSnapshot.errorCount}; warnings={compileSnapshot.warningCount}; updatedUtc={compileSnapshot.timestampUtc}";
        }

        private static string BuildRepairPrompt(string[] compilerLines, string[] approvedTargets)
        {
            var targetSummary = approvedTargets.Length == 0
                ? "No recently approved target files are known."
                : "Recently approved target files:\n- " + string.Join("\n- ", approvedTargets);

            return
                "Fix the current Unity compile errors using the provided compiler messages and project context.\n\n"
                + targetSummary
                + "\n\nCompiler errors:\n- "
                + string.Join("\n- ", compilerLines)
                + "\n\nInspect the relevant files first, then propose the smallest safe repair.";
        }

        private static string BuildResponseText(AgentResponse result)
        {
            var planBlock = result.planSteps == null || result.planSteps.Length == 0
                ? result.planSummary
                : string.Join(
                    "\n",
                    result.planSteps.Select(step => $"- [{step.status}] {step.title}: {step.detail}")
                );

            var diagnostics = result.diagnostics == null
                ? "Diagnostics unavailable."
                : $"Provider={result.diagnostics.providerName}, Gateway={result.diagnostics.gatewayKind}, Model={result.diagnostics.modelName}, EffectiveModel={result.diagnostics.effectiveModelName}, ApiKeyLoaded={result.diagnostics.apiKeyPresent}, Mode={result.diagnostics.mode}, MemoryHits={result.diagnostics.memoryHits}, Scene={result.diagnostics.activeSceneName}, SelectedAssets={result.diagnostics.selectedAssetCount}, SelectedObjects={result.diagnostics.selectedObjectCount}, CompileState={result.diagnostics.compileState}, CompilerMessages={result.diagnostics.compilerMessageCount}";

            var actions = result.suggestedActions == null || result.suggestedActions.Length == 0
                ? "No suggested actions."
                : string.Join(
                    "\n",
                    result.suggestedActions.Select(BuildActionSummary)
                );

            var actionResults = result.actionExecutionResults == null || result.actionExecutionResults.Length == 0
                ? "No action execution results."
                : string.Join(
                    "\n\n",
                    result.actionExecutionResults.Select(execution => $"[{(execution.success ? "ok" : "failed")}] {execution.type} -> {execution.target}\n{BuildExecutionSummary(execution)}")
                );

            var issues = result.detectedIssues == null || result.detectedIssues.Length == 0
                ? "No issues detected."
                : string.Join(
                    "\n",
                    result.detectedIssues.Select(issue => $"- [{issue.severity}] {issue.category} -> {issue.target}: {issue.detail}")
                );

            var toolSummary = result.toolExecutionSummary == null
                ? "No tool execution summary."
                : result.toolExecutionSummary.summary;

            return $"Status: {result.status}\n\nPlan:\n{planBlock}\n\nAssistant:\n{result.assistantMessage}\n\nSuggested Actions:\n{actions}\n\nAction Results:\n{actionResults}\n\nTool Summary:\n{toolSummary}\n\nDetected Issues:\n{issues}\n\nDiagnostics:\n{diagnostics}";
        }

        private static string BuildActionSummary(AgentSuggestedAction action)
        {
            if (action == null)
            {
                return "- unknown action";
            }

            var approvalTag = action.approvalRequired ? " [approval required]" : string.Empty;
            var baseLine = $"- {action.type}: {action.target}{approvalTag} ({action.reason})";

            if (string.IsNullOrWhiteSpace(action.proposalPreview))
            {
                return baseLine;
            }

            var firstLineBreak = action.proposalPreview.IndexOf('\n');
            var proposalHeadline = firstLineBreak >= 0
                ? action.proposalPreview.Substring(0, firstLineBreak)
                : action.proposalPreview;
            return $"{baseLine}\n  Proposal: {proposalHeadline}";
        }

        private static string BuildExecutionSummary(AgentActionExecutionResult execution)
        {
            if (!IsRangeReadResult(execution))
            {
                return execution.output;
            }

            var output = execution.output ?? string.Empty;
            var firstLineBreak = output.IndexOf('\n');
            return firstLineBreak >= 0 ? output.Substring(0, firstLineBreak) : output;
        }

        private static AgentActionExecutionResult[] AppendExecutionResult(
            AgentActionExecutionResult[] existingResults,
            AgentActionExecutionResult newResult)
        {
            var results = new List<AgentActionExecutionResult>();
            if (existingResults != null)
            {
                results.AddRange(existingResults.Where(result => result != null));
            }

            if (newResult != null)
            {
                results.Add(newResult);
            }

            return results.ToArray();
        }

        private static AgentToolExecutionSummary BuildLocalToolSummary(AgentActionExecutionResult[] results)
        {
            int attempted = results?.Length ?? 0;
            int succeeded = results?.Count(result => result != null && result.success) ?? 0;
            int failed = attempted - succeeded;
            return new AgentToolExecutionSummary
            {
                attemptedActions = attempted,
                successfulActions = succeeded,
                failedActions = failed,
                summary = $"Attempted {attempted} action(s); {succeeded} succeeded, {failed} failed."
            };
        }

        private static bool IsRangeReadResult(AgentActionExecutionResult result)
        {
            return result != null
                && string.Equals(result.type, "read_file_range", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(result.output)
                && result.output.StartsWith("Lines ", StringComparison.Ordinal);
        }

        private static AgentMode ParsePreferredMode(string preferredMode)
        {
            return preferredMode?.ToLowerInvariant() switch
            {
                "ask" => AgentMode.Ask,
                "plan" => AgentMode.Plan,
                _ => AgentMode.Agent
            };
        }

        private static PendingFileSnapshot[] SnapshotApprovedActions(AgentApprovedAction[] approvedActions)
        {
            if (approvedActions == null || approvedActions.Length == 0)
            {
                return Array.Empty<PendingFileSnapshot>();
            }

            var snapshots = new List<PendingFileSnapshot>();
            foreach (var action in approvedActions)
            {
                if (action == null || string.IsNullOrWhiteSpace(action.target))
                {
                    continue;
                }

                var absolutePath = ResolveProjectTargetPath(action.target);
                if (string.IsNullOrWhiteSpace(absolutePath))
                {
                    continue;
                }

                bool existedBefore = File.Exists(absolutePath);
                string previousContent = existedBefore ? File.ReadAllText(absolutePath) : null;
                snapshots.Add(new PendingFileSnapshot(action.target, absolutePath, existedBefore, previousContent, action.type));
            }

            return snapshots.ToArray();
        }

        private static void CommitPendingSnapshots(AgentResponse result, PendingFileSnapshot[] pendingSnapshots)
        {
            if (pendingSnapshots == null || pendingSnapshots.Length == 0)
            {
                return;
            }

            var successfulTargets = (result.actionExecutionResults ?? Array.Empty<AgentActionExecutionResult>())
                .Where(entry => entry != null && entry.success && !string.IsNullOrWhiteSpace(entry.target) && entry.type != null && entry.type.StartsWith("approved_", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.target.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var snapshot in pendingSnapshots)
            {
                if (!successfulTargets.Contains(snapshot.Target.Replace('\\', '/')))
                {
                    continue;
                }

                string currentContent = File.Exists(snapshot.AbsolutePath)
                    ? File.ReadAllText(snapshot.AbsolutePath)
                    : string.Empty;
                string changeId = Guid.NewGuid().ToString("N");
                string memoryId = Guid.NewGuid().ToString("N");
                var record = new AppliedChangeRecord(
                    changeId,
                    snapshot.NodeId,
                    memoryId,
                    snapshot.Target,
                    snapshot.AbsolutePath,
                    snapshot.ExistedBefore,
                    snapshot.PreviousContent,
                    currentContent,
                    snapshot.ActionType,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                );
                AppliedChangeHistory.Insert(0, record);
                RegisterAppliedChangeWithNode(record);
                CodeMemoryStore.Add(new CodeMemoryEntry
                {
                    id = memoryId,
                    nodeId = snapshot.NodeId,
                    request = _activeRequestText,
                    target = snapshot.Target,
                    actionType = snapshot.ActionType,
                    timestampLocal = record.TimestampLocal,
                    existedBefore = snapshot.ExistedBefore,
                    beforeContent = snapshot.PreviousContent ?? string.Empty,
                    afterContent = currentContent,
                    summary = snapshot.ActionType + " -> " + snapshot.Target
                });
            }

            while (AppliedChangeHistory.Count > 20)
            {
                AppliedChangeHistory.RemoveAt(AppliedChangeHistory.Count - 1);
            }
        }

        private static void RegisterAppliedChangeWithNode(AppliedChangeRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.NodeId))
            {
                return;
            }

            var node = PlanTaskNodes.FirstOrDefault(item => string.Equals(item.Id, record.NodeId, StringComparison.Ordinal));
            if (node == null)
            {
                return;
            }

            node.AppliedChanges.Insert(0, record);
            node.Status = "completed";
        }

        private static void MarkNodeRolledBack(string nodeId, string target)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var node = PlanTaskNodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.Ordinal));
            if (node == null)
            {
                return;
            }

            node.Status = "rolled_back";
            node.ExecutionResults.Add(new AgentActionExecutionResult
            {
                type = "rollback",
                target = target,
                success = true,
                output = "Rolled back from node history."
            });
        }

        private static string ResolveProjectTargetPath(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            string normalizedTarget = target.Replace('\\', '/');
            if (Path.IsPathRooted(normalizedTarget))
            {
                return normalizedTarget;
            }

            string assetsPath = Application.dataPath.Replace('\\', '/');
            string projectRoot = Directory.GetParent(assetsPath)?.FullName?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return string.Empty;
            }

            if (normalizedTarget.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(projectRoot, normalizedTarget.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.Combine(assetsPath, normalizedTarget.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ToUnityAssetPath(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            string normalized = target.Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            string assetsPath = Application.dataPath.Replace('\\', '/');
            string fullPath = ResolveProjectTargetPath(target).Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(fullPath) && fullPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + fullPath.Substring(assetsPath.Length).TrimStart('/');
            }

            return string.Empty;
        }

        internal static string GetExistingFileContentForTarget(string target)
        {
            var absolutePath = ResolveProjectTargetPath(target);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return string.Empty;
            }

            try
            {
                return File.ReadAllText(absolutePath);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static string ExtractProposalCodeContent(string proposalPreview)
        {
            if (string.IsNullOrWhiteSpace(proposalPreview))
            {
                return string.Empty;
            }

            int firstFence = proposalPreview.IndexOf("```", StringComparison.Ordinal);
            if (firstFence < 0)
            {
                return proposalPreview;
            }

            int lineBreak = proposalPreview.IndexOf('\n', firstFence);
            if (lineBreak < 0)
            {
                return proposalPreview;
            }

            int closingFence = proposalPreview.IndexOf("```", lineBreak + 1, StringComparison.Ordinal);
            if (closingFence < 0)
            {
                return proposalPreview.Substring(lineBreak + 1);
            }

            return proposalPreview.Substring(lineBreak + 1, closingFence - lineBreak - 1).TrimEnd();
        }

        internal static string BuildProposalDiffPreview(string target, string proposalPreview)
        {
            string before = NormalizeLines(GetExistingFileContentForTarget(target));
            string after = NormalizeLines(ExtractProposalCodeContent(proposalPreview));

            if (string.IsNullOrWhiteSpace(after))
            {
                return "No proposal code block was available to diff.";
            }

            var beforeLines = before.Split('\n');
            var afterLines = after.Split('\n');
            int maxLines = Math.Max(beforeLines.Length, afterLines.Length);
            var lines = new List<string>(maxLines + 4)
            {
                $"Target: {target}",
                string.IsNullOrWhiteSpace(before) ? "[create]" : "[replace]",
                string.Empty
            };

            for (int index = 0; index < maxLines; index++)
            {
                string left = index < beforeLines.Length ? beforeLines[index] : null;
                string right = index < afterLines.Length ? afterLines[index] : null;

                if (left == right)
                {
                    lines.Add("  " + (right ?? string.Empty));
                    continue;
                }

                if (left != null)
                {
                    lines.Add("- " + left);
                }

                if (right != null)
                {
                    lines.Add("+ " + right);
                }
            }

            return string.Join("\n", lines.Take(220));
        }

        private static string NormalizeLines(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return input.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        internal sealed class PendingApprovalItem
        {
            internal PendingApprovalItem(AgentSuggestedAction action, string nodeId, string nodeTitle)
            {
                Id = Guid.NewGuid().ToString("N");
                Action = action;
                NodeId = nodeId ?? string.Empty;
                NodeTitle = nodeTitle ?? string.Empty;
                IsUnityEditorAction = UnityEditorActionExecutor.IsUnityEditorAction(action.type);
                IsSafeCreateCandidate =
                    string.Equals(action.type, "suggest_create_file", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(action.target)
                    && action.target.Replace('\\', '/').StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            }

            internal string Id { get; }
            internal AgentSuggestedAction Action { get; }
            internal string NodeId { get; }
            internal string NodeTitle { get; }
            internal bool IsUnityEditorAction { get; }
            internal bool IsSafeCreateCandidate { get; }
        }

        internal sealed class AppliedChangeRecord
        {
            internal AppliedChangeRecord(string id, string nodeId, string memoryId, string target, string absolutePath, bool existedBefore, string previousContent, string currentContent, string actionType, string timestampLocal)
            {
                Id = id;
                NodeId = nodeId ?? string.Empty;
                MemoryId = memoryId ?? string.Empty;
                Target = target;
                AbsolutePath = absolutePath;
                ExistedBefore = existedBefore;
                PreviousContent = previousContent;
                CurrentContent = currentContent;
                ActionType = actionType;
                TimestampLocal = timestampLocal;
            }

            internal string Id { get; }
            internal string NodeId { get; }
            internal string MemoryId { get; }
            internal string Target { get; }
            internal string AbsolutePath { get; }
            internal bool ExistedBefore { get; }
            internal string PreviousContent { get; }
            internal string CurrentContent { get; }
            internal string ActionType { get; }
            internal string TimestampLocal { get; }
        }

        private sealed class PendingFileSnapshot
        {
            internal PendingFileSnapshot(string target, string absolutePath, bool existedBefore, string previousContent, string actionType)
            {
                Target = target;
                AbsolutePath = absolutePath;
                ExistedBefore = existedBefore;
                PreviousContent = previousContent;
                ActionType = actionType;
            }

            internal string NodeId { get; set; }
            internal string Target { get; }
            internal string AbsolutePath { get; }
            internal bool ExistedBefore { get; }
            internal string PreviousContent { get; }
            internal string ActionType { get; }
        }

        internal sealed class PlanTaskNode
        {
            internal PlanTaskNode(string id, string title, string detail, string status, string request)
            {
                Id = id;
                Title = title;
                Detail = detail;
                Status = status;
                Request = request;
            }

            internal string Id { get; }
            internal string Title { get; }
            internal string Detail { get; }
            internal string Request { get; }
            internal string Status { get; set; }
            internal string LastAssistantMessage { get; set; }
            internal bool IsTerminal =>
                string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Status, "rolled_back", StringComparison.OrdinalIgnoreCase);
            internal List<AgentSuggestedAction> SuggestedActions { get; } = new();
            internal List<AgentActionExecutionResult> ExecutionResults { get; } = new();
            internal List<AppliedChangeRecord> AppliedChanges { get; } = new();
        }
    }
}
