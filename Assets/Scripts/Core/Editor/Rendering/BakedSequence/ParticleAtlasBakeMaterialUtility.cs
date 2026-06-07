#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;

public static class ParticleAtlasBakeMaterialUtility
{
    public static void ApplyBakeParticleMaterials(GameObject instance)
    {
        ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            ParticleSystemRenderer particleRenderer = renderers[rendererIndex];
            if (particleRenderer == null)
            {
                continue;
            }

            Material[] sourceMaterials = particleRenderer.sharedMaterials;
            Material[] bakeMaterials = new Material[sourceMaterials.Length];
            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                bakeMaterials[materialIndex] = CreateBakeParticleMaterial(sourceMaterials[materialIndex], rendererIndex, materialIndex);
            }

            particleRenderer.sharedMaterials = bakeMaterials;
        }
    }

    private static Material CreateBakeParticleMaterial(Material sourceMaterial, int rendererIndex, int materialIndex)
    {
        Shader shader = FindBakeParticleShader();
        if (shader == null)
        {
            return sourceMaterial;
        }

        Material material = new Material(shader)
        {
            name = "ParticleAtlasBakeMaterial_" + rendererIndex + "_" + materialIndex,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };

        string sourceTextureProperty = GetMainTextureProperty(sourceMaterial);
        Texture texture = GetMainTexture(sourceMaterial, sourceTextureProperty);
        if (texture != null)
        {
            CopyTextureIfExists(material, "_BaseMap", texture, sourceMaterial, sourceTextureProperty);
            CopyTextureIfExists(material, "_MainTex", texture, sourceMaterial, sourceTextureProperty);
        }

        Color sourceColor = GetSourceColor(sourceMaterial);
        SetColorIfExists(material, "_BaseColor", sourceColor);
        SetColorIfExists(material, "_Color", sourceColor);
        SetFloatIfExists(material, "_Surface", 1f);
        SetFloatIfExists(material, "_Cull", 0f);
        SetFloatIfExists(material, "_ZWrite", 0f);
        SetFloatIfExists(material, "_AlphaClip", 0f);
        ApplyFlipbookSettings(material, sourceMaterial);

        ApplyBlendMode(material, IsAdditive(sourceMaterial));
        return material;
    }

    private static Shader FindBakeParticleShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Sprites/Default");
    }

    private static string GetMainTextureProperty(Material material)
    {
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
        {
            return "_BaseMap";
        }

        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
        {
            return "_MainTex";
        }

        return null;
    }

    private static Texture GetMainTexture(Material material, string propertyName)
    {
        if (material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
        {
            return null;
        }

        return material.GetTexture(propertyName);
    }

    private static Color GetSourceColor(Material material)
    {
        if (material == null)
        {
            return Color.white;
        }

        if (IsLegacyParticleShader(material) && material.HasProperty("_TintColor"))
        {
            Color tint = material.GetColor("_TintColor");
            return new Color(
                Mathf.Clamp01(tint.r * 2f),
                Mathf.Clamp01(tint.g * 2f),
                Mathf.Clamp01(tint.b * 2f),
                Mathf.Clamp01(tint.a * 2f));
        }

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        if (material.HasProperty("_TintColor"))
        {
            return material.GetColor("_TintColor");
        }

        return Color.white;
    }

    private static bool IsAdditive(Material material)
    {
        if (material == null)
        {
            return true;
        }

        string shaderName = material.shader != null ? material.shader.name : string.Empty;
        if (shaderName.IndexOf("Additive", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (material.HasProperty("_Blend") && Mathf.RoundToInt(material.GetFloat("_Blend")) == 2)
        {
            return true;
        }

        if (material.HasProperty("_DstBlend") && Mathf.RoundToInt(material.GetFloat("_DstBlend")) == (int)BlendMode.One)
        {
            return true;
        }

        return false;
    }

    private static bool IsLegacyParticleShader(Material material)
    {
        string shaderName = material != null && material.shader != null ? material.shader.name : string.Empty;
        return shaderName.IndexOf("Legacy Shaders/Particles", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("Particles/Additive", System.StringComparison.OrdinalIgnoreCase) >= 0
            || shaderName.IndexOf("Particles/Alpha", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyBlendMode(Material material, bool additive)
    {
        SetFloatIfExists(material, "_Blend", additive ? 2f : 0f);
        SetFloatIfExists(material, "_BlendOp", (float)BlendOp.Add);
        SetFloatIfExists(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfExists(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfExists(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfExists(material, "_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
    }

    private static void ApplyFlipbookSettings(Material material, Material sourceMaterial)
    {
        float flipbookBlending = GetSourceFloat(sourceMaterial, "_FlipbookBlending", "_FlipbookMode", 0f);
        SetFloatIfExists(material, "_FlipbookBlending", flipbookBlending);
        if (flipbookBlending > 0f)
        {
            material.EnableKeyword("_FLIPBOOKBLENDING_ON");
        }
        else
        {
            material.DisableKeyword("_FLIPBOOKBLENDING_ON");
        }
    }

    private static float GetSourceFloat(Material material, string propertyName, string fallbackPropertyName, float defaultValue)
    {
        if (material == null)
        {
            return defaultValue;
        }

        if (material.HasProperty(propertyName))
        {
            return material.GetFloat(propertyName);
        }

        if (!string.IsNullOrEmpty(fallbackPropertyName) && material.HasProperty(fallbackPropertyName))
        {
            return material.GetFloat(fallbackPropertyName);
        }

        return defaultValue;
    }

    private static void CopyTextureIfExists(Material material, string propertyName, Texture texture, Material sourceMaterial, string sourcePropertyName)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
            if (sourceMaterial != null && !string.IsNullOrEmpty(sourcePropertyName) && sourceMaterial.HasProperty(sourcePropertyName))
            {
                material.SetTextureScale(propertyName, sourceMaterial.GetTextureScale(sourcePropertyName));
                material.SetTextureOffset(propertyName, sourceMaterial.GetTextureOffset(sourcePropertyName));
            }
        }
    }

    private static void SetColorIfExists(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloatIfExists(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
#endif
