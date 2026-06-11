using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    internal static class DrawMeshRenderMaterialUtility
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int PositionTexId = Shader.PropertyToID("_PositionTex");
        private static readonly int ColorTexId = Shader.PropertyToID("_ColorTex");
        private static Material fallbackProjectileMaterial;
        private static Material fallbackUnitMaterial;

        public static void ApplyVitMaterial(Material material, Texture mainTexture, Texture positionTexture, Texture colorTexture)
        {
            if (material == null)
            {
                return;
            }

            material.enableInstancing = true;
            SetTextureIfExists(material, MainTexId, mainTexture);
            SetTextureIfExists(material, PositionTexId, positionTexture);
            SetTextureIfExists(material, ColorTexId, colorTexture);
        }

        public static void ApplySequenceMaterial(BakedSequenceAsset asset)
        {
            Material material = asset.material;
            material.enableInstancing = true;
            SetTextureIfExists(material, MainTexId, asset.atlas);
            SetTextureIfExists(material, BaseMapId, asset.atlas);
            if (asset.atlas != null)
            {
                asset.atlas.wrapMode = TextureWrapMode.Clamp;
            }
        }

        public static void ApplyAtlasMaterial(AtlasRenderAsset asset)
        {
            Material material = asset.material;
            material.enableInstancing = true;
            Shader atlasShader = Shader.Find("Hybrid/Baked Effect Atlas");
            if (atlasShader != null && material.shader != atlasShader)
            {
                material.shader = atlasShader;
            }

            Texture texture = GetAtlasTexture(asset);
            if (texture != null)
            {
                SetTextureIfExists(material, MainTexId, texture);
                SetTextureIfExists(material, BaseMapId, texture);
            }
        }

        public static Material GetFallbackProjectileMaterial()
        {
            fallbackProjectileMaterial ??= CreateFallbackMaterial("Battle Projectile Fallback Material");
            return fallbackProjectileMaterial;
        }

        public static Material GetFallbackUnitMaterial()
        {
            fallbackUnitMaterial ??= CreateFallbackMaterial("Battle Unit Fallback Material");
            return fallbackUnitMaterial;
        }

        public static BakedSequenceMetadata LoadSequenceMetadata(BakedSequenceAsset asset)
        {
            if (asset == null || asset.metadataJson == null || string.IsNullOrWhiteSpace(asset.metadataJson.text))
            {
                return null;
            }

            return JsonUtility.FromJson<BakedSequenceMetadata>(asset.metadataJson.text);
        }

        public static int FindNextVisibleFrame(BakedSequenceMetadata metadata, int startFrame)
        {
            if (metadata == null || metadata.frameRects == null || metadata.frameRects.Length == 0)
            {
                return startFrame;
            }

            int safeStart = Mathf.Clamp(startFrame, 0, metadata.frameRects.Length - 1);
            for (int offset = 0; offset < metadata.frameRects.Length; offset++)
            {
                int frame = (safeStart + offset) % metadata.frameRects.Length;
                BakedSequenceFrameRect rect = metadata.frameRects[frame];
                if (rect.uvWidth > 0f && rect.uvHeight > 0f && rect.quadWidth > 0f && rect.quadHeight > 0f)
                {
                    return frame;
                }
            }

            return safeStart;
        }

        public static Vector4 CalculateUvClamp(Texture2D atlas, Vector4 uvRect)
        {
            float minX = Mathf.Min(uvRect.x, uvRect.x + uvRect.z);
            float maxX = Mathf.Max(uvRect.x, uvRect.x + uvRect.z);
            float minY = Mathf.Min(uvRect.y, uvRect.y + uvRect.w);
            float maxY = Mathf.Max(uvRect.y, uvRect.y + uvRect.w);
            if (atlas == null)
            {
                return new Vector4(minX, minY, maxX, maxY);
            }

            float insetX = 0.5f / Mathf.Max(1, atlas.width);
            float insetY = 0.5f / Mathf.Max(1, atlas.height);
            return new Vector4(
                Mathf.Min(minX + insetX, maxX),
                Mathf.Min(minY + insetY, maxY),
                Mathf.Max(maxX - insetX, minX),
                Mathf.Max(maxY - insetY, minY));
        }

        public static Texture GetAtlasTexture(AtlasRenderAsset asset)
        {
            Sprite sprite = GetAtlasSprite(asset);
            return sprite != null && sprite.texture != null ? sprite.texture : asset != null ? asset.atlas : null;
        }

        public static Sprite GetAtlasSprite(AtlasRenderAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            if (asset.sprite != null)
            {
                return asset.sprite;
            }

            return asset.spriteAtlas != null && !string.IsNullOrEmpty(asset.spriteName)
                ? asset.spriteAtlas.GetSprite(asset.spriteName)
                : null;
        }

        public static Vector4 GetAtlasUvRect(AtlasRenderAsset asset)
        {
            Sprite sprite = GetAtlasSprite(asset);
            if (sprite == null)
            {
                return asset != null ? asset.uvRect : new Vector4(0f, 0f, 1f, 1f);
            }

            Vector2[] uvs = sprite.uv;
            if (uvs == null || uvs.Length == 0)
            {
                return asset.uvRect;
            }

            float minX = uvs[0].x;
            float minY = uvs[0].y;
            float maxX = uvs[0].x;
            float maxY = uvs[0].y;
            for (int i = 1; i < uvs.Length; i++)
            {
                Vector2 uv = uvs[i];
                minX = Mathf.Min(minX, uv.x);
                minY = Mathf.Min(minY, uv.y);
                maxX = Mathf.Max(maxX, uv.x);
                maxY = Mathf.Max(maxY, uv.y);
            }

            Vector4 uvRect = new(minX, minY, Mathf.Max(0f, maxX - minX), Mathf.Max(0f, maxY - minY));
            return uvRect.z > 0f && uvRect.w > 0f ? uvRect : asset.uvRect;
        }

        public static Vector4 GetSafeUvRect(Vector4 uvRect)
        {
            return uvRect.z == 0f || uvRect.w == 0f ? new Vector4(0f, 0f, 1f, 1f) : uvRect;
        }

        public static Vector4 GetUvClamp(Vector4 uvRect)
        {
            uvRect = GetSafeUvRect(uvRect);
            return new Vector4(
                Mathf.Min(uvRect.x, uvRect.x + uvRect.z),
                Mathf.Min(uvRect.y, uvRect.y + uvRect.w),
                Mathf.Max(uvRect.x, uvRect.x + uvRect.z),
                Mathf.Max(uvRect.y, uvRect.y + uvRect.w));
        }

        private static void SetTextureIfExists(Material material, int propertyId, Texture texture)
        {
            if (material != null && texture != null && material.HasProperty(propertyId))
            {
                material.SetTexture(propertyId, texture);
            }
        }

        private static Material CreateFallbackMaterial(string materialName)
        {
            Shader shader = Shader.Find("Hybrid/Battle DrawMesh Instance Unlit")
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            Material material = new(shader)
            {
                name = materialName,
                enableInstancing = true,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_InstanceColor"))
            {
                material.SetColor("_InstanceColor", Color.white);
            }

            return material;
        }
    }
}
