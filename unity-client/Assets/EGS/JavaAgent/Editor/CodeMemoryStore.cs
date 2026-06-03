using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EGS.JavaAgent.Editor
{
    internal static class CodeMemoryStore
    {
        private const int MaxEntries = 60;

        private static readonly List<CodeMemoryEntry> Entries = new();
        private static bool _loaded;

        internal static IReadOnlyList<CodeMemoryEntry> RecentEntries
        {
            get
            {
                EnsureLoaded();
                return Entries;
            }
        }

        internal static void Add(CodeMemoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.target))
            {
                return;
            }

            EnsureLoaded();
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }

            Save();
        }

        internal static CodeMemoryEntry Find(string id)
        {
            EnsureLoaded();
            return Entries.FirstOrDefault(entry => string.Equals(entry.id, id, StringComparison.Ordinal));
        }

        internal static void Remove(string id)
        {
            EnsureLoaded();
            if (Entries.RemoveAll(entry => string.Equals(entry.id, id, StringComparison.Ordinal)) > 0)
            {
                Save();
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            Entries.Clear();
            string path = StorePath;
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var wrapper = JsonUtility.FromJson<CodeMemoryWrapper>(File.ReadAllText(path));
                if (wrapper?.entries != null)
                {
                    Entries.AddRange(wrapper.entries.Where(entry => entry != null));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to load EGS Java Agent code memory: " + exception.Message);
            }
        }

        private static void Save()
        {
            string path = StorePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.dataPath);
            var wrapper = new CodeMemoryWrapper { entries = Entries.ToArray() };
            File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
        }

        private static string StorePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Library", "EGSJavaAgent", "code-memory.json");
            }
        }

        [Serializable]
        private sealed class CodeMemoryWrapper
        {
            public CodeMemoryEntry[] entries = Array.Empty<CodeMemoryEntry>();
        }
    }

    [Serializable]
    internal sealed class CodeMemoryEntry
    {
        public string id;
        public string nodeId;
        public string request;
        public string target;
        public string actionType;
        public string timestampLocal;
        public bool existedBefore;
        public string beforeContent;
        public string afterContent;
        public string summary;
    }
}
