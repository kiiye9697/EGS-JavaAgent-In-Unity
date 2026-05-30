using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal sealed class JavaAgentDebugWindow : EditorWindow
    {
        private Vector2 _rollbackScroll;
        private Vector2 _compileScroll;
        private Vector2 _logScroll;
        private Vector2 _actionScroll;

        [MenuItem("Window/EGS Java Agent/Debug Console")]
        internal static void OpenWindow()
        {
            var window = GetWindow<JavaAgentDebugWindow>("Java Agent Debug");
            window.minSize = new Vector2(760f, 420f);
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
            DrawCompileCard();
            DrawActionResultsCard();
            DrawRollbackCard();
            DrawEventLogCard();
        }

        private void DrawToolbar()
        {
            var settings = JavaAgentSettings.instance;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Debug Console", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            settings.autoRepairOnCompileError = GUILayout.Toggle(settings.autoRepairOnCompileError, "Auto Repair", EditorStyles.toolbarButton, GUILayout.Width(100f));
            settings.autoAttachLastAppliedScript = GUILayout.Toggle(settings.autoAttachLastAppliedScript, "Auto Attach", EditorStyles.toolbarButton, GUILayout.Width(100f));

            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.AppliedChanges.Count == 0);
            if (GUILayout.Button("Rollback Last", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                JavaAgentSessionState.RollbackLatestChanges(1);
            }

            if (GUILayout.Button("Rollback Last 3", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                JavaAgentSessionState.RollbackLatestChanges(3);
            }

            if (GUILayout.Button("Rollback All", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                JavaAgentSessionState.RollbackAllAppliedChanges();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Refresh Assets", EditorStyles.toolbarButton, GUILayout.Width(95f)))
            {
                JavaAgentSessionState.RefreshAssetDatabase();
            }

            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
            if (GUILayout.Button("Repair Now", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                _ = JavaAgentSessionState.RepairFromCompilerErrorsAsync(false);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!JavaAgentSessionState.CanAttachLastAppliedScript());
            if (GUILayout.Button("Attach Script", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                JavaAgentSessionState.AttachLastAppliedScriptToSelection();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }
        }

        private void DrawCompileCard()
        {
            var snapshot = JavaAgentSessionState.CurrentCompileSnapshot;
            var settings = JavaAgentSettings.instance;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Compile And Repair", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", snapshot.status);
            EditorGUILayout.LabelField("Errors", snapshot.errorCount.ToString());
            EditorGUILayout.LabelField("Warnings", snapshot.warningCount.ToString());
            EditorGUILayout.LabelField("Updated", string.IsNullOrWhiteSpace(snapshot.timestampUtc) ? "none" : snapshot.timestampUtc);
            EditorGUILayout.LabelField("Auto Repair Attempts", $"{JavaAgentSessionState.AutoRepairAttempts}/{settings.maxAutoRepairAttempts}");
            EditorGUILayout.LabelField("Last Applied Asset", string.IsNullOrWhiteSpace(JavaAgentSessionState.LastAppliedAssetPath) ? "none" : JavaAgentSessionState.LastAppliedAssetPath);

            _compileScroll = EditorGUILayout.BeginScrollView(_compileScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
            if (snapshot.messages.Length == 0)
            {
                EditorGUILayout.HelpBox("No compiler messages are currently tracked.", MessageType.None);
            }
            else
            {
                foreach (var message in snapshot.messages)
                {
                    EditorGUILayout.SelectableLabel(message.ToSummaryLine(), EditorStyles.textArea, GUILayout.MinHeight(32f));
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionResultsCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Latest Tool Actions", EditorStyles.boldLabel);

            if (JavaAgentSessionState.LastActionExecutionResults.Length == 0)
            {
                EditorGUILayout.HelpBox("No tool actions have been recorded yet.", MessageType.None);
            }
            else
            {
                _actionScroll = EditorGUILayout.BeginScrollView(_actionScroll, GUILayout.MinHeight(100f), GUILayout.MaxHeight(180f));
                foreach (var result in JavaAgentSessionState.LastActionExecutionResults)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"{result.type} -> {result.target} [{(result.success ? "ok" : "failed")}]");
                    EditorGUILayout.SelectableLabel(result.output ?? string.Empty, EditorStyles.textArea, GUILayout.MinHeight(42f));
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEventLogCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Event Log", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(120f), GUILayout.ExpandHeight(true));
            foreach (var entry in JavaAgentSessionState.Logs)
            {
                EditorGUILayout.SelectableLabel(entry, EditorStyles.textArea, GUILayout.MinHeight(20f));
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRollbackCard()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Rollback History", EditorStyles.boldLabel);

            if (JavaAgentSessionState.AppliedChanges.Count == 0)
            {
                EditorGUILayout.HelpBox("No agent-applied file snapshots are available yet. After an approved write, you can roll back that file from here.", MessageType.None);
            }
            else
            {
                _rollbackScroll = EditorGUILayout.BeginScrollView(_rollbackScroll, GUILayout.MinHeight(100f), GUILayout.MaxHeight(180f));
                foreach (var item in JavaAgentSessionState.AppliedChanges)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"{item.TimestampLocal} | {item.ActionType}");
                    EditorGUILayout.LabelField(item.Target);
                    EditorGUILayout.LabelField(item.ExistedBefore ? "Restore previous file contents" : "Delete created file", EditorStyles.miniLabel);
                    if (GUILayout.Button("Rollback This Change", GUILayout.Height(22f)))
                    {
                        JavaAgentSessionState.RollbackAppliedChange(item.Id);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }
    }
}
