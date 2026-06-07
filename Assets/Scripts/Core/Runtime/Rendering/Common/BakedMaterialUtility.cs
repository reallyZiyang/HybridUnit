using UnityEngine;

public static class BakedMaterialUtility
{
    public static bool SetTextureIfNeeded(Material targetMaterial, int propertyId, Texture texture)
    {
        if (targetMaterial == null || texture == null || !targetMaterial.HasProperty(propertyId))
        {
            return false;
        }

        if (targetMaterial.GetTexture(propertyId) == texture)
        {
            return false;
        }

        targetMaterial.SetTexture(propertyId, texture);
        return true;
    }
}
