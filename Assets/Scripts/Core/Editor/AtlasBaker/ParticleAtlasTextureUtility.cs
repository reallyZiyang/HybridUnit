#if UNITY_EDITOR
using System;
using UnityEngine;

public static class ParticleAtlasTextureUtility
{
    public static Color32[] ReadFramePixels(Texture2D frameTexture, bool alphaFromColor)
    {
        Color32[] pixels = frameTexture.GetPixels32();
        if (alphaFromColor)
        {
            ApplyAlphaFromColor(pixels);
        }

        return pixels;
    }

    public static void CopyFrameToAtlas(Color32[] pixels, Texture2D atlas, int frameIndex, ParticleAtlasBakeSettings settings, ParticleAtlasLayout layout)
    {
        int column = frameIndex % layout.Columns;
        int row = frameIndex / layout.Columns;
        int x = column * settings.FrameWidth;
        int y = settings.FirstFrameTopLeft ? layout.AtlasHeight - ((row + 1) * settings.FrameHeight) : row * settings.FrameHeight;

        atlas.SetPixels32(x, y, settings.FrameWidth, settings.FrameHeight, pixels);
    }

    public static void CopyFrameRectToAtlas(Color32[] pixels, Texture2D atlas, RectInt sourceRect, RectInt atlasRect, int frameWidth)
    {
        if (sourceRect.width <= 0 || sourceRect.height <= 0 || atlasRect.width <= 0 || atlasRect.height <= 0)
        {
            return;
        }

        Color32[] rectPixels = new Color32[sourceRect.width * sourceRect.height];
        for (int y = 0; y < sourceRect.height; y++)
        {
            int sourceOffset = (sourceRect.y + y) * frameWidth + sourceRect.x;
            int targetOffset = y * sourceRect.width;
            Array.Copy(pixels, sourceOffset, rectPixels, targetOffset, sourceRect.width);
        }

        atlas.SetPixels32(atlasRect.x, atlasRect.y, sourceRect.width, sourceRect.height, rectPixels);
    }

    public static int CountVisiblePixels(Color32[] pixels)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            if (pixel.a != 0 || pixel.r != 0 || pixel.g != 0 || pixel.b != 0)
            {
                count++;
            }
        }

        return count;
    }

    public static RectInt CalculateVisibleRect(Color32[] pixels, int width, int height, int padding)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                Color32 pixel = pixels[rowOffset + x];
                if (pixel.a == 0 && pixel.r == 0 && pixel.g == 0 && pixel.b == 0)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new RectInt(0, 0, 0, 0);
        }

        int safePadding = Mathf.Max(0, padding);
        minX = Mathf.Max(0, minX - safePadding);
        minY = Mathf.Max(0, minY - safePadding);
        maxX = Mathf.Min(width - 1, maxX + safePadding);
        maxY = Mathf.Min(height - 1, maxY + safePadding);

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static void FillTexture(Texture2D texture, Color32 color)
    {
        Color32[] pixels = new Color32[texture.width * texture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private static void ApplyAlphaFromColor(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            int colorAlpha = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
            // Additive 粒子常见写法是黑底贴图 + 加法混合，原始 alpha 可能覆盖整张 quad。
            // 烘培成普通透明序列图时应以 RGB 亮度重建 alpha，否则黑色区域会以非零 alpha 形成黑块。
            pixel.a = (byte)colorAlpha;
            pixels[i] = pixel;
        }
    }
}
#endif
