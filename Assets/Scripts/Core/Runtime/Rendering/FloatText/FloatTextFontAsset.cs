using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FloatTextFontAsset", menuName = "Hybrid/Rendering/Float Text Font Asset")]
public sealed class FloatTextFontAsset : ScriptableObject
{
    public Texture2D atlas;
    public Material material;
    public float pixelsPerUnit = 100f;
    public float defaultLineHeight = 150f;
    public FloatTextGlyph[] glyphs = Array.Empty<FloatTextGlyph>();

    public bool TryGetGlyph(string key, FloatTextStyleId style, out FloatTextGlyph glyph)
    {
        if (glyphs != null)
        {
            for (int i = 0; i < glyphs.Length; i++)
            {
                FloatTextGlyph candidate = glyphs[i];
                if (candidate != null &&
                    candidate.style == style &&
                    string.Equals(candidate.key, key, StringComparison.Ordinal))
                {
                    glyph = candidate;
                    return true;
                }
            }

            for (int i = 0; i < glyphs.Length; i++)
            {
                FloatTextGlyph candidate = glyphs[i];
                if (candidate != null &&
                    candidate.style == FloatTextStyleId.Damage &&
                    string.Equals(candidate.key, key, StringComparison.Ordinal))
                {
                    glyph = candidate;
                    return true;
                }
            }
        }

        glyph = null;
        return false;
    }
}

public enum FloatTextStyleId
{
    Damage = 0,
    Heal = 1,
    Icon = 2,
    Token = 3
}

[Serializable]
public sealed class FloatTextGlyph
{
    public string key;
    public FloatTextStyleId style;
    public Vector4 uvRect;
    public Vector2 pixelSize;
    public Vector2 offset;
    public float advance;
    public float scale = 1f;
}
