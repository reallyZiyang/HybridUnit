#if UNITY_EDITOR
using UnityEngine;

public readonly struct ParticleAtlasLayout
{
    public ParticleAtlasLayout(int frameCount, int columns, int rows, int usedAtlasWidth, int usedAtlasHeight, int atlasWidth, int atlasHeight)
    {
        FrameCount = frameCount;
        Columns = columns;
        Rows = rows;
        UsedAtlasWidth = usedAtlasWidth;
        UsedAtlasHeight = usedAtlasHeight;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;

        long usedArea = (long)usedAtlasWidth * usedAtlasHeight;
        long atlasArea = (long)atlasWidth * atlasHeight;
        WastePercent = atlasArea > 0L ? (atlasArea - usedArea) * 100f / atlasArea : 0f;
    }

    public int FrameCount { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int UsedAtlasWidth { get; }
    public int UsedAtlasHeight { get; }
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    public float WastePercent { get; }
}

public static class ParticleAtlasLayoutUtility
{
    public static int CalculateFrameCount(ParticleAtlasBakeSettings settings)
    {
        float duration = ParticleAtlasBakeUtility.GetBakeDuration(settings);
        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.001f, duration) * Mathf.Max(1, settings.FrameRate)));
    }

    public static ParticleAtlasLayout CalculateLayout(ParticleAtlasBakeSettings settings, int frameCount)
    {
        int columns = CalculateColumns(settings, frameCount);
        int rows = Mathf.CeilToInt(frameCount / (float)columns);
        int usedAtlasWidth = columns * Mathf.Max(1, settings.FrameWidth);
        int usedAtlasHeight = rows * Mathf.Max(1, settings.FrameHeight);
        int atlasWidth = Mathf.NextPowerOfTwo(usedAtlasWidth);
        int atlasHeight = Mathf.NextPowerOfTwo(usedAtlasHeight);

        return new ParticleAtlasLayout(frameCount, columns, rows, usedAtlasWidth, usedAtlasHeight, atlasWidth, atlasHeight);
    }

    public static int CalculateColumns(ParticleAtlasBakeSettings settings, int frameCount)
    {
        if (settings.Columns > 0)
        {
            return Mathf.Clamp(settings.Columns, 1, frameCount);
        }

        return CalculateOptimalPowerOfTwoColumns(settings, frameCount);
    }

    private static int CalculateOptimalPowerOfTwoColumns(ParticleAtlasBakeSettings settings, int frameCount)
    {
        int bestColumns = 1;
        long bestArea = long.MaxValue;
        int bestWaste = int.MaxValue;
        float bestAspectScore = float.MaxValue;
        int bestAllowedColumns = 0;
        long bestAllowedArea = long.MaxValue;
        int bestAllowedWaste = int.MaxValue;
        float bestAllowedAspectScore = float.MaxValue;
        float maxAspect = Mathf.Max(1f, settings.MaxAtlasAspect);

        for (int candidateColumns = 1; candidateColumns <= frameCount; candidateColumns++)
        {
            int candidateRows = Mathf.CeilToInt(frameCount / (float)candidateColumns);
            int usedWidth = candidateColumns * settings.FrameWidth;
            int usedHeight = candidateRows * settings.FrameHeight;
            int atlasWidth = Mathf.NextPowerOfTwo(usedWidth);
            int atlasHeight = Mathf.NextPowerOfTwo(usedHeight);
            long area = (long)atlasWidth * atlasHeight;
            int waste = atlasWidth * atlasHeight - usedWidth * usedHeight;
            float aspect = atlasWidth / (float)atlasHeight;
            float aspectScore = Mathf.Abs(Mathf.Log(Mathf.Max(0.001f, aspect), 2f));
            float normalizedAspect = Mathf.Max(aspect, 1f / Mathf.Max(0.001f, aspect));

            if (area < bestArea
                || area == bestArea && waste < bestWaste
                || area == bestArea && waste == bestWaste && aspectScore < bestAspectScore)
            {
                bestColumns = candidateColumns;
                bestArea = area;
                bestWaste = waste;
                bestAspectScore = aspectScore;
            }

            if (normalizedAspect <= maxAspect
                && (area < bestAllowedArea
                    || area == bestAllowedArea && waste < bestAllowedWaste
                    || area == bestAllowedArea && waste == bestAllowedWaste && aspectScore < bestAllowedAspectScore))
            {
                bestAllowedColumns = candidateColumns;
                bestAllowedArea = area;
                bestAllowedWaste = waste;
                bestAllowedAspectScore = aspectScore;
            }
        }

        return bestAllowedColumns > 0 ? bestAllowedColumns : bestColumns;
    }
}
#endif
