#if UNITY_EDITOR
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "AnimationVitBakeSettings", menuName = "Hybrid/Rendering/Animation VIT Bake Settings")]
public sealed class AnimationVitBakeSettings : ScriptableObject
{
    public const string DefaultOutputFolder = "Assets/BakedSequences";

    public GameObject SourceRoot;
    public AnimationClip[] Clips = Array.Empty<AnimationClip>();
    public AnimationVitBakeResolutionPreset ResolutionPreset = AnimationVitBakeResolutionPreset.Medium;
    public AnimationVitBakeFrameRatePreset FrameRatePreset = AnimationVitBakeFrameRatePreset.Medium;
    public int SourceTextureMaxSize = 1024;
    public int FrameRate = 20;
    public string OutputFolder = DefaultOutputFolder;
    public string OutputName = string.Empty;

    public void ApplyResolutionPreset()
    {
        switch (ResolutionPreset)
        {
            case AnimationVitBakeResolutionPreset.Low:
                SourceTextureMaxSize = 512;
                break;
            case AnimationVitBakeResolutionPreset.Medium:
                SourceTextureMaxSize = 1024;
                break;
            case AnimationVitBakeResolutionPreset.High:
                SourceTextureMaxSize = 2048;
                break;
        }
    }

    public void ApplyFrameRatePreset()
    {
        switch (FrameRatePreset)
        {
            case AnimationVitBakeFrameRatePreset.Low:
                FrameRate = 15;
                break;
            case AnimationVitBakeFrameRatePreset.Medium:
                FrameRate = 20;
                break;
            case AnimationVitBakeFrameRatePreset.High:
                FrameRate = 30;
                break;
        }
    }
}

public enum AnimationVitBakeResolutionPreset
{
    Low,
    Medium,
    High,
    Custom
}

public enum AnimationVitBakeFrameRatePreset
{
    Low,
    Medium,
    High,
    Custom
}
#endif
