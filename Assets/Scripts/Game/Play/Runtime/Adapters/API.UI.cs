using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Naipa.UIToolbar.Palette;
using UniKit.Localization;
using UnityEngine;

namespace Game.Play.Adapters
{
    public static partial class API
    {
        public static class UI
        {
            private static readonly Dictionary<string, Color> ColorPresets = new();

            public static async UniTask InitConfig()
            {
                var paletteData = await Assets.LoadAssetAsync<PaletteConfig>("UI_Palette");
                if (!paletteData) return;
                foreach (var colorPreset in paletteData.colorPresets)
                {
                    if (!ColorPresets.ContainsKey(colorPreset.name))
                    {
                        ColorPresets.Add(colorPreset.name, colorPreset.color);
                    }
                }
            }

            public static Color GetColor(string colorName)
            {
                if (ColorPresets.TryGetValue(colorName, out var color))
                {
                    return color;
                }
                return Color.white;
            }

            public static string GetColorHtml(string colorName)
            {
                if (ColorPresets.TryGetValue(colorName, out var color))
                {
                    return GetColorHtml(color);
                }
                return GetColorHtml(Color.white);
            }

            public static Color GetQualityColor(int quality)
            {
                return GetColor($"Item_Quality_{quality}");
            }

            public static string GetColorHtml(Color color)
            {
                return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
            }

            public static string GetQualityColorHtml(int quality)
            {
                Color color = GetQualityColor(quality);
                return GetColorHtml(color);
            }

            public static string LocalizeText(string key)
                => LocalizationManager.GetText(key);

            public static string LocalizeText(string key, params object[] args)
                => LocalizationManager.GetText(key, args);
        }
    }
}