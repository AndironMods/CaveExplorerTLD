using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader;

namespace CaveExplorer
{
    /// <summary>
    /// Scenes that are never treated as cave, from {ModsDirectory}/CaveExplorerExclude.json:
    /// { "ExcludedScenes": ["MineConcentratorBuilding"] }
    /// The file is created with the default list if it does not exist (also after it was deleted)
    /// and read again whenever it changed. A file with errors is never overwritten; the last valid list stays in use.
    /// </summary>
    public class CaveExplorerExclusion
    {
        private static readonly string FilePath =
            Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "CaveExplorerExclude.json");

        private static readonly string[] DefaultScenes = { "MineConcentratorBuilding" };

        private static readonly JsonDocumentOptions ReadOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private HashSet<string> _excluded = new HashSet<string>(DefaultScenes, StringComparer.OrdinalIgnoreCase);
        private DateTime _lastWrite = DateTime.MinValue;

        public bool IsExcluded(string sceneName)
        {
            Reload();
            return _excluded.Contains(sceneName);
        }

        public void Reload()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, CreateDefaultContent());
                    MelonLogger.Msg($"Created {FilePath}");
                }

                DateTime lastWrite = File.GetLastWriteTimeUtc(FilePath);
                if (lastWrite == _lastWrite)
                    return;
                _lastWrite = lastWrite;

                var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FilePath), ReadOptions))
                {
                    if (doc.RootElement.TryGetProperty("ExcludedScenes", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in list.EnumerateArray())
                        {
                            string name = item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : null;
                            if (!string.IsNullOrEmpty(name))
                                excluded.Add(name);
                        }
                    }
                }

                _excluded = excluded;
                MelonLogger.Msg(excluded.Count > 0 ? $"Excluded scenes: {string.Join(", ", excluded)}" : "No excluded scenes");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading {FilePath}, still using: {string.Join(", ", _excluded)}. {ex.Message}");
            }
        }

        // Comments are allowed in this file (see ReadOptions)
        private static string CreateDefaultContent()
        {
            var quoted = new List<string>();
            foreach (string scene in DefaultScenes)
                quoted.Add("\"" + scene + "\"");

            return string.Join("\n", new[]
            {
                "// CaveExplorer - excluded places",
                "// Places listed in ExcludedScenes never get a cave map.",
                "//",
                "// How to add a place:",
                "// 1. Enter the place in the game and look at the MelonLoader console. It shows the scene name, e.g.",
                "//      [CaveExplorer] Entered cave MiningRegionMine (Langston Mine)",
                "// 2. Add the scene name (here: MiningRegionMine) in quotes, separated by a comma. Example with two caves:",
                "//      \"ExcludedScenes\": [\"MineConcentratorBuilding\", \"MiningRegionMine\"]",
                "// 3. Save the file. The change is used the next time you enter a place, no restart needed.",
                "//",
                "// An empty list [] excludes nothing. If this file is deleted, it is created again with the default list.",
                "{",
                "  \"ExcludedScenes\": [" + string.Join(", ", quoted) + "]",
                "}",
                ""
            });
        }
    }
}
