#if UNITY_EDITOR
using System;

[Serializable]
public sealed class ParticleAtlasMetadata
{
    public string prefab;
    public bool loop;
    public bool loopBlend;
    public int loopBlendFrames;
    public float duration;
    public float effectiveDuration;
    public string resolutionPreset;
    public string frameRatePreset;
    public int frameRate;
    public int requestedFrameCount;
    public int frameCount;
    public int frameWidth;
    public int frameHeight;
    public int columns;
    public int rows;
    public float maxAtlasAspect;
    public int atlasWidth;
    public int atlasHeight;
    public int usedAtlasWidth;
    public int usedAtlasHeight;
    public bool powerOfTwoAtlas;
    public bool firstFrameTopLeft;
    public bool alphaFromColor;
    public bool useBakeParticleMaterial;
    public bool trimEmptyHead;
    public bool trimEmptyTail;
    public bool trimFrameRects;
    public bool packFrameRects;
    public int frameRectPadding;
    public int outputStartFrame;
    public int trimmedHeadFrameCount;
    public int trimmedFrameCount;
    public int firstVisibleFrame;
    public int lastVisibleFrame;
    public bool autoFrameCamera;
    public bool forceRandomSeed;
    public uint randomSeed;
    public ParticleAtlasFrameRect[] frameRects;
}

[Serializable]
public sealed class ParticleAtlasFrameRect
{
    public int frame;
    public int sourceX;
    public int sourceY;
    public int sourceWidth;
    public int sourceHeight;
    public int atlasX;
    public int atlasY;
    public int atlasWidth;
    public int atlasHeight;
    public float uvX;
    public float uvY;
    public float uvWidth;
    public float uvHeight;
    public float quadOffsetX;
    public float quadOffsetY;
    public float quadWidth;
    public float quadHeight;
}
#endif
