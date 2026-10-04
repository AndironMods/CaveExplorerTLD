using System;
using System.Collections.Generic;
using UnityEngine;

namespace CaveExplorer
{
    /// <summary>
    /// Draws the recorded path into a transparent texture. North (world +z) is up.
    /// </summary>
    public class CaveMapRenderer
    {
        private const int Size = 1024;
        private const float MarginPercent = 0.08f;
        private const float MinExtentMeters = 40f;

        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);
        private static readonly Color32 PathColor = new Color32(222, 216, 204, 255);
        private static readonly Color32 PlayerColor = new Color32(196, 64, 48, 255);

        private readonly Color32[] _pixels = new Color32[Size * Size];
        private Texture2D _texture;
        private float _centerX;
        private float _centerZ;
        private float _scale;

        public Texture2D Texture => _texture;

        /// <summary>Converts a world position to texture coordinates (0..1, origin bottom left).</summary>
        public Vector2 WorldToUv(float x, float z)
        {
            return new Vector2(
                (Size / 2f + (x - _centerX) * _scale) / Size,
                (Size / 2f + (z - _centerZ) * _scale) / Size);
        }

        /// <param name="pathRadius">Radius in meters around each walked position that is drawn as explored.</param>
        public void Render(CaveRecord record, Vector3 playerPosition, Vector3 playerForward, float pathRadius)
        {
            if (_texture == null)
            {
                _texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                _texture.hideFlags = HideFlags.HideAndDontSave;
            }

            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = Transparent;

            // Bounding box of the path, entrances and player
            float minX = playerPosition.x, maxX = playerPosition.x;
            float minZ = playerPosition.z, maxZ = playerPosition.z;
            foreach (var segment in record.Segments)
            {
                for (int i = 0; i + 1 < segment.Count; i += 2)
                {
                    minX = Math.Min(minX, segment[i]);
                    maxX = Math.Max(maxX, segment[i]);
                    minZ = Math.Min(minZ, segment[i + 1]);
                    maxZ = Math.Max(maxZ, segment[i + 1]);
                }
            }
            foreach (var entrance in record.Entrances)
            {
                minX = Math.Min(minX, entrance[0]);
                maxX = Math.Max(maxX, entrance[0]);
                minZ = Math.Min(minZ, entrance[1]);
                maxZ = Math.Max(maxZ, entrance[1]);
            }

            float extent = Math.Max(Math.Max(maxX - minX, maxZ - minZ), MinExtentMeters);
            _centerX = (minX + maxX) / 2f;
            _centerZ = (minZ + maxZ) / 2f;
            _scale = Size * (1f - 2f * MarginPercent) / extent;
            int pathDiameter = Math.Max(1, (int)Math.Round(2f * pathRadius * _scale));

            foreach (var segment in record.Segments)
            {
                if (segment.Count == 2)
                    DrawDot(ToPixelX(segment[0]), ToPixelY(segment[1]), pathDiameter, PathColor);

                for (int i = 0; i + 3 < segment.Count; i += 2)
                {
                    DrawLine(
                        ToPixelX(segment[i]), ToPixelY(segment[i + 1]),
                        ToPixelX(segment[i + 2]), ToPixelY(segment[i + 3]),
                        pathDiameter, PathColor);
                }
            }

            // Player marker with a short line in view direction
            int px = ToPixelX(playerPosition.x);
            int py = ToPixelY(playerPosition.z);
            var direction = new Vector2(playerForward.x, playerForward.z);
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                DrawLine(px, py, px + (int)(direction.x * 22f), py + (int)(direction.y * 22f), 3, PlayerColor);
            }
            DrawDot(px, py, 11, PlayerColor);

            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }

        private int ToPixelX(float x) => (int)Math.Round(Size / 2f + (x - _centerX) * _scale);

        private int ToPixelY(float z) => (int)Math.Round(Size / 2f + (z - _centerZ) * _scale);

        private void DrawLine(int x0, int y0, int x1, int y1, int width, Color32 color)
        {
            // Wide lines: discs stamped along the line, a quarter of the width apart (a disc per pixel would be far too slow)
            if (width > 4)
            {
                float length = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                int steps = Math.Max(1, (int)Math.Ceiling(length / (width / 4f)));
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    DrawDot((int)Math.Round(x0 + (x1 - x0) * t), (int)Math.Round(y0 + (y1 - y0) * t), width, color);
                }
                return;
            }

            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                DrawDot(x0, y0, width, color);
                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void DrawDot(int cx, int cy, int diameter, Color32 color)
        {
            int r = diameter / 2;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= Size) continue;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= Size) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r + r)
                        _pixels[y * Size + x] = color;
                }
            }
        }
    }
}
