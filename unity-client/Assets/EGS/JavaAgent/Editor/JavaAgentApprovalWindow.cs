using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal sealed class JavaAgentApprovalWindow : EditorWindow
    {
        private Vector2 _queueScroll;
        private Vector2 _previewScroll;
        private bool _showDiffPreview = true;
        private string _selectedApprovalId;

        [MenuItem("Window/EGS Java Agent/Approval Queue")]
        internal static void OpenWindow()
        {
            var window = GetWindow<JavaAgentApprovalWindow>("Java Agent Approval");
            window.minSize = new Vector2(720f, 420f);
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
            DrawQueuePane();
            DrawPreviewPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            var settings = JavaAgentSettings.instance;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Approval Queue", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _showDiffPreview = GUILayout.Toggle(_showDiffPreview, "Diff View", EditorStyles.toolbarButton, GUILayout.Width(80f));
            settings.autoApproveCreateFiles = GUILayout.Toggle(settings.autoApproveCreateFiles, "Auto Approve Safe Creates", EditorStyles.toolbarButton, GUILayout.Width(170f));
            if (GUILayout.Button("Approve Safe Creates Now", EditorStyles.toolbarButton, GUILayout.Width(160f)))
            {
                _ = JavaAgentSessionState.ApproveAllSafeCreatesAsync();
            }

            if (GUILayout.Button("Reject All", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                JavaAgentSessionState.RejectAllApprovals();
            }

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }
        }

        private void DrawQueuePane()
        {
            var approvals = JavaAgentSessionState.PendingApprovals;

            EditorGUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.38f));
            EditorGUILayout.LabelField("Pending Proposals", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Count", approvals.Count.ToString());

            if (approvals.Count == 0)
            {
                EditorGUILayout.HelpBox("No pending file proposals. Send a request or let auto-repair produce a fix proposal.", MessageType.None);
            }
            else
            {
                EnsureSelectionStillExists(approvals);
                _queueScroll = EditorGUILayout.BeginScrollView(_queueScroll);
                foreach (var item in approvals)
                {
                    var isSelected = string.Equals(_selectedApprovalId, item.Id, StringComparison.Ordinal);
                    var buttonLabel = item.IsSafeCreateCandidate
                        ? $"[Safe Create] {item.Action.target}"
                        : item.Action.target;
                    if (GUILayout.Toggle(isSelected, buttonLabel, "Button"))
                    {
                        _selectedApprovalId = item.Id;
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewPane()
        {
            var selected = JavaAgentSessionState.PendingApprovals.FirstOrDefault(item => item.Id == _selectedApprovalId);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Proposal Review", EditorStyles.boldLabel);

            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a pending proposal to inspect its target, reason, and content preview.", MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Type", selected.Action.type);
            EditorGUILayout.LabelField("Target", selected.Action.target);
            EditorGUILayout.LabelField("Reason", string.IsNullOrWhiteSpace(selected.Action.reason) ? "none" : selected.Action.reason);
            EditorGUILayout.LabelField("Safety", selected.IsSafeCreateCandidate ? "Safe create candidate" : "Manual review recommended");

            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.ExpandHeight(true));
            var previewText = _showDiffPreview
                ? JavaAgentSessionState.BuildProposalDiffPreview(selected.Action.target, selected.Action.proposalPreview)
                : selected.Action.proposalPreview ?? string.Empty;
            EditorGUILayout.TextArea(previewText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(JavaAgentSessionState.IsBusy);
            if (GUILayout.Button("Approve And Apply", GUILayout.Height(28f)))
            {
                _ = JavaAgentSessionState.ApplyApprovalAsync(selected);
            }

            if (GUILayout.Button("Reject", GUILayout.Height(28f)))
            {
                JavaAgentSessionState.RejectApproval(selected.Id);
                _selectedApprovalId = null;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void EnsureSelectionStillExists(System.Collections.Generic.IReadOnlyList<JavaAgentSessionState.PendingApprovalItem> approvals)
        {
            if (!string.IsNullOrWhiteSpace(_selectedApprovalId) && approvals.Any(item => item.Id == _selectedApprovalId))
            {
                return;
            }

            _selectedApprovalId = approvals.Count > 0 ? approvals[0].Id : null;
        }
    }
}
