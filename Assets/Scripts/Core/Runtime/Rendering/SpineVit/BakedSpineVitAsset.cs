using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BakedSpineVitAsset", menuName = "Hybrid/Rendering/Baked Spine VIT Asset")]
public sealed class BakedSpineVitAsset : ScriptableObject
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
    public BakedSpineVitClip[] clips = Array.Empty<BakedSpineVitClip>();

    public bool TryGetClip(string clipName, out BakedSpineVitClip clip)
    {
        if (clips != null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                BakedSpineVitClip candidate = clips[i];
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
public sealed class BakedSpineVitClip
{
    public string name;
    public int startFrame;
    public int frameCount;
    public float duration;
    public bool loop = true;
}
