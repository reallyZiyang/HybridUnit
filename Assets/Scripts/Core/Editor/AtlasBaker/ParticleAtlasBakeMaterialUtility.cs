#if UNITY_EDITOR
using UnityEngine;

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

        Texture texture = GetMainTexture(sourceMaterial);
        if (texture != null)
        {
            SetTextureIfExists(material, "_BaseMap", texture);
            SetTextureIfExists(material, "_MainTex", texture);
        }

        SetColorIfExists(material, "_BaseColor", Color.white);
        SetColorIfExists(material, "_Color", Color.white);
        SetFloatIfExists(material, "_Surface", 1f);
        SetFloatIfExists(material, "_Blend", 2f);
        SetFloatIfExists(material, "_Cull", 0f);
        SetFloatIfExists(material, "_ZWrite", 0f);
        SetFloatIfExists(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfExists(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        SetFloatIfExists(material, "_AlphaClip", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_BLENDMODE_ADD");
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

    private static Texture GetMainTexture(Material material)
    {
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_BaseMap"))
        {
            Texture texture = material.GetTexture("_BaseMap");
            if (texture != null)
            {
                return texture;
            }
        }

        if (material.HasProperty("_MainTex"))
        {
            return material.GetTexture("_MainTex");
        }

        return null;
    }

    private static void SetTextureIfExists(Material material, string propertyName, Texture texture)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
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
