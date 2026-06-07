#if UNITY_EDITOR
public readonly struct ParticleAtlasBakeResult
{
    public ParticleAtlasBakeResult(string atlasProjectPath, int requestedFrameCount, int outputFrameCount, int visiblePixelCount, int firstVisibleFrame, int lastVisibleFrame)
    {
        AtlasProjectPath = atlasProjectPath;
        RequestedFrameCount = requestedFrameCount;
        OutputFrameCount = outputFrameCount;
        VisiblePixelCount = visiblePixelCount;
        FirstVisibleFrame = firstVisibleFrame;
        LastVisibleFrame = lastVisibleFrame;
    }

    public string AtlasProjectPath { get; }
    public int RequestedFrameCount { get; }
    public int OutputFrameCount { get; }
    public int VisiblePixelCount { get; }
    public int FirstVisibleFrame { get; }
    public int LastVisibleFrame { get; }
}
#endif
