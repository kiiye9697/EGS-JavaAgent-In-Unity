using System;
using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal static class MarkdownRenderer
    {
        internal static void Draw(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                EditorGUILayout.HelpBox("No response yet.", MessageType.None);
                return;
            }

            bool inCodeBlock = false;
            string codeBuffer = string.Empty;
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine ?? string.Empty;
                string trimmed = line.Trim();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCodeBlock)
                    {
                        DrawCodeBlock(codeBuffer.TrimEnd('\n'));
                        codeBuffer = string.Empty;
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                    }

                    continue;
                }

                if (inCodeBlock)
                {
                    codeBuffer += line + "\n";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    EditorGUILayout.Space(4f);
                    continue;
                }

                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    DrawSection(trimmed.Substring(3));
                    continue;
                }

                if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                {
                    DrawTitle(trimmed.Substring(2));
                    continue;
                }

                if (trimmed.EndsWith(":", StringComparison.Ordinal) && trimmed.Length < 48)
                {
                    DrawSection(trimmed.TrimEnd(':'));
                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    DrawBullet(trimmed.Substring(2));
                    continue;
                }

                DrawParagraph(line);
            }

            if (inCodeBlock && !string.IsNullOrEmpty(codeBuffer))
            {
                DrawCodeBlock(codeBuffer.TrimEnd('\n'));
            }
        }

        private static void DrawTitle(string text)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        private static void DrawSection(string text)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
        }

        private static void DrawBullet(string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("•", GUILayout.Width(14f));
            EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(18f));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawParagraph(string text)
        {
            EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(18f));
        }

        private static void DrawCodeBlock(string code)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            EditorGUILayout.TextArea(code, EditorStyles.textArea, GUILayout.MinHeight(Mathf.Clamp(code.Split('\n').Length * 18f, 54f, 260f)));
            GUI.backgroundColor = previous;
        }
    }
}
