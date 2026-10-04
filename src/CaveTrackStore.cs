using System;
using System.Collections.Generic;
using System.Text.Json;
using MelonLoader;
using ModData;

namespace CaveExplorer
{
    /// <summary>Everything recorded for one cave scene in one save game.</summary>
    public class CaveRecord
    {
        /// <summary>English display name of the cave.</summary>
        public string Name { get; set; } = "";

        /// <summary>Localization key of the cave name, used to show the name in the game language.</summary>
        public string NameLocId { get; set; } = "";

        /// <summary>Game hours played (TimeOfDay.GetHoursPlayedNotPaused) when the path was last extended.</summary>
        public float LastUpdatedHours { get; set; } = -1f;

        /// <summary>Entrance/exit positions as [x, z].</summary>
        public List<List<float>> Entrances { get; set; } = new List<List<float>>();

        /// <summary>Walked path; each segment is a flat list of x,z pairs. A new segment starts on every scene entry.</summary>
        public List<List<float>> Segments { get; set; } = new List<List<float>>();
    }

    /// <summary>
    /// Holds the recorded caves of the active save game. The data is stored inside the save game with ModData
    /// (Mods\ModData\&lt;slot&gt;.moddata, entry "CaveExplorer"): ModData knows which save is active, writes the data
    /// to disk when the game saves and deletes it together with the save. Layout: scene name -> CaveRecord.
    /// </summary>
    public class CaveTrackStore
    {
        /// <summary>Two consecutive points further apart than this are a teleport, not a walked step.</summary>
        public const float MaxStepMeters = 25f;

        /// <summary>Entrances closer than this are treated as the same entrance.</summary>
        private const float EntranceMergeMeters = 10f;

        private readonly ModDataManager _modData = new ModDataManager("CaveExplorer", false);
        private readonly object _lock = new object();
        private Dictionary<string, CaveRecord> _caves = new Dictionary<string, CaveRecord>();
        private string _loadedSave;
        private bool _dirty;

        /// <summary>Forgets the data of the current save (back in the main menu).</summary>
        public void Unload()
        {
            lock (_lock)
            {
                _caves = new Dictionary<string, CaveRecord>();
                _loadedSave = null;
                _dirty = false;
            }
        }

        // Loads the data of the active save from ModData the first time it is needed
        private void EnsureLoaded()
        {
            string saveName = Il2Cpp.SaveGameSystem.GetCurrentSaveName() ?? "";
            if (_loadedSave == saveName)
                return;

            _caves = new Dictionary<string, CaveRecord>();
            _loadedSave = saveName;
            _dirty = false;

            try
            {
                string json = _modData.Load();
                if (string.IsNullOrEmpty(json))
                    return;

                var caves = JsonSerializer.Deserialize<Dictionary<string, CaveRecord>>(json);
                if (caves != null)
                {
                    foreach (var cave in caves)
                        _caves[cave.Key] = Normalize(cave.Value);
                }
                RemoveJumps();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error reading cave maps of save {saveName}: {ex.Message}");
            }
        }

        private static CaveRecord Normalize(CaveRecord record)
        {
            record ??= new CaveRecord();
            record.Entrances ??= new List<List<float>>();
            record.Segments ??= new List<List<float>>();
            record.Name ??= "";
            record.NameLocId ??= "";
            return record;
        }

        public CaveRecord GetRecord(string sceneName)
        {
            lock (_lock)
            {
                EnsureLoaded();
                if (!_caves.TryGetValue(sceneName, out var record))
                {
                    record = new CaveRecord();
                    _caves[sceneName] = record;
                }
                return record;
            }
        }

        public List<float> StartSegment(CaveRecord record)
        {
            lock (_lock)
            {
                var segment = new List<float>();
                record.Segments.Add(segment);
                return segment;
            }
        }

        public void ClearSegment(List<float> segment)
        {
            lock (_lock)
            {
                segment.Clear();
                _dirty = true;
            }
        }

        public void AddPoint(CaveRecord record, List<float> segment, float x, float z, float hoursPlayed)
        {
            lock (_lock)
            {
                segment.Add(x);
                segment.Add(z);
                record.LastUpdatedHours = hoursPlayed;
                _dirty = true;
            }
        }

        public void AddEntrance(CaveRecord record, float x, float z)
        {
            lock (_lock)
            {
                foreach (var entrance in record.Entrances)
                {
                    float dx = entrance[0] - x;
                    float dz = entrance[1] - z;
                    if (dx * dx + dz * dz < EntranceMergeMeters * EntranceMergeMeters)
                        return;
                }

                record.Entrances.Add(new List<float> { x, z });
                _dirty = true;
            }
        }

        public void SetName(CaveRecord record, string name, string locId)
        {
            lock (_lock)
            {
                if (record.Name == name && record.NameLocId == locId)
                    return;

                record.Name = name;
                record.NameLocId = locId;
                _dirty = true;
            }
        }

        /// <summary>
        /// Hands the current data to ModData. ModData only keeps it in memory and writes it to disk
        /// when the game saves, so this is called right before every game save (see Patch_SaveGameSlots_WriteSlotToDisk).
        /// </summary>
        public void Commit()
        {
            lock (_lock)
            {
                if (!_dirty || _loadedSave == null)
                    return;

                try
                {
                    if (_modData.Save(WriteJson(_caves)))
                        _dirty = false;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error storing cave maps of save {_loadedSave}: {ex.Message}");
                }
            }
        }

        /// <summary>Segments with fewer points are dropped on load; they were recorded during loading screens.</summary>
        private const int MinSegmentPoints = 3;

        // Drops leading points that are further away from the next point than the player can walk
        // (position from the previous scene recorded right after a scene load), segments with too few points,
        // and entrances that are not near the remaining path.
        private void RemoveJumps()
        {
            foreach (var record in _caves.Values)
            {
                foreach (var segment in record.Segments)
                {
                    while (segment.Count >= 4)
                    {
                        float dx = segment[2] - segment[0];
                        float dz = segment[3] - segment[1];
                        if (dx * dx + dz * dz <= MaxStepMeters * MaxStepMeters)
                            break;

                        segment.RemoveRange(0, 2);
                        _dirty = true;
                    }
                }

                if (record.Segments.RemoveAll(IsTooShort) > 0)
                    _dirty = true;

                if (record.Entrances.RemoveAll(entrance => !IsNearPath(record, entrance)) > 0)
                    _dirty = true;
            }
        }

        private static bool IsTooShort(List<float> segment)
        {
            return segment.Count < MinSegmentPoints * 2;
        }

        private static bool IsNearPath(CaveRecord record, List<float> entrance)
        {
            foreach (var segment in record.Segments)
            {
                for (int i = 0; i + 1 < segment.Count; i += 2)
                {
                    float dx = segment[i] - entrance[0];
                    float dz = segment[i + 1] - entrance[1];
                    if (dx * dx + dz * dz <= EntranceMergeMeters * EntranceMergeMeters)
                        return true;
                }
            }
            return false;
        }

        // Readable layout: one level per cave and field; each coordinate list stays on one line
        private static string WriteJson(Dictionary<string, CaveRecord> caves)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            int caveIndex = 0;
            foreach (var cave in caves)
            {
                CaveRecord record = cave.Value;
                sb.Append("  ").Append(Quote(cave.Key)).Append(": {\n");
                sb.Append("    \"Name\": ").Append(Quote(record.Name)).Append(",\n");
                sb.Append("    \"NameLocId\": ").Append(Quote(record.NameLocId)).Append(",\n");
                sb.Append("    \"LastUpdatedHours\": ").Append(FormatNumber(record.LastUpdatedHours, "0.###")).Append(",\n");
                AppendNumberLists(sb, "Entrances", record.Entrances, true);
                AppendNumberLists(sb, "Segments", record.Segments, false);
                sb.Append("  }").Append(++caveIndex < caves.Count ? "," : "").Append('\n');
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        private static void AppendNumberLists(System.Text.StringBuilder sb, string name, List<List<float>> lists, bool more)
        {
            sb.Append("    ").Append(Quote(name)).Append(": [");
            for (int i = 0; i < lists.Count; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n").Append("      [");
                for (int j = 0; j < lists[i].Count; j++)
                {
                    if (j > 0)
                        sb.Append(", ");
                    sb.Append(FormatNumber(lists[i][j], "0.##"));
                }
                sb.Append(']');
            }
            sb.Append(lists.Count > 0 ? "\n    ]" : "]").Append(more ? "," : "").Append('\n');
        }

        private static string FormatNumber(float value, string format)
        {
            return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        // Umlauts and dashes stay readable instead of being escaped
        private static readonly JsonSerializerOptions QuoteOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static string Quote(string text)
        {
            return JsonSerializer.Serialize(text ?? "", QuoteOptions);
        }
    }
}
