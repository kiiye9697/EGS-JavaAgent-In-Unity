using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal sealed class JavaAgentSettings : ScriptableSingleton<JavaAgentSettings>
    {
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
    }
}
