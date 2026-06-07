using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BakedAnimationVitAsset", menuName = "Hybrid/Rendering/Baked Animation VIT Asset")]
public sealed class BakedAnimationVitAsset : ScriptableObject
{
    public Mesh mesh;
    public Material material;
    public Texture2D sourceTexture;
    public Texture2D positionTexture;
    public Texture2D colorTexture;
    public float frameRate = 20f;
    public int vertexCount;
    public int totalFrameCount;
    public Bounds bounds;
    public BakedAnimationVitClip[] clips = Array.Empty<BakedAnimationVitClip>();

    public bool TryGetClip(string clipName, out BakedAnimationVitClip clip)
    {
        if (clips != null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                BakedAnimationVitClip candidate = clips[i];
                if (candidate != null && string.Equals(candidate.name, clipName, StringComparison.Ordinal))
                {
                    clip = candidate;
                    return true;
                }
            }
        }

        clip = clips != null && clips.Length > 0 ? clips[0] : null;
        return clip != null;
    }
}

[Serializable]
public sealed class BakedAnimationVitClip
{
    public string name;
    public int startFrame;
    public int frameCount;
    public float duration;
    public bool loop = true;
}
