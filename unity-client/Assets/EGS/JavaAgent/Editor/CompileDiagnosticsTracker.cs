using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace EGS.JavaAgent.Editor
{
    [InitializeOnLoad]
    internal static class CompileDiagnosticsTracker
    {
        private const int MaxMessages = 40;
        private static readonly List<CompileMessage> Messages = new();
        private static DateTime _lastUpdatedUtc = DateTime.MinValue;
        private static bool _isCompiling;
        internal static event Action<CompileSnapshot> SnapshotChanged;

        static CompileDiagnosticsTracker()
        {
            _isCompiling = EditorApplication.isCompiling;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        internal static CompileSnapshot GetSnapshot()
        {
            int errorCount = Messages.Count(message => message.severity == "error");
            int warningCount = Messages.Count(message => message.severity == "warning");
            string status = _isCompiling
                ? "compiling"
                : errorCount > 0
                    ? "failed"
                    : "idle";

            return new CompileSnapshot(
                status,
                errorCount,
                warningCount,
                _lastUpdatedUtc == DateTime.MinValue ? string.Empty : _lastUpdatedUtc.ToString("O"),
                Messages.ToArray()
            );
        }

        private static void OnCompilationStarted(object _)
        {
            _isCompiling = true;
            Messages.Clear();
            _lastUpdatedUtc = DateTime.UtcNow;
            NotifySnapshotChanged();
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] compilerMessages)
        {
            foreach (CompilerMessage compilerMessage in compilerMessages)
            {
                string severity = compilerMessage.type switch
                {
                    CompilerMessageType.Error => "error",
                    CompilerMessageType.Warning => "warning",
                    _ => "info"
                };

                Messages.Add(new CompileMessage(
                    severity,
                    string.IsNullOrWhiteSpace(compilerMessage.file) ? assemblyPath : compilerMessage.file,
                    compilerMessage.line,
                    compilerMessage.message ?? string.Empty
                ));
            }

            if (Messages.Count > MaxMessages)
            {
                Messages.RemoveRange(0, Messages.Count - MaxMessages);
            }

            _lastUpdatedUtc = DateTime.UtcNow;
            NotifySnapshotChanged();
        }

        private static void OnCompilationFinished(object _)
        {
            _isCompiling = false;
            _lastUpdatedUtc = DateTime.UtcNow;
            NotifySnapshotChanged();
        }

        private static void NotifySnapshotChanged()
        {
            SnapshotChanged?.Invoke(GetSnapshot());
        }

        internal readonly struct CompileSnapshot
        {
            internal readonly string status;
            internal readonly int errorCount;
            internal readonly int warningCount;
            internal readonly string timestampUtc;
            internal readonly CompileMessage[] messages;

            internal CompileSnapshot(
                string status,
                int errorCount,
                int warningCount,
                string timestampUtc,
                CompileMessage[] messages
            )
            {
                this.status = status;
                this.errorCount = errorCount;
                this.warningCount = warningCount;
                this.timestampUtc = timestampUtc;
                this.messages = messages ?? Array.Empty<CompileMessage>();
            }
        }

        internal readonly struct CompileMessage
        {
            internal readonly string severity;
            internal readonly string file;
            internal readonly int line;
            internal readonly string message;

            internal CompileMessage(string severity, string file, int line, string message)
            {
                this.severity = severity;
                this.file = file;
                this.line = line;
                this.message = message;
            }

            internal string ToSummaryLine()
            {
                string location = string.IsNullOrWhiteSpace(file) ? "unknown" : file.Replace('\\', '/');
                return $"{severity.ToUpperInvariant()} | {location}:{line} | {message}";
            }
        }
    }
}
