#if UNITY_EDITOR
using System;
using Spine.Unity;
using UnityEngine;

[CreateAssetMenu(fileName = "SpineVitBakeSettings", menuName = "Hybrid/Rendering/Spine VIT Bake Settings")]
public sealed class SpineVitBakeSettings : ScriptableObject
{
    public const string DefaultOutputFolder = "Assets/BakedSequences";

    public SkeletonDataAsset SkeletonDataAsset;
    public string SkinName = string.Empty;
    public string[] AnimationNames = Array.Empty<string>();
    public SpineVitBakeResolutionPreset ResolutionPreset = SpineVitBakeResolutionPreset.Medium;
    public SpineVitBakeFrameRatePreset FrameRatePreset = SpineVitBakeFrameRatePreset.Medium;
    public int SourceTextureMaxSize = 1024;
    public int FrameRate = 20;
    public string OutputFolder = DefaultOutputFolder;
    public string OutputName = string.Empty;

    public void ApplyResolutionPreset()
    {
        switch (ResolutionPreset)
        {
            case SpineVitBakeResolutionPreset.Low:
                SourceTextureMaxSize = 512;
                break;
            case SpineVitBakeResolutionPreset.Medium:
                SourceTextureMaxSize = 1024;
                break;
            case SpineVitBakeResolutionPreset.High:
                SourceTextureMaxSize = 2048;
                break;
        }
    }

    public void ApplyFrameRatePreset()
    {
        switch (FrameRatePreset)
        {
            case SpineVitBakeFrameRatePreset.Low:
                FrameRate = 15;
                break;
            case SpineVitBakeFrameRatePreset.Medium:
                FrameRate = 20;
                break;
            case SpineVitBakeFrameRatePreset.High:
                FrameRate = 30;
                break;
        }
    }
}

public enum SpineVitBakeResolutionPreset
{
    Low,
    Medium,
    High,
    Custom
}

public enum SpineVitBakeFrameRatePreset
{
    Low,
    Medium,
    High,
    Custom
}
#endif
