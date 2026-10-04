using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CaveExplorer
{
    /// <summary>
    /// Full screen cave map in the style of the game's map:
    /// black background, title and "last updated" top left, cave name top right, back button bottom right.
    /// </summary>
    public class CaveMapView
    {
        private const float ReferenceHeight = 1080f;
        private const int IconTextureSize = 64;

        private static readonly Color TextColor = new Color(0.93f, 0.92f, 0.9f, 1f);
        private static readonly Color SubTextColor = new Color(0.62f, 0.61f, 0.58f, 1f);

        private Texture2D _black;
        private Texture2D _buttonBackground;
        private Texture2D _buttonHover;
        private Texture2D _fallbackIcon;

        private Font _font;
        private bool _fontSearched;

        private Texture2D _gameIcon;
        private bool _iconLogged;

        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _buttonStyle;

        /// <summary>Draws the map. Returns true when the back button was clicked.</summary>
        public bool Draw(CaveMapRenderer renderer, CaveRecord record, string displayName, float hoursPlayed)
        {
            EnsureResources();

            float scale = Screen.height / ReferenceHeight;
            float margin = 70f * scale;
            Color oldColor = GUI.color;

            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);

            // Map area between header and footer
            float top = 150f * scale;
            float bottom = 110f * scale;
            float size = Math.Min(Screen.width - 2f * margin, Screen.height - top - bottom);
            var mapRect = new Rect((Screen.width - size) / 2f, top + (Screen.height - top - bottom - size) / 2f, size, size);
            if (renderer.Texture != null)
                GUI.DrawTexture(mapRect, renderer.Texture);

            DrawEntrances(renderer, record, mapRect, scale);

            // Header
            _titleStyle.fontSize = (int)(34f * scale);
            _subStyle.fontSize = (int)(15f * scale);
            _nameStyle.fontSize = (int)(34f * scale);
            _buttonStyle.fontSize = (int)(17f * scale);

            GUI.color = TextColor;
            GUI.Label(new Rect(margin, 40f * scale, Screen.width / 2f, 46f * scale),
                Text.Get(Text.TitleKeys, "Map").ToUpperInvariant(), _titleStyle);
            GUI.Label(new Rect(Screen.width / 2f, 40f * scale, Screen.width / 2f - margin, 46f * scale),
                displayName.ToUpperInvariant(), _nameStyle);
            GUI.color = SubTextColor;
            GUI.Label(new Rect(margin, 88f * scale, Screen.width / 2f, 24f * scale),
                Text.LastUpdated(record.LastUpdatedHours, hoursPlayed).ToUpperInvariant(), _subStyle);

            // Back button: drawn by hand
            float buttonWidth = 130f * scale;
            float buttonHeight = 44f * scale;
            var buttonRect = new Rect(Screen.width - margin - buttonWidth, Screen.height - 40f * scale - buttonHeight, buttonWidth, buttonHeight);
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool hover = buttonRect.Contains(mouse);

            GUI.color = Color.white;
            GUI.DrawTexture(buttonRect, hover ? _buttonHover : _buttonBackground);
            GUI.color = hover ? TextColor : SubTextColor;
            GUI.Label(buttonRect, Text.Get(new[] { "GAMEPLAY_ButtonBack" }, "Back").ToUpperInvariant(), _buttonStyle);
            GUI.color = oldColor;

            return hover && Input.GetMouseButtonDown(0);
        }

        private void DrawEntrances(CaveMapRenderer renderer, CaveRecord record, Rect mapRect, float scale)
        {
            float iconSize = 40f * scale;
            foreach (var entrance in record.Entrances)
            {
                Vector2 uv = renderer.WorldToUv(entrance[0], entrance[1]);
                var rect = new Rect(
                    mapRect.x + uv.x * mapRect.width - iconSize / 2f,
                    mapRect.y + (1f - uv.y) * mapRect.height - iconSize / 2f,
                    iconSize, iconSize);

                if (_gameIcon != null)
                    GUI.DrawTexture(rect, _gameIcon, ScaleMode.ScaleToFit);
                else
                    GUI.DrawTexture(rect, _fallbackIcon);
            }
        }

        /// <summary>Called when the map is opened; looks up game resources that may only exist once the game's map was loaded.</summary>
        public void OnOpen()
        {
            if (!_fontSearched || _font == null)
                FindFont();
            if (_gameIcon == null)
                FindCaveIcon();
        }

        private void EnsureResources()
        {
            if (_black == null)
                _black = CreateSolid(new Color32(0, 0, 0, 255));
            if (_buttonBackground == null)
                _buttonBackground = CreateSolid(new Color32(42, 42, 42, 235));
            if (_buttonHover == null)
                _buttonHover = CreateSolid(new Color32(70, 70, 70, 245));
            if (_fallbackIcon == null)
                _fallbackIcon = CreateCaveIcon();

            if (_titleStyle == null)
            {
                _titleStyle = CreateStyle(TextAnchor.UpperLeft);
                _subStyle = CreateStyle(TextAnchor.UpperLeft);
                _nameStyle = CreateStyle(TextAnchor.UpperRight);
                _buttonStyle = CreateStyle(TextAnchor.MiddleCenter);
            }

            if (_font != null && !_fontApplied)
            {
                _fontApplied = true;
                _titleStyle.font = _font;
                _subStyle.font = _font;
                _nameStyle.font = _font;
                _buttonStyle.font = _font;
            }
        }

        private bool _fontApplied;

        // A new GUIStyle has black text; it is set to white and tinted per label with GUI.color
        private static GUIStyle CreateStyle(TextAnchor alignment)
        {
            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.alignment = alignment;
            style.wordWrap = false;
            return style;
        }

        private static Texture2D CreateSolid(Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // White arch on transparent background, similar to the cave icon of the game's map
        private static Texture2D CreateCaveIcon()
        {
            int n = IconTextureSize;
            var pixels = new Color32[n * n];
            var white = new Color32(235, 232, 225, 255);
            float cx = n / 2f;
            float baseY = n * 0.18f;
            float outer = n * 0.42f;
            float inner = n * 0.2f;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - baseY;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    bool arch = dy >= 0f && d <= outer && d >= inner;
                    bool ground = dy >= -n * 0.06f && dy < 0f && Math.Abs(dx) <= outer + n * 0.04f;
                    pixels[y * n + x] = arch || ground ? white : new Color32(0, 0, 0, 0);
                }
            }

            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private void FindFont()
        {
            _fontSearched = true;
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_Map>(out Panel_Map map) && map != null)
                {
                    _font = GetLabelFont(map.m_HeaderLabel) ?? GetLabelFont(map.m_LastUpdatedLabel);
                    if (_font != null)
                    {
                        // MelonLogger.Msg($"Using map font '{_font.name}'");
                        return;
                    }
                }

                var names = new List<string>();
                foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
                    names.Add(font.name);
                MelonLogger.Msg($"Map font not found, using default. Loaded fonts: {string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error finding map font: {ex.Message}");
            }
        }

        private static Font GetLabelFont(UILabel label)
        {
            if (label == null)
                return null;
            if (label.trueTypeFont != null)
                return label.trueTypeFont;
            var bitmapFont = label.bitmapFont;
            return bitmapFont != null ? bitmapFont.dynamicFont : null;
        }

        // The game's atlas texture must not be kept between frames (its handle becomes invalid and drawing it
        // crashes the game). The sprite is copied into an own texture right away via a temporary RenderTexture,
        // because the atlas is not readable with GetPixels.
        private static Texture2D CopySprite(Texture atlas, UISpriteData sprite)
        {
            int width = sprite.width;
            int height = sprite.height;
            var scale = new Vector2((float)width / atlas.width, (float)height / atlas.height);
            var offset = new Vector2((float)sprite.x / atlas.width, 1f - (float)(sprite.y + height) / atlas.height);

            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(atlas, renderTexture, scale, offset);
                RenderTexture.active = renderTexture;

                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.hideFlags = HideFlags.HideAndDontSave;
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private void FindCaveIcon()
        {
            try
            {
                var candidates = new List<string>();
                UIAtlas bestAtlas = null;
                UISpriteData bestSprite = null;

                foreach (var atlas in Resources.FindObjectsOfTypeAll<UIAtlas>())
                {
                    var sprites = atlas.spriteList;
                    if (sprites == null)
                        continue;

                    foreach (var sprite in sprites)
                    {
                        if (sprite?.name == null || sprite.name.IndexOf("cave", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        candidates.Add($"{atlas.name}/{sprite.name}");
                        bool isMapIcon = sprite.name.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (bestSprite == null || isMapIcon)
                        {
                            bestAtlas = atlas;
                            bestSprite = sprite;
                        }
                    }
                }

                if (bestSprite != null && bestAtlas.texture != null)
                {
                    _gameIcon = CopySprite(bestAtlas.texture, bestSprite);
                    // MelonLogger.Msg($"Using cave icon '{bestAtlas.name}/{bestSprite.name}' (candidates: {string.Join(", ", candidates)})");
                }
                else if (!_iconLogged)
                {
                    MelonLogger.Msg("Cave icon of the game map not loaded yet, using own icon");
                }
                _iconLogged = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error finding cave icon: {ex.Message}");
            }
        }

        /// <summary>Texts from the game's localization with English fallbacks.</summary>
        private static class Text
        {
            public static readonly string[] TitleKeys = { "GAMEPLAY_Map" };

            public static string Get(string[] keys, string fallback)
            {
                foreach (var key in keys)
                {
                    try
                    {
                        if (Localization.Exists(key))
                        {
                            string text = Localization.Get(key);
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }
                    catch
                    {
                        // ignore and try the next key
                    }
                }

                return fallback;
            }

            public static string LastUpdated(float lastUpdatedHours, float hoursPlayed)
            {
                if (lastUpdatedHours < 0f)
                    return Get(new[] { "GAMEPLAY_MapLastUpdateNever" }, "Last updated: never");

                float hours = Math.Max(0f, hoursPlayed - lastUpdatedHours);
                if (hours < 1f)
                    return Get(new[] { "GAMEPLAY_MapLastUpdateJustNow" }, "Last updated: just now");
                if (hours < 24f)
                    return Format(Get(new[] { "GAMEPLAY_MapLastUpdateHoursAgo" }, "Last updated: {0} hours ago"), (int)hours);
                if (hours < 48f)
                    return Get(new[] { "GAMEPLAY_MapLastUpdateYesterday" }, "Last updated: yesterday");
                return Format(Get(new[] { "GAMEPLAY_MapLastUpdateDaysAgo" }, "Last updated: {0} days ago"), (int)(hours / 24f));
            }

            private static string Format(string text, int value)
            {
                return text.Replace("{0}", value.ToString());
            }
        }
    }
}
