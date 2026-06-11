using UnityEngine;

[CreateAssetMenu(fileName = "BakedSequenceAsset", menuName = "Hybrid/Rendering/Baked Sequence Asset")]
public sealed class BakedSequenceAsset : BattleRenderAssetBase
{
    public Texture2D atlas;
    public TextAsset metadataJson;
    public Material material;
    public bool playOnEnable = true;
    public bool loop = true;
    public float speed = 1f;
    public float displayScale = 1f;
    public Color color = Color.white;
    public bool skipEmptyFrames = true;
    public bool flipU;
    public bool flipV;
}
