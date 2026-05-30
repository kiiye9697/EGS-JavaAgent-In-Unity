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

        [MenuItem("Window/EGS Java Agent/Workspace")]
        private static void OpenWorkspace()
        {
            var window = GetWindow<JavaAgentWindow>("Java Agent Workspace");
            window.minSize = new Vector2(760f, 520f);
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

            EditorGUILayout.BeginHorizontal();
            DrawWorkspaceColumn();
            DrawInsightColumn();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("EGS Java Agent Workspace", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.contentColor = JavaAgentSessionState.AgentHealthKnown
                ? (JavaAgentSessionState.AgentHealthy ? Color.green : new Color(1f, 0.75f, 0.2f))
                : Color.white;
            GUILayout.Label("Agent: " + JavaAgentSessionState.AgentHealthStatus, EditorStyles.miniLabel);
            GUI.contentColor = Color.white;

            if (GUILayout.Button("Check Agent", EditorStyles.toolbarButton, GUILayout.Width(85f)))
            {
                _ = JavaAgentSessionState.RefreshAgentHealthAsync();
            }

            if (GUILayout.Button("Start Agent", EditorStyles.toolbarButton, GUILayout.Width(85f)))
            {
                _ = JavaAgentSessionState.EnsureAgentRunningAsync();
            }

            if (GUILayout.Button("Approval Queue", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                JavaAgentApprovalWindow.OpenWindow();
            }

            if (GUILayout.Button("Debug Console", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                JavaAgentDebugWindow.OpenWindow();
            }

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                SettingsService.OpenProjectSettings("Project/EGS Java Agent");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWorkspaceColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.48f));
            DrawStatusCard();
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
            DrawImplementedTargetsCard();
            DrawIssuesCard();
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusCard()
        {
            var snapshot = JavaAgentSessionState.CurrentCompileSnapshot;
            var diagnostics = JavaAgentSessionState.LastDiagnostics;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Live Workflow", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", JavaAgentSessionState.LastWorkflowStatus);
            EditorGUILayout.LabelField("Skill Profile", JavaAgentSkillCatalog.GetLabel(JavaAgentSessionState.SkillProfile));
            EditorGUILayout.LabelField("Selection", JavaAgentSessionState.BuildSelectionSummary());
            EditorGUILayout.LabelField("Pending Approvals", JavaAgentSessionState.PendingApprovals.Count.ToString());
            EditorGUILayout.LabelField("Rollback Records", JavaAgentSessionState.AppliedChanges.Count.ToString());
            EditorGUILayout.LabelField("Compile State", $"{snapshot.status} | errors={snapshot.errorCount} warnings={snapshot.warningCount}");

            if (diagnostics != null)
            {
                EditorGUILayout.LabelField("Runtime", $"{diagnostics.providerName} / {diagnostics.gatewayKind} / {diagnostics.effectiveModelName}");
            }

            DrawPipeline(snapshot);
            EditorGUILayout.EndVertical();
        }

        private void DrawPipeline(CompileDiagnosticsTracker.CompileSnapshot snapshot)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Node Flow", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Skill, "Skill", JavaAgentSkillCatalog.GetLabel(JavaAgentSessionState.SkillProfile), JavaAgentSessionState.SkillProfile != JavaAgentSkillProfile.GeneralAgent, true);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Reference, "Reference", string.IsNullOrWhiteSpace(JavaAgentSessionState.ReferenceInputsText) ? "none" : "attached", !string.IsNullOrWhiteSpace(JavaAgentSessionState.ReferenceInputsText), true);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Inspect, "Inspect", "tool reads", JavaAgentSessionState.LastActionExecutionResults.Length > 0, JavaAgentSessionState.IsBusy);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Approve, "Approve", JavaAgentSessionState.PendingApprovals.Count.ToString() + " queued", JavaAgentSessionState.PendingApprovals.Count > 0, false);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Apply, "Apply", string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath) ? "none" : "written", !string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath), false);
            DrawNodeCard(JavaAgentSessionState.WorkflowNode.Repair, "Repair", snapshot.errorCount > 0 ? "errors detected" : "stable", JavaAgentSessionState.AutoRepairAttempts > 0, snapshot.errorCount > 0);
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
            if (GUILayout.Toggle(isSelected, isSelected ? "Selected" : "Select", "Button"))
            {
                JavaAgentSessionState.SelectedWorkflowNode = node;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedNodeCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
            var node = JavaAgentSessionState.SelectedWorkflowNode;
            EditorGUILayout.LabelField(node.ToString(), EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(GetNodeDescription(node), EditorStyles.wordWrappedMiniLabel);

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

        private void DrawImplementedTargetsCard()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(190f));
            EditorGUILayout.LabelField("Implemented Targets", EditorStyles.boldLabel);

            if (JavaAgentSessionState.AppliedChanges.Count == 0)
            {
                EditorGUILayout.HelpBox("No applied targets yet. After an approved write, you can focus or open the implemented asset from here.", MessageType.None);
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

        private void DrawPromptCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Request Composer", EditorStyles.boldLabel);
            JavaAgentSessionState.Mode = (AgentMode)EditorGUILayout.EnumPopup("Mode", JavaAgentSessionState.Mode);
            JavaAgentSessionState.SkillProfile = (JavaAgentSkillProfile)EditorGUILayout.EnumPopup("Built-in Skill", JavaAgentSessionState.SkillProfile);
            EditorGUILayout.HelpBox(JavaAgentSkillCatalog.GetDescription(JavaAgentSessionState.SkillProfile), MessageType.None);

            _promptScroll = EditorGUILayout.BeginScrollView(_promptScroll, GUILayout.MinHeight(180f));
            var newPrompt = EditorGUILayout.TextArea(JavaAgentSessionState.Prompt, GUILayout.ExpandHeight(true));
            if (!string.Equals(newPrompt, JavaAgentSessionState.Prompt, StringComparison.Ordinal))
            {
                JavaAgentSessionState.Prompt = newPrompt;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Reference Inputs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One local file path or URL per line. The Java agent will read these references before implementing shader or function requests when relevant.",
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
            EditorGUILayout.LabelField("Execution Controls", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Built-in skills replace MCP-only abilities here: they bias the agent toward shader, material, function, repair, or logic-cleanup work while still using the same approval and repair loop.",
                MessageType.None
            );

            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
            if (GUILayout.Button("Send To Java Agent", GUILayout.Height(28f)))
            {
                _ = JavaAgentSessionState.SendPromptAsync();
            }

            if (GUILayout.Button("Open Approval Queue", GUILayout.Height(24f)))
            {
                JavaAgentApprovalWindow.OpenWindow();
            }

            if (GUILayout.Button("Trigger Repair From Compiler Errors", GUILayout.Height(24f)))
            {
                _ = JavaAgentSessionState.RepairFromCompilerErrorsAsync(false);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Last Applied Asset", string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath) ? "none" : JavaAgentSessionState.LastAppliedAssetPath);
            EditorGUILayout.EndVertical();
        }

        private void DrawResponseCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Agent Transcript", EditorStyles.boldLabel);
            _responseScroll = EditorGUILayout.BeginScrollView(_responseScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(JavaAgentSessionState.Response, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawIssuesCard()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Height(180f));
            EditorGUILayout.LabelField("Latest Issues", EditorStyles.boldLabel);

            if (JavaAgentSessionState.LastDetectedIssues.Length == 0)
            {
                EditorGUILayout.HelpBox("No lightweight project issues were reported in the latest run.", MessageType.None);
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
            EditorGUILayout.LabelField("Recent Requests", EditorStyles.boldLabel);

            if (JavaAgentSessionState.History.Count == 0)
            {
                EditorGUILayout.HelpBox("No request history yet.", MessageType.None);
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
            switch (node)
            {
                case JavaAgentSessionState.WorkflowNode.Skill:
                    return "Built-in skill profiles steer the agent toward shader, function, repair, or logic-cleanup work without requiring MCP.";
                case JavaAgentSessionState.WorkflowNode.Reference:
                    return "Reference inputs let the agent read local docs or URLs before implementation. Useful for specs, tutorials, and assignment files.";
                case JavaAgentSessionState.WorkflowNode.Inspect:
                    return "Inspect is the evidence-gathering phase where the agent reads project files, selected assets, and validation context.";
                case JavaAgentSessionState.WorkflowNode.Approve:
                    return "Approve is where generated proposals are reviewed in the Approval Queue before any file write happens.";
                case JavaAgentSessionState.WorkflowNode.Apply:
                    return "Apply records the implemented asset and lets you focus or open the latest generated script or shader.";
                case JavaAgentSessionState.WorkflowNode.Repair:
                    return "Repair feeds compiler diagnostics back into the agent and keeps iteration grounded in real Unity errors.";
                default:
                    return string.Empty;
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
