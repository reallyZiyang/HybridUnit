using System;
using UnityEngine;

namespace Game.Play.Battle.Runtime
{
    [Serializable]
    public struct BattlefieldBoundaryConfig
    {
        public bool enabled;
        public float rectWidth;
        public float rectHeight;
        public Vector2 rectCenterOffset;

        public static BattlefieldBoundaryConfig TesterDefault => new()
        {
            enabled = true,
            rectWidth = 9f,
            rectHeight = 16f,
            rectCenterOffset = Vector2.zero
        };
    }

    public static class BattlefieldBoundary
    {
        public static readonly Color FillColor = new(0.45f, 0.8f, 1f, 0.18f);

        public static bool IsEnabled(in BattlefieldBoundaryConfig config)
        {
            return config.enabled && HasRectangle(config);
        }

        public static bool Contains(Vector2 point, in BattlefieldBoundaryConfig config)
        {
            if (!IsEnabled(config))
            {
                return true;
            }

            Rect rect = GetRect(config);
            return point.x >= rect.xMin - 0.0001f
                && point.x <= rect.xMax + 0.0001f
                && point.y >= rect.yMin - 0.0001f
                && point.y <= rect.yMax + 0.0001f;
        }

        public static Vector2 Clamp(Vector2 point, in BattlefieldBoundaryConfig config)
        {
            if (!IsEnabled(config))
            {
                return point;
            }

            Rect rect = GetRect(config);
            return new Vector2(
                Mathf.Clamp(point.x, rect.xMin, rect.xMax),
                Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }

        public static Rect GetRect(in BattlefieldBoundaryConfig config)
        {
            float width = Mathf.Max(0f, config.rectWidth);
            float height = Mathf.Max(0f, config.rectHeight);
            return new Rect(
                config.rectCenterOffset.x - width * 0.5f,
                config.rectCenterOffset.y - height * 0.5f,
                width,
                height);
        }

        private static bool HasRectangle(in BattlefieldBoundaryConfig config)
        {
            return config.rectWidth > 0f
                && config.rectHeight > 0f
                && IsFinite(config.rectWidth)
                && IsFinite(config.rectHeight)
                && IsFinite(config.rectCenterOffset.x)
                && IsFinite(config.rectCenterOffset.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
