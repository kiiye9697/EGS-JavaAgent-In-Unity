using System;
using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal sealed class JavaAgentSettings : ScriptableSingleton<JavaAgentSettings>
    {
        private const string ApiKeyEditorPrefsPrefix = "EGS.JavaAgent.ProviderApiKey.";

        [SerializeField] internal string endpoint = "http://localhost:8765/v1/agent/execute";
        [SerializeField] internal string sessionId = "unity-default-session";
        [SerializeField] internal string preferredMode = "agent";
        [SerializeField] internal string provider = "deepseek";
        [SerializeField] internal string gateway = "langchain4j";
        [SerializeField] internal string model = "deepseek-v4-flash";
        [SerializeField] internal bool useEnvironmentToken = true;
        [SerializeField] internal bool autoApproveCreateFiles;
        [SerializeField] internal bool autoRepairOnCompileError = true;
        [SerializeField] internal int maxAutoRepairAttempts = 2;
        [SerializeField] internal bool autoAttachLastAppliedScript = true;
        [SerializeField] internal string localJavaCommand = @"Assets/EGS/JavaAgent/Embedded/jdk/bin/java.exe";
        [SerializeField] internal string localJavaWorkingDirectory = @"Assets/EGS/JavaAgent/Embedded/egs-java-agent";
        [SerializeField] internal string localJavaClasspath = @"lib\*";
        [SerializeField] internal string localJavaMainClass = "com.egs.javaagent.JavaAgentApplication";

        internal string ProviderKeyEnvironmentName => GetProviderKeyEnvironmentName(provider);

        internal bool HasLocalProviderApiKey()
        {
            return !string.IsNullOrWhiteSpace(GetLocalProviderApiKey());
        }

        internal bool HasConfiguredProviderApiKey()
        {
            if (HasLocalProviderApiKey())
            {
                return true;
            }

            return useEnvironmentToken
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ProviderKeyEnvironmentName));
        }

        internal string GetLocalProviderApiKey()
        {
            return EditorPrefs.GetString(GetApiKeyPrefsKey(provider), string.Empty);
        }

        internal void SetLocalProviderApiKey(string apiKey)
        {
            string prefsKey = GetApiKeyPrefsKey(provider);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                EditorPrefs.DeleteKey(prefsKey);
                return;
            }

            EditorPrefs.SetString(prefsKey, apiKey.Trim());
        }

        internal void SaveSettings()
        {
            Save(true);
        }

        internal void ApplyRecommendedDefaults()
        {
            endpoint = "http://localhost:8765/v1/agent/execute";
            preferredMode = "agent";
            provider = "deepseek";
            gateway = "langchain4j";
            model = "deepseek-v4-flash";
            useEnvironmentToken = true;
            autoApproveCreateFiles = false;
            autoRepairOnCompileError = true;
            maxAutoRepairAttempts = 2;
            autoAttachLastAppliedScript = true;
            localJavaCommand = @"Assets/EGS/JavaAgent/Embedded/jdk/bin/java.exe";
            localJavaWorkingDirectory = @"Assets/EGS/JavaAgent/Embedded/egs-java-agent";
            localJavaClasspath = @"lib\*";
            localJavaMainClass = "com.egs.javaagent.JavaAgentApplication";
            Save(true);
        }

        internal static string GetProviderKeyEnvironmentName(string providerName)
        {
            switch ((providerName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "deepseek":
                    return "DEEPSEEK_API_KEY";
                case "glm":
                    return "GLM_API_KEY";
                default:
                    return "OPENAI_API_KEY";
            }
        }

        private static string GetApiKeyPrefsKey(string providerName)
        {
            string normalizedProvider = string.IsNullOrWhiteSpace(providerName)
                ? "openai"
                : providerName.Trim().ToLowerInvariant();
            return ApiKeyEditorPrefsPrefix + normalizedProvider;
        }
    }
}
