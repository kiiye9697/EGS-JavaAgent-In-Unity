using System;
using System.Linq;
using EGS.JavaAgent.Runtime;
using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal sealed class JavaAgentWindow : EditorWindow
    {
        private readonly Color _nodeIdleColor = new Color(0.24f, 0.24f, 0.24f);
        private readonly Color _nodeActiveColor = new Color(0.16f, 0.46f, 0.73f);
        private readonly Color _nodeDoneColor = new Color(0.18f, 0.54f, 0.33f);
        private Vector2 _promptScroll;
        private Vector2 _referenceScroll;
        private Vector2 _responseScroll;
        private Vector2 _issuesScroll;
        private Vector2 _historyScroll;
        private Vector2 _implementedScroll;
        private Vector2 _planNodeScroll;
        private Vector2 _memoryScroll;
        private Vector2 _toolCatalogScroll;
        private Vector2 _windowScroll;
        private Vector2 _selectedNodeScroll;
        private bool _showRawResponse;

        [MenuItem("Window/EGS Java Agent/Workspace")]
        private static void OpenWorkspace()
        {
            var window = GetWindow<JavaAgentWindow>(LocalizationSystem.T("workspace.title"));
            window.minSize = new Vector2(420f, 420f);
        }

        [MenuItem("Window/EGS Java Agent")]
        private static void OpenWorkspaceAlias()
        {
            OpenWorkspace();
        }

        private void OnEnable()
        {
            JavaAgentSessionState.Changed += Repaint;
        }

        private void OnDisable()
        {
            JavaAgentSessionState.Changed -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(6f);
            DrawSetupWarningCard();
            DrawCommandStrip();
            EditorGUILayout.Space(6f);

            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
            if (position.width < 760f)
            {
                DrawWorkspaceColumn(false);
                DrawInsightColumn();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                DrawWorkspaceColumn(true);
                DrawInsightColumn();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(LocalizationSystem.T("workspace.title"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.contentColor = JavaAgentSessionState.AgentHealthKnown
                ? (JavaAgentSessionState.AgentHealthy ? Color.green : new Color(1f, 0.75f, 0.2f))
                : Color.white;
            GUILayout.Label(LocalizationSystem.T("workspace.status") + ": " + JavaAgentSessionState.AgentHealthStatus, EditorStyles.miniLabel);
            GUI.contentColor = Color.white;

            if (GUILayout.Button(LocalizationSystem.CurrentLanguage == JavaAgentLanguage.Chinese ? "EN" : "中文", EditorStyles.toolbarButton, GUILayout.Width(45f)))
            {
                LocalizationSystem.CurrentLanguage = LocalizationSystem.CurrentLanguage == JavaAgentLanguage.Chinese
                    ? JavaAgentLanguage.English
                    : JavaAgentLanguage.Chinese;
            }

            if (GUILayout.Button(LocalizationSystem.T("workspace.settings"), EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                SettingsService.OpenProjectSettings("Project/EGS Java Agent");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCommandStrip()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationSystem.T("action.primary"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(LocalizationSystem.T("action.start"), GUILayout.Height(30f)))
            {
                _ = JavaAgentSessionState.EnsureAgentRunningAsync();
            }

            if (GUILayout.Button(LocalizationSystem.T("action.restart"), GUILayout.Height(30f)))
            {
                _ = JavaAgentSessionState.RestartAgentAsync();
            }

            if (GUILayout.Button(LocalizationSystem.T("action.check"), GUILayout.Height(30f)))
            {
                _ = JavaAgentSessionState.RefreshAgentHealthAsync();
            }

            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
            if (GUILayout.Button(LocalizationSystem.T("action.send"), GUILayout.Height(30f)))
            {
                _ = JavaAgentSessionState.SendPromptAsync();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(LocalizationSystem.T("action.approval"), GUILayout.Height(26f)))
            {
                JavaAgentApprovalWindow.OpenWindow();
            }

            if (GUILayout.Button(LocalizationSystem.T("action.debug"), GUILayout.Height(26f)))
            {
                JavaAgentDebugWindow.OpenWindow();
            }

            EditorGUI.BeginDisabledGroup(!JavaAgentSessionState.CanAttachLastAppliedScript());
            if (GUILayout.Button(LocalizationSystem.T("action.attach"), GUILayout.Height(26f)))
            {
                JavaAgentSessionState.AttachLastAppliedScriptToSelection();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSetupWarningCard()
        {
            var settings = JavaAgentSettings.instance;
            bool apiKeyMissing =
                (JavaAgentSessionState.LastDiagnostics != null && !JavaAgentSessionState.LastDiagnostics.apiKeyPresent)
                || !settings.HasConfiguredProviderApiKey();

            if (!apiKeyMissing)
            {
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox(
                LocalizationSystem.T("warning.key", settings.provider, settings.ProviderKeyEnvironmentName),
                MessageType.Warning
            );
            if (GUILayout.Button(LocalizationSystem.T("action.token"), GUILayout.Height(24f)))
            {
                SettingsService.OpenProjectSettings("Project/EGS Java Agent");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawWorkspaceColumn(bool fixedWidth)
        {
            if (fixedWidth)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.48f));
            }
            else
            {
                EditorGUILayout.BeginVertical();
            }

            DrawStatusCard();
            DrawPlanNodesCard();
            DrawSelectedNodeCard();
            DrawPromptCard();
            DrawActionCard();
            DrawHistoryCard();
            EditorGUILayout.EndVertical();
        }

        private void DrawInsightColumn()
        {
            EditorGUILayout.BeginVertical();
            DrawResponseCard();
            DrawToolCatalogCard();
            DrawImplementedTargetsCard();
            DrawCodeMemoryCard();
            DrawIssuesCard();
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusCard()
        {
            var snapshot = JavaAgentSessionState.CurrentCompileSnapshot;
            var diagnostics = JavaAgentSessionState.LastDiagnostics;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationSystem.T("status.live"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(LocalizationSystem.T("status.workflow"), JavaAgentSessionState.LastWorkflowStatus);
            EditorGUILayout.LabelField(LocalizationSystem.T("status.skill"), JavaAgentSkillCatalog.GetLabel(JavaAgentSessionState.SkillProfile));
            EditorGUILayout.LabelField(LocalizationSystem.T("status.selection"), JavaAgentSessionState.BuildSelectionSummary());
            EditorGUILayout.LabelField(LocalizationSystem.T("status.pending"), JavaAgentSessionState.PendingApprovals.Count.ToString());
            EditorGUILayout.LabelField(LocalizationSystem.T("status.rollback"), JavaAgentSessionState.AppliedChanges.Count.ToString());
            EditorGUILayout.LabelField(LocalizationSystem.T("status.compile"), $"{snapshot.status} | errors={snapshot.errorCount} warnings={snapshot.warningCount}");

            if (diagnostics != null)
            {
                EditorGUILayout.LabelField(LocalizationSystem.T("status.runtime"), $"{diagnostics.providerName} / {diagnostics.gatewayKind} / {diagnostics.effectiveModelName}");
            }

            DrawPipeline(snapshot);
            EditorGUILayout.EndVertical();
        }

        private void DrawPipeline(CompileDiagnosticsTracker.CompileSnapshot snapshot)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(LocalizationSystem.T("node.flow"), EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Skill, LocalizationSystem.T("node.skill"), JavaAgentSkillCatalog.GetLabel(JavaAgentSessionState.SkillProfile), JavaAgentSessionState.SkillProfile != JavaAgentSkillProfile.GeneralAgent, true);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Reference, LocalizationSystem.T("node.reference"), string.IsNullOrWhiteSpace(JavaAgentSessionState.ReferenceInputsText) ? "none" : "attached", !string.IsNullOrWhiteSpace(JavaAgentSessionState.ReferenceInputsText), true);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Inspect, LocalizationSystem.T("node.inspect"), JavaAgentSessionState.LastActionExecutionResults.Length > 0 ? "tool reads" : "waiting", JavaAgentSessionState.LastActionExecutionResults.Length > 0, JavaAgentSessionState.IsBusy);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Approve, LocalizationSystem.T("node.approve"), JavaAgentSessionState.PendingApprovals.Count.ToString() + " queued", JavaAgentSessionState.PendingApprovals.Count > 0, false);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Apply, LocalizationSystem.T("node.apply"), string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath) ? "none" : "written", !string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath), false);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Repair, LocalizationSystem.T("node.repair"), snapshot.errorCount > 0 ? "errors detected" : "stable", JavaAgentSessionState.AutoRepairAttempts > 0, snapshot.errorCount > 0);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNodeCard(JavaAgentSessionState.WorkflowNode node, string title, string caption, bool isDone, bool isActive)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = isActive ? _nodeActiveColor : isDone ? _nodeDoneColor : _nodeIdleColor;
            EditorGUILayout.BeginVertical("helpbox", GUILayout.MinHeight(52f));
            GUI.backgroundColor = previousColor;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(caption, EditorStyles.wordWrappedMiniLabel);
            bool isSelected = JavaAgentSessionState.SelectedWorkflowNode == node;
            if (GUILayout.Toggle(isSelected, isSelected ? LocalizationSystem.T("node.selectedButton") : LocalizationSystem.T("node.select"), "Button"))
            {
                JavaAgentSessionState.SelectedWorkflowNode = node;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedNodeCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationSystem.T("node.selected"), EditorStyles.boldLabel);
            var node = JavaAgentSessionState.SelectedWorkflowNode;
            EditorGUILayout.LabelField(GetNodeTitle(node), EditorStyles.miniBoldLabel);
            _selectedNodeScroll = EditorGUILayout.BeginScrollView(_selectedNodeScroll, GUILayout.MinHeight(44f), GUILayout.MaxHeight(72f));
            EditorGUILayout.LabelField(GetNodeDescription(node), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            switch (node)
            {
                case JavaAgentSessionState.WorkflowNode.Reference:
                    if (GUILayout.Button("Use First Reference"))
                    {
                        FocusFirstReference();
                    }
                    break;
                case JavaAgentSessionState.WorkflowNode.Approve:
                    if (GUILayout.Button("Open Approval Queue"))
                    {
                        JavaAgentApprovalWindow.OpenWindow();
                    }
                    break;
                case JavaAgentSessionState.WorkflowNode.Apply:
                    EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath));
                    if (GUILayout.Button("Focus Implemented Asset"))
                    {
                        JavaAgentSessionState.FocusLastAppliedAsset(false);
                    }

                    if (GUILayout.Button("Open Implemented Asset"))
                    {
                        JavaAgentSessionState.FocusLastAppliedAsset(true);
                    }
                    EditorGUI.EndDisabledGroup();
                    break;
                case JavaAgentSessionState.WorkflowNode.Repair:
                    if (GUILayout.Button("Open Debug Console"))
                    {
                        JavaAgentDebugWindow.OpenWindow();
                    }
                    break;
                case JavaAgentSessionState.WorkflowNode.Inspect:
                    if (GUILayout.Button("Focus Selected Asset"))
                    {
                        FocusCurrentlySelectedAsset();
                    }
                    break;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawPlanNodesCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Plan Nodes / 计划节点", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.PlanNodes.Count == 0 || JavaAgentSessionState.IsBusy || JavaAgentSessionState.WorkflowRunnerInFlight);
            if (GUILayout.Button("Run All", GUILayout.Width(72f)))
            {
                _ = JavaAgentSessionState.RunAllPlanNodesAsync(applySafeApprovals: false);
            }

            if (GUILayout.Button("Run+Safe", GUILayout.Width(82f)))
            {
                _ = JavaAgentSessionState.RunAllPlanNodesAsync(applySafeApprovals: true);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.SelectedPlanNode == null || JavaAgentSessionState.IsBusy || JavaAgentSessionState.WorkflowRunnerInFlight);
            if (GUILayout.Button("Run Selected", GUILayout.Width(110f)))
            {
                _ = JavaAgentSessionState.ExecuteSelectedPlanNodeAsync();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (JavaAgentSessionState.PlanNodes.Count == 0)
            {
                EditorGUILayout.HelpBox("Use Plan mode to generate node tasks. Each node will keep actions, results, and code snapshots.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            _planNodeScroll = EditorGUILayout.BeginScrollView(_planNodeScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
            foreach (var planNode in JavaAgentSessionState.PlanNodes)
            {
                bool selected = string.Equals(JavaAgentSessionState.SelectedPlanNodeId, planNode.Id, StringComparison.Ordinal);
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = selected ? new Color(0.18f, 0.42f, 0.65f) : previous;
                if (GUILayout.Toggle(selected, $"{planNode.Status} | {planNode.Title}", "Button"))
                {
                    JavaAgentSessionState.SelectPlanNode(planNode.Id);
                }
                GUI.backgroundColor = previous;

                if (selected)
                {
                    EditorGUILayout.BeginVertical("helpbox");
                    EditorGUILayout.LabelField(planNode.Detail, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField($"Actions: {planNode.SuggestedActions.Count} | Results: {planNode.ExecutionResults.Count} | Changes: {planNode.AppliedChanges.Count}", EditorStyles.miniLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
                    if (GUILayout.Button("Apply Node Safe", GUILayout.Height(22f)))
                    {
                        _ = JavaAgentSessionState.ApplySelectedNodeApprovalsAsync(safeOnly: true);
                    }

                    if (GUILayout.Button("Apply Node All", GUILayout.Height(22f)))
                    {
                        _ = JavaAgentSessionState.ApplySelectedNodeApprovalsAsync(safeOnly: false);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();

                    foreach (var action in planNode.SuggestedActions.Take(3))
                    {
                        EditorGUILayout.LabelField("Action", $"{action.type} -> {action.target}", EditorStyles.miniLabel);
                    }

                    foreach (var result in planNode.ExecutionResults.Take(3))
                    {
                        EditorGUILayout.LabelField("Result", $"{(result.success ? "ok" : "failed")} {result.type} -> {result.target}", EditorStyles.miniLabel);
                    }

                    foreach (var change in planNode.AppliedChanges.Take(2))
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(change.Target, EditorStyles.miniLabel);
                        if (GUILayout.Button("Focus", GUILayout.Width(58f)))
                        {
                            JavaAgentSessionState.FocusAppliedChange(change.Id, false);
                        }

                        if (GUILayout.Button("Rollback", GUILayout.Width(76f)))
                        {
                            JavaAgentSessionState.RollbackAppliedChange(change.Id);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawImplementedTargetsCard()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(190f));
            EditorGUILayout.LabelField(LocalizationSystem.T("targets.title"), EditorStyles.boldLabel);

            if (JavaAgentSessionState.AppliedChanges.Count == 0)
            {
                EditorGUILayout.HelpBox(LocalizationSystem.T("targets.empty"), MessageType.None);
            }
            else
            {
                _implementedScroll = EditorGUILayout.BeginScrollView(_implementedScroll, GUILayout.ExpandHeight(true));
                foreach (var item in JavaAgentSessionState.AppliedChanges)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField(item.Target);
                    EditorGUILayout.LabelField(item.TimestampLocal, EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Focus", GUILayout.Height(22f)))
                    {
                        JavaAgentSessionState.FocusAppliedChange(item.Id, false);
                    }

                    if (GUILayout.Button("Open", GUILayout.Height(22f)))
                    {
                        JavaAgentSessionState.FocusAppliedChange(item.Id, true);
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawToolCatalogCard()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(180f));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Unity Tool Catalog / MCP-like 工具", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
            if (GUILayout.Button("Apply Safe", GUILayout.Width(82f)))
            {
                _ = JavaAgentSessionState.ApplyAllApprovalsAsync(safeOnly: true);
            }

            if (GUILayout.Button("Apply All", GUILayout.Width(74f)))
            {
                _ = JavaAgentSessionState.ApplyAllApprovalsAsync(safeOnly: false);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            _toolCatalogScroll = EditorGUILayout.BeginScrollView(_toolCatalogScroll, GUILayout.ExpandHeight(true));
            foreach (var tool in JavaAgentSessionState.UnityTools)
            {
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.LabelField($"{tool.Group} | {tool.Name}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(tool.Description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"Action: {tool.ActionType} | Approval: {(tool.ApprovalRequired ? "required" : "optional")}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCodeMemoryCard()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(220f));
            EditorGUILayout.LabelField("Code Memory / 代码记忆", EditorStyles.boldLabel);

            if (JavaAgentSessionState.CodeMemories.Count == 0)
            {
                EditorGUILayout.HelpBox("No code memories yet. Approved file writes are stored here with before/after content for later iteration.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            _memoryScroll = EditorGUILayout.BeginScrollView(_memoryScroll, GUILayout.ExpandHeight(true));
            foreach (var memory in JavaAgentSessionState.CodeMemories.Take(12))
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(memory.target, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{memory.timestampLocal} | {memory.actionType}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(memory.summary ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Use Before", GUILayout.Height(22f)))
                {
                    JavaAgentSessionState.UseCodeMemoryAsPrompt(memory.id, false);
                }

                if (GUILayout.Button("Use After", GUILayout.Height(22f)))
                {
                    JavaAgentSessionState.UseCodeMemoryAsPrompt(memory.id, true);
                }

                if (GUILayout.Button("Forget", GUILayout.Width(58f), GUILayout.Height(22f)))
                {
                    JavaAgentSessionState.DeleteCodeMemory(memory.id);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPromptCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationSystem.T("prompt.title"), EditorStyles.boldLabel);
            JavaAgentSessionState.Mode = (AgentMode)EditorGUILayout.EnumPopup(LocalizationSystem.T("prompt.mode"), JavaAgentSessionState.Mode);
            JavaAgentSessionState.SkillProfile = (JavaAgentSkillProfile)EditorGUILayout.EnumPopup(LocalizationSystem.T("prompt.skill"), JavaAgentSessionState.SkillProfile);
            EditorGUILayout.HelpBox(JavaAgentSkillCatalog.GetDescription(JavaAgentSessionState.SkillProfile), MessageType.None);

            _promptScroll = EditorGUILayout.BeginScrollView(_promptScroll, GUILayout.MinHeight(180f));
            var newPrompt = EditorGUILayout.TextArea(JavaAgentSessionState.Prompt, GUILayout.ExpandHeight(true));
            if (!string.Equals(newPrompt, JavaAgentSessionState.Prompt, StringComparison.Ordinal))
            {
                JavaAgentSessionState.Prompt = newPrompt;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(LocalizationSystem.T("prompt.references"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                LocalizationSystem.T("prompt.referenceHelp"),
                MessageType.None
            );
            _referenceScroll = EditorGUILayout.BeginScrollView(_referenceScroll, GUILayout.MinHeight(72f), GUILayout.MaxHeight(120f));
            var newReferenceInputs = EditorGUILayout.TextArea(JavaAgentSessionState.ReferenceInputsText, GUILayout.ExpandHeight(true));
            if (!string.Equals(newReferenceInputs, JavaAgentSessionState.ReferenceInputsText, StringComparison.Ordinal))
            {
                JavaAgentSessionState.ReferenceInputsText = newReferenceInputs;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationSystem.T("execution.title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                LocalizationSystem.T("execution.help"),
                MessageType.None
            );

            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
            if (GUILayout.Button(LocalizationSystem.T("execution.send"), GUILayout.Height(28f)))
            {
                _ = JavaAgentSessionState.SendPromptAsync();
            }

            if (GUILayout.Button(LocalizationSystem.T("action.approval"), GUILayout.Height(24f)))
            {
                JavaAgentApprovalWindow.OpenWindow();
            }

            if (GUILayout.Button(LocalizationSystem.T("execution.repair"), GUILayout.Height(24f)))
            {
                _ = JavaAgentSessionState.RepairFromCompilerErrorsAsync(false);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(LocalizationSystem.T("execution.last"), string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath) ? "none" : JavaAgentSessionState.LastAppliedAssetPath);
            EditorGUILayout.EndVertical();
        }

        private void DrawResponseCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationSystem.T("response.title"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _showRawResponse = GUILayout.Toggle(_showRawResponse, LocalizationSystem.T("response.raw"), EditorStyles.miniButton, GUILayout.Width(92f));
            EditorGUILayout.EndHorizontal();

            _responseScroll = EditorGUILayout.BeginScrollView(_responseScroll, GUILayout.ExpandHeight(true));
            if (_showRawResponse)
            {
                EditorGUILayout.TextArea(JavaAgentSessionState.Response, GUILayout.ExpandHeight(true));
            }
            else
            {
                MarkdownRenderer.Draw(JavaAgentSessionState.Response);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawIssuesCard()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(180f));
            EditorGUILayout.LabelField(LocalizationSystem.T("issues.title"), EditorStyles.boldLabel);

            if (JavaAgentSessionState.LastDetectedIssues.Length == 0)
            {
                EditorGUILayout.HelpBox(LocalizationSystem.T("issues.empty"), MessageType.None);
            }
            else
            {
                _issuesScroll = EditorGUILayout.BeginScrollView(_issuesScroll, GUILayout.ExpandHeight(true));
                foreach (var issue in JavaAgentSessionState.LastDetectedIssues)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"{issue.severity} / {issue.category}");
                    EditorGUILayout.LabelField(issue.target);
                    EditorGUILayout.SelectableLabel(issue.detail ?? string.Empty, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(36f));
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHistoryCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(LocalizationSystem.T("history.title"), EditorStyles.boldLabel);

            if (JavaAgentSessionState.History.Count == 0)
            {
                EditorGUILayout.HelpBox(LocalizationSystem.T("history.empty"), MessageType.None);
            }
            else
            {
                _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
                foreach (var item in JavaAgentSessionState.History)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"{item.timestampUtc} [{item.status}]");
                    EditorGUILayout.LabelField($"{item.providerName} / {item.modelName} / {item.mode}");
                    EditorGUILayout.SelectableLabel(item.promptPreview ?? string.Empty, EditorStyles.textArea, GUILayout.MinHeight(34f));
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private string GetNodeDescription(JavaAgentSessionState.WorkflowNode node)
        {
            bool chinese = LocalizationSystem.CurrentLanguage == JavaAgentLanguage.Chinese;
            switch (node)
            {
                case JavaAgentSessionState.WorkflowNode.Skill:
                    return chinese ? "技能节点决定 Agent 偏向 Shader、Material、Function、Validation、Scene 或 Project 任务。" : "Skill profiles steer the agent toward shader, material, function, validation, scene, or project work.";
                case JavaAgentSessionState.WorkflowNode.Reference:
                    return chinese ? "参考节点读取本地文档或 URL，用于课程文档、教程、接口说明和实现约束。" : "Reference inputs let the agent read local docs or URLs before implementation.";
                case JavaAgentSessionState.WorkflowNode.Inspect:
                    return chinese ? "检查节点收集项目文件、选中资源、场景和编译状态，避免凭空生成。" : "Inspect gathers project files, selected assets, scene context, and validation state.";
                case JavaAgentSessionState.WorkflowNode.Approve:
                    return chinese ? "审批节点展示待写入文件和 diff，人工或安全自动审批后才会改项目。" : "Approve reviews generated proposals before any file write happens.";
                case JavaAgentSessionState.WorkflowNode.Apply:
                    return chinese ? "应用节点执行已审批写入，记录快照，并支持定位、打开和回滚。" : "Apply writes approved changes, records snapshots, and supports focus, open, and rollback.";
                case JavaAgentSessionState.WorkflowNode.Repair:
                    return chinese ? "修复节点把 Unity 编译错误反馈给 Agent，驱动自动修复和再次审批。" : "Repair feeds Unity compiler diagnostics back into the agent.";
                default:
                    return string.Empty;
            }
        }

        private string GetNodeTitle(JavaAgentSessionState.WorkflowNode node)
        {
            switch (node)
            {
                case JavaAgentSessionState.WorkflowNode.Skill:
                    return LocalizationSystem.T("node.skill");
                case JavaAgentSessionState.WorkflowNode.Reference:
                    return LocalizationSystem.T("node.reference");
                case JavaAgentSessionState.WorkflowNode.Inspect:
                    return LocalizationSystem.T("node.inspect");
                case JavaAgentSessionState.WorkflowNode.Approve:
                    return LocalizationSystem.T("node.approve");
                case JavaAgentSessionState.WorkflowNode.Apply:
                    return LocalizationSystem.T("node.apply");
                case JavaAgentSessionState.WorkflowNode.Repair:
                    return LocalizationSystem.T("node.repair");
                default:
                    return node.ToString();
            }
        }

        private void FocusFirstReference()
        {
            var firstReference = JavaAgentSessionState.ReferenceInputsText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(firstReference))
            {
                return;
            }

            JavaAgentSessionState.FocusTarget(firstReference, true);
        }

        private void FocusCurrentlySelectedAsset()
        {
            var firstSelectedAsset = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

            if (string.IsNullOrWhiteSpace(firstSelectedAsset))
            {
                return;
            }

            JavaAgentSessionState.FocusTarget(firstSelectedAsset, true);
        }
    }
}
