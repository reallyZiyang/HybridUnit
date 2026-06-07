#if UNITY_EDITOR
public struct AnimationVitBakeResult
{
    public AnimationVitBakeResult(string assetPath, int vertexCount, int totalFrameCount, int clipCount)
    {
        AssetPath = assetPath;
        VertexCount = vertexCount;
        TotalFrameCount = totalFrameCount;
        ClipCount = clipCount;
    }

    public string AssetPath { get; }
    public int VertexCount { get; }
    public int TotalFrameCount { get; }
    public int ClipCount { get; }
}
#endif
