using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal static class JavaAgentSettingsProvider
    {
        private static string _providerApiKeyBuffer;

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/EGS Java Agent", SettingsScope.Project)
            {
                label = LocalizationSystem.T("settings.title"),
                guiHandler = _ => DrawSettingsGui()
            };
        }

        private static void DrawSettingsGui()
        {
            var settings = JavaAgentSettings.instance;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField(LocalizationSystem.T("settings.title"), EditorStyles.boldLabel);
            settings.endpoint = EditorGUILayout.TextField(LocalizationSystem.T("settings.endpoint"), settings.endpoint);
            settings.sessionId = EditorGUILayout.TextField(LocalizationSystem.T("settings.session"), settings.sessionId);
            settings.preferredMode = EditorGUILayout.TextField(LocalizationSystem.T("settings.mode"), settings.preferredMode);
            settings.provider = EditorGUILayout.TextField(LocalizationSystem.T("settings.provider"), settings.provider);
            settings.gateway = EditorGUILayout.TextField(LocalizationSystem.T("settings.gateway"), settings.gateway);
            settings.model = EditorGUILayout.TextField(LocalizationSystem.T("settings.model"), settings.model);
            settings.useEnvironmentToken = EditorGUILayout.Toggle(LocalizationSystem.T("settings.env"), settings.useEnvironmentToken);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(LocalizationSystem.T("settings.token"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(LocalizationSystem.T("settings.envName"), settings.ProviderKeyEnvironmentName);
            if (_providerApiKeyBuffer == null)
            {
                _providerApiKeyBuffer = settings.GetLocalProviderApiKey();
            }

            _providerApiKeyBuffer = EditorGUILayout.PasswordField(LocalizationSystem.T("settings.localKey"), _providerApiKeyBuffer ?? string.Empty);
            EditorGUILayout.HelpBox(
                settings.HasConfiguredProviderApiKey()
                    ? LocalizationSystem.T("settings.keyOk")
                    : LocalizationSystem.T("settings.keyMissing"),
                settings.HasConfiguredProviderApiKey() ? MessageType.Info : MessageType.Warning
            );

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(LocalizationSystem.T("settings.saveKey")))
            {
                settings.SetLocalProviderApiKey(_providerApiKeyBuffer);
            }

            if (GUILayout.Button(LocalizationSystem.T("settings.clearKey")))
            {
                _providerApiKeyBuffer = string.Empty;
                settings.SetLocalProviderApiKey(string.Empty);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(LocalizationSystem.T("settings.automation"), EditorStyles.boldLabel);
            settings.autoApproveCreateFiles = EditorGUILayout.Toggle(LocalizationSystem.T("settings.autoApprove"), settings.autoApproveCreateFiles);
            settings.autoRepairOnCompileError = EditorGUILayout.Toggle(LocalizationSystem.T("settings.autoRepair"), settings.autoRepairOnCompileError);
            settings.maxAutoRepairAttempts = EditorGUILayout.IntSlider(LocalizationSystem.T("settings.maxRepair"), settings.maxAutoRepairAttempts, 1, 5);
            settings.autoAttachLastAppliedScript = EditorGUILayout.Toggle(LocalizationSystem.T("settings.autoAttach"), settings.autoAttachLastAppliedScript);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(LocalizationSystem.T("settings.launch"), EditorStyles.boldLabel);
            settings.localJavaCommand = EditorGUILayout.TextField(LocalizationSystem.T("settings.java"), settings.localJavaCommand);
            settings.localJavaWorkingDirectory = EditorGUILayout.TextField(LocalizationSystem.T("settings.workingDir"), settings.localJavaWorkingDirectory);
            settings.localJavaClasspath = EditorGUILayout.TextField(LocalizationSystem.T("settings.classpath"), settings.localJavaClasspath);
            settings.localJavaMainClass = EditorGUILayout.TextField(LocalizationSystem.T("settings.mainClass"), settings.localJavaMainClass);

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(LocalizationSystem.T("settings.defaults")))
            {
                settings.ApplyRecommendedDefaults();
            }

            if (GUILayout.Button(LocalizationSystem.T("settings.save")))
            {
                settings.SaveSettings();
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }
        }
    }
}
