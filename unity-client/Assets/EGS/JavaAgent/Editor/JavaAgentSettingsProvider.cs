using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal static class JavaAgentSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            var provider = new SettingsProvider("Project/EGS Java Agent", SettingsScope.Project)
            {
                label = "EGS Java Agent",
                guiHandler = _ =>
                {
                    var settings = JavaAgentSettings.instance;
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.LabelField("Java Agent Endpoint", EditorStyles.boldLabel);
                    settings.endpoint = EditorGUILayout.TextField("Endpoint", settings.endpoint);
                    settings.sessionId = EditorGUILayout.TextField("Session ID", settings.sessionId);
                    settings.preferredMode = EditorGUILayout.TextField("Default Mode", settings.preferredMode);
                    settings.provider = EditorGUILayout.TextField("Provider", settings.provider);
                    settings.gateway = EditorGUILayout.TextField("Gateway", settings.gateway);
                    settings.model = EditorGUILayout.TextField("Model", settings.model);
                    settings.useEnvironmentToken = EditorGUILayout.Toggle("Use Environment Token", settings.useEnvironmentToken);
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Approval And Debug Automation", EditorStyles.boldLabel);
                    settings.autoApproveCreateFiles = EditorGUILayout.Toggle("Auto Approve Create Files", settings.autoApproveCreateFiles);
                    settings.autoRepairOnCompileError = EditorGUILayout.Toggle("Auto Repair On Compile Error", settings.autoRepairOnCompileError);
                    settings.maxAutoRepairAttempts = EditorGUILayout.IntSlider("Max Auto Repair Attempts", settings.maxAutoRepairAttempts, 1, 5);
                    settings.autoAttachLastAppliedScript = EditorGUILayout.Toggle("Auto Attach Applied Script", settings.autoAttachLastAppliedScript);
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.LabelField("Local Agent Launch", EditorStyles.boldLabel);
                    settings.localJavaCommand = EditorGUILayout.TextField("Java Command", settings.localJavaCommand);
                    settings.localJavaWorkingDirectory = EditorGUILayout.TextField("Working Directory", settings.localJavaWorkingDirectory);
                    settings.localJavaClasspath = EditorGUILayout.TextField("Classpath", settings.localJavaClasspath);
                    settings.localJavaMainClass = EditorGUILayout.TextField("Main Class", settings.localJavaMainClass);

                    EditorGUILayout.HelpBox(
                        "Recommended: keep tokens out of Unity assets and let the Java service read API keys from environment variables. Auto approval only applies to new file creation proposals under Assets/.",
                        MessageType.Info
                    );

                    if (GUILayout.Button("Use Recommended DeepSeek + LangChain4j Defaults"))
                    {
                        settings.ApplyRecommendedDefaults();
                    }

                    if (GUILayout.Button("Save"))
                    {
                        settings.SaveSettings();
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        settings.SaveSettings();
                    }
                }
            };

            return provider;
        }
    }
}
