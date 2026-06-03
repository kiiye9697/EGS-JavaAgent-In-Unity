using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal static class LocalJavaAgentController
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        internal static async Task<bool> IsHealthyAsync(string endpoint)
        {
            try
            {
                string healthEndpoint = BuildHealthEndpoint(endpoint);
                string response = await HttpClient.GetStringAsync(healthEndpoint);
                return response.IndexOf("\"success\" : true", StringComparison.OrdinalIgnoreCase) >= 0
                    || response.IndexOf("\"success\":true", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        internal static async Task<bool> RestartAsync(JavaAgentSettings settings)
        {
            StopLikelyAgentProcesses();
            await Task.Delay(1000);
            return Start(settings, out _);
        }

        internal static bool Start(JavaAgentSettings settings, out string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.localJavaCommand))
                {
                    message = "Java command is not configured.";
                    return false;
                }

                string javaCommand = ResolveProjectPath(settings.localJavaCommand);
                string workingDirectory = ResolveProjectPath(settings.localJavaWorkingDirectory);
                string classpath = ResolveWorkingDirectoryPath(workingDirectory, settings.localJavaClasspath);

                if (!File.Exists(javaCommand))
                {
                    message = "Java command was not found: " + javaCommand;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
                {
                    message = "Working directory was not found: " + workingDirectory;
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = javaCommand,
                    Arguments = $"-classpath \"{classpath}\" {settings.localJavaMainClass}",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.EnvironmentVariables["EGS_AGENT_PROVIDER"] = settings.provider;
                startInfo.EnvironmentVariables["EGS_AGENT_MODEL"] = settings.model;
                startInfo.EnvironmentVariables["EGS_AGENT_GATEWAY"] = settings.gateway;
                ApplyProviderApiKey(startInfo, settings);

                Process.Start(startInfo);
                message = "Local Java agent launch requested from bundled runtime.";
                return true;
            }
            catch (Exception exception)
            {
                message = "Failed to launch local Java agent: " + exception.Message;
                return false;
            }
        }

        private static void StopLikelyAgentProcesses()
        {
            foreach (var process in Process.GetProcessesByName("java"))
            {
                TryStopProcessIfAgent(process);
            }

            foreach (var process in Process.GetProcessesByName("javaw"))
            {
                TryStopProcessIfAgent(process);
            }
        }

        private static void TryStopProcessIfAgent(Process process)
        {
            try
            {
                string mainWindowTitle = process.MainWindowTitle ?? string.Empty;
                string processPath = string.Empty;
                try
                {
                    processPath = process.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    // Some processes do not allow module inspection; leave them alone.
                }

                bool looksLikeBundledAgent =
                    mainWindowTitle.IndexOf("EGS Java Agent", StringComparison.OrdinalIgnoreCase) >= 0
                    || processPath.Replace('\\', '/').IndexOf("/Assets/EGS/JavaAgent/Embedded/", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksLikeBundledAgent)
                {
                    return;
                }

                process.Kill();
            }
            catch
            {
                // Best effort only. If the process cannot be stopped, health/start will report the remaining state.
            }
        }

        private static void ApplyProviderApiKey(ProcessStartInfo startInfo, JavaAgentSettings settings)
        {
            string apiKey = settings.GetLocalProviderApiKey();
            if (string.IsNullOrWhiteSpace(apiKey) && settings.useEnvironmentToken)
            {
                apiKey = Environment.GetEnvironmentVariable(settings.ProviderKeyEnvironmentName);
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return;
            }

            startInfo.EnvironmentVariables[settings.ProviderKeyEnvironmentName] = apiKey;
            if (string.Equals(settings.provider, "glm", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.EnvironmentVariables["ZHIPU_API_KEY"] = apiKey;
            }
        }

        private static string BuildHealthEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return "http://localhost:8765/health";
            }

            if (endpoint.EndsWith("/v1/agent/execute", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint.Substring(0, endpoint.Length - "/v1/agent/execute".Length) + "/health";
            }

            return endpoint.TrimEnd('/') + "/health";
        }

        private static string ResolveProjectPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return configuredPath;
            }

            return Path.GetFullPath(Path.Combine(projectRoot, configuredPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ResolveWorkingDirectoryPath(string workingDirectory, string configuredClasspath)
        {
            if (string.IsNullOrWhiteSpace(configuredClasspath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(configuredClasspath))
            {
                return configuredClasspath;
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return configuredClasspath;
            }

            return Path.GetFullPath(Path.Combine(workingDirectory, configuredClasspath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
