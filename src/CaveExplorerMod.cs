using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

[assembly: MelonInfo(typeof(CaveExplorer.CaveExplorerMod), "CaveExplorer", "1.0.0", "Andiron")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: MelonAdditionalDependencies("ModData")]

namespace CaveExplorer
{
    public class CaveExplorerMod : MelonMod
    {
        private const string EnglishLanguage = "English";

        internal static CaveExplorerMod Instance;

        private readonly CaveExplorerSettings _settings = new CaveExplorerSettings();
        private readonly CaveTrackStore _store = new CaveTrackStore();
        private readonly CaveExplorerExclusion _exclusion = new CaveExplorerExclusion();
        private readonly CaveMapRenderer _renderer = new CaveMapRenderer();
        private readonly CaveMapView _view = new CaveMapView();

        private string _currentSceneName = "";
        private string _lastRealSceneName = "";
        private string _displayName = "";
        private CaveRecord _record;
        private List<float> _currentSegment;
        private bool _markEntrance;
        private float _nextSampleTime;

        private bool _mapOpen;
        private bool _mapDirty;
        private int _lastToggleFrame = -1;
        private PlayerControlMode _restoreControlMode = PlayerControlMode.Normal;

        internal bool IsInCave { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            _settings.AddToModSettings("CaveExplorer");
            _exclusion.Reload();
        }

        /// <summary>Called right before the game writes its save, so ModData stores the latest map with it.</summary>
        internal void OnBeforeGameSave()
        {
            _store.Commit();
        }

        public override void OnUpdate()
        {
            UpdateScene();

            if (!IsInCave)
                return;

            // Fallback in case the game does not ask for the map key while the player is locked
            if (_mapOpen && (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape)))
                RequestClose();

            if (_mapOpen || Time.time < _nextSampleTime)
                return;
            _nextSampleTime = Time.time + _settings.SampleSeconds;

            // During loading screens the player is already moved to the next scene
            if (IsLoadingOrTeleporting())
                return;

            var player = GameManager.GetPlayerTransform();
            if (player == null)
                return;

            Vector3 position = player.position;
            int count = _currentSegment.Count;
            if (count >= 2)
            {
                float dx = position.x - _currentSegment[count - 2];
                float dz = position.z - _currentSegment[count - 1];
                float sqrDistance = dx * dx + dz * dz;
                // the path is drawn with the radius, so points closer than that add nothing
                if (sqrDistance < _settings.PathRadius * _settings.PathRadius)
                    return;

                // Right after a scene load the player still has the position from the previous scene.
                // Such a jump must not become part of the path.
                if (sqrDistance > CaveTrackStore.MaxStepMeters * CaveTrackStore.MaxStepMeters)
                {
                    if (count == 2)
                        _store.ClearSegment(_currentSegment);
                    else
                        _currentSegment = _store.StartSegment(_record);
                }
            }

            _store.AddPoint(_record, _currentSegment, position.x, position.z, GetHoursPlayed());
            _mapDirty = true;

            // The first point of a segment is only trusted once the next step is a normal walking step
            if (_markEntrance && _currentSegment.Count >= 4)
            {
                _store.AddEntrance(_record, _currentSegment[0], _currentSegment[1]);
                _markEntrance = false;
            }
        }

        public override void OnGUI()
        {
            if (!_mapOpen || !IsInCave)
                return;

            var player = GameManager.GetPlayerTransform();
            if (player == null)
                return;

            if (_mapDirty || _renderer.Texture == null)
            {
                _renderer.Render(_record, player.position, player.forward, _settings.PathRadius);
                _mapDirty = false;
            }

            ShowCursor();
            if (_view.Draw(_renderer, _record, _displayName, GetHoursPlayed()))
                CloseMap();
        }

        internal bool IsMapOpen => _mapOpen;

        // The game's own hide calls are blocked while the map is open (see Patch_InputManager_ShowCursor).
        // This only corrects the cursor if something else changed it, without toggling it every frame.
        public override void OnLateUpdate()
        {
            if (_mapOpen)
                ShowCursor();
        }

        private static void ShowCursor()
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        /// <summary>Called by the map key patch. Returns true if the key press was handled by this mod.</summary>
        internal bool OnMapKeyPressed()
        {
            if (!IsInCave)
                return false;

            // the game may ask for the key several times per frame
            if (_lastToggleFrame != Time.frameCount)
            {
                _lastToggleFrame = Time.frameCount;
                if (_mapOpen)
                    CloseMap();
                else
                    OpenMap();
            }

            return true;
        }

        /// <summary>Called by the escape key patch. Returns true if the key press was handled by this mod.</summary>
        internal bool OnEscapePressed()
        {
            // also swallow the key if the map was closed by the same key press earlier in this frame
            if (!_mapOpen)
                return _lastToggleFrame == Time.frameCount;

            RequestClose();
            return true;
        }

        private void RequestClose()
        {
            if (_lastToggleFrame == Time.frameCount)
                return;

            _lastToggleFrame = Time.frameCount;
            CloseMap();
        }

        private void OpenMap()
        {
            var playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager == null)
                return;

            // Do not interrupt climbing, placing, conversations etc.
            PlayerControlMode mode = playerManager.GetControlMode();
            if (mode != PlayerControlMode.Normal)
                return;

            _restoreControlMode = mode;
            playerManager.SetControlMode(PlayerControlMode.Locked);
            InputManager.ShowCursor(true);
            ShowCursor();

            _view.OnOpen();
            _mapOpen = true;
            _mapDirty = true;
        }

        private void CloseMap()
        {
            if (!_mapOpen)
                return;

            // must be false before ShowCursor(false), otherwise the patch blocks it
            _mapOpen = false;

            var playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager != null && playerManager.GetControlMode() == PlayerControlMode.Locked)
                playerManager.SetControlMode(_restoreControlMode);
            InputManager.ShowCursor(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void UpdateScene()
        {
            string sceneName = UnitySceneManager.GetActiveScene().name ?? "";
            if (sceneName == _currentSceneName)
                return;

            if (IsInCave)
                LeaveCave(sceneName);

            if (!string.IsNullOrEmpty(_currentSceneName) && !_currentSceneName.Equals("Empty", StringComparison.OrdinalIgnoreCase))
                _lastRealSceneName = _currentSceneName;

            _currentSceneName = sceneName;

            // the cave maps belong to the save game; ModData closes it in the main menu as well
            if (sceneName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase))
                _store.Unload();

            IsInCave = _settings.Enabled && IsCaveScene(sceneName) && !_exclusion.IsExcluded(sceneName);

            if (IsInCave)
                EnterCave(sceneName);
        }

        private void EnterCave(string sceneName)
        {
            _record = _store.GetRecord(sceneName);
            _currentSegment = _store.StartSegment(_record);
            _nextSampleTime = 0f;

            // When a save is loaded inside the cave the start position is not an entrance
            _markEntrance = !_lastRealSceneName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase);

            // Name: key set by the cave entrance used, else the key saved from an earlier visit, else the scene name.
            // The location text shown on entry (OnLocationReveal) can still correct it.
            _displayName = sceneName;
            if (!ApplyNameLocId(GetTransitionLocId()) && !ApplyNameLocId(_record.NameLocId))
                _store.SetName(_record, sceneName, "");

            MelonLogger.Msg($"Entered cave {sceneName} ({_displayName})");
        }

        /// <summary>Called by the location reveal patch with the text the game shows when entering a location.</summary>
        internal void OnLocationReveal(string text, string subText)
        {
            if (!IsInCave || string.IsNullOrEmpty(text))
                return;

            // A cave without an own name is shown as "Cave" with the region as sub text,
            // e.g. 'Höhle' / 'Stummflusstal'. Both together are used as name: "Höhle – Stummflusstal".
            string genericLocId = GetGenericLocId(text);
            if (genericLocId != null)
            {
                if (string.IsNullOrEmpty(subText))
                    return;

                string subLocId = FindLocIdForText(subText);
                if (subLocId == null || !ApplyNameLocId(genericLocId + LocIdSeparator + subLocId))
                    _displayName = text + NameSeparator + subText;
                return;
            }

            if (text != _displayName && !ApplyNameLocId(FindLocIdForText(text)))
                _displayName = text;
        }

        // Keys of names the game uses for all caves/mines without an own name
        private static readonly string[] GenericNameLocIds = { "GAMEPLAY_Cave", "GAMEPLAY_Mine" };

        // NameLocId may combine several keys, e.g. "GAMEPLAY_Cave|SCENENAME_RiverValley"
        private const char LocIdSeparator = '|';
        private const string NameSeparator = " – ";

        internal static bool IsGenericLocId(string locId)
        {
            return Array.IndexOf(GenericNameLocIds, locId) >= 0;
        }

        private static string GetGenericLocId(string text)
        {
            foreach (var locId in GenericNameLocIds)
            {
                try
                {
                    if (Localization.Exists(locId) && Localization.Get(locId) == text)
                        return locId;
                }
                catch
                {
                    // ignore and check the next key
                }
            }
            return null;
        }

        // Shows the name in the game language and stores the English name and the key(s) in the JSON.
        // A generic name alone ("Cave") is ignored; the scene name is shown instead.
        private bool ApplyNameLocId(string locId)
        {
            try
            {
                if (string.IsNullOrEmpty(locId) || IsGenericLocId(locId))
                    return false;

                var localized = new List<string>();
                var english = new List<string>();
                foreach (string part in locId.Split(LocIdSeparator))
                {
                    if (!Localization.Exists(part))
                        return false;

                    string text = Localization.Get(part);
                    if (string.IsNullOrEmpty(text))
                        return false;

                    string englishText = Localization.GetForLang(part, EnglishLanguage);
                    localized.Add(text);
                    english.Add(string.IsNullOrEmpty(englishText) ? text : englishText);
                }

                if (localized.Count == 1 && GetGenericLocId(localized[0]) != null)
                    return false;

                _displayName = string.Join(NameSeparator, localized);
                _store.SetName(_record, string.Join(NameSeparator, english), locId);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading cave name {locId}: {ex.Message}");
                return false;
            }
        }

        private static string GetTransitionLocId()
        {
            try { return GameManager.m_SceneTransitionData?.m_SceneLocationLocIDOverride; }
            catch { return null; }
        }

        private void LeaveCave(string nextSceneName)
        {
            CloseMap();

            // The last position is an exit, unless the player quits to the main menu
            if (!nextSceneName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase) && _currentSegment.Count >= 4)
            {
                int count = _currentSegment.Count;
                _store.AddEntrance(_record, _currentSegment[count - 2], _currentSegment[count - 1]);
            }

            _store.Commit();
            MelonLogger.Msg($"Left cave {_currentSceneName}");
        }

        private static bool IsCaveScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) ||
                sceneName.Contains("MainMenu", StringComparison.OrdinalIgnoreCase) ||
                sceneName.Equals("Empty", StringComparison.OrdinalIgnoreCase) ||
                sceneName.Equals("Boot", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!sceneName.Contains("Cave", StringComparison.OrdinalIgnoreCase) &&
                !sceneName.Contains("Mine", StringComparison.OrdinalIgnoreCase))
                return false;

            if (GameManager.m_Instance == null || GameManager.IsMainMenuActive())
                return false;

            try { return !GameManager.IsOutDoorsScene(sceneName); }
            catch { return false; }
        }

        private static bool IsLoadingOrTeleporting()
        {
            try
            {
                if (Il2Cpp.Utils.IsSceneTransition() || Il2Cpp.SceneManager.IsLoading())
                    return true;

                var playerManager = GameManager.GetPlayerManagerComponent();
                return playerManager != null && (playerManager.m_TeleportPending || playerManager.m_DoTeleportAfterSceneLoad);
            }
            catch
            {
                return false;
            }
        }

        private static float GetHoursPlayed()
        {
            var timeOfDay = GameManager.GetTimeOfDayComponent();
            return timeOfDay != null ? timeOfDay.GetHoursPlayedNotPaused() : 0f;
        }

        // The location reveal only gives the text in the current language. Find its localization key
        // by comparing all entries of the current string table.
        private static string FindLocIdForText(string text)
        {
            try
            {
                var table = Localization.s_CurrentLanguageStringTable;
                if (table == null)
                    return null;

                int count = table.GetNumEntries();
                for (int i = 0; i < count; i++)
                {
                    string key = table.GetKeyForEntry(i);
                    if (!string.IsNullOrEmpty(key) && Localization.Get(key) == text)
                        return key;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error finding localization key for '{text}': {ex.Message}");
            }

            return null;
        }
    }

    // ModData writes its data to disk in a postfix of this method; the map is handed over right before
    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.WriteSlotToDisk), new Type[] { typeof(SlotData), typeof(SaveGameSlots.Timestamp) })]
    internal static class Patch_SaveGameSlots_WriteSlotToDisk
    {
        private static void Prefix()
        {
            CaveExplorerMod.Instance?.OnBeforeGameSave();
        }
    }

    // In caves the map key opens the path map instead of the game's region map
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetOpenMapPressed), new Type[] { typeof(MonoBehaviour) })]
    internal static class Patch_InputManager_GetOpenMapPressed
    {
        private static void Postfix(ref bool __result)
        {
            if (__result && CaveExplorerMod.Instance != null && CaveExplorerMod.Instance.OnMapKeyPressed())
                __result = false;
        }
    }

    // The name shown on screen when entering a location is the real cave name
    [HarmonyPatch(typeof(Panel_HUD), nameof(Panel_HUD.ShowLocationReveal))]
    internal static class Patch_Panel_HUD_ShowLocationReveal
    {
        private static void Postfix(string text, string subText)
        {
            CaveExplorerMod.Instance?.OnLocationReveal(text, subText);
        }
    }

    // During gameplay the game hides the cursor every frame; while the path map is open these calls are skipped
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.ShowCursor), new Type[] { typeof(bool) })]
    internal static class Patch_InputManager_ShowCursor
    {
        private static bool Prefix(bool show)
        {
            return show || CaveExplorerMod.Instance == null || !CaveExplorerMod.Instance.IsMapOpen;
        }
    }

    // While the path map is open, escape closes it instead of opening the pause menu
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetEscapePressed), new Type[] { typeof(MonoBehaviour) })]
    internal static class Patch_InputManager_GetEscapePressed
    {
        private static void Postfix(ref bool __result)
        {
            if (__result && CaveExplorerMod.Instance != null && CaveExplorerMod.Instance.OnEscapePressed())
                __result = false;
        }
    }
}
