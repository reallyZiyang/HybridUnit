#if UNITY_EDITOR
using System;
using Spine.Unity;
using UnityEngine;

[Serializable]
public sealed class SpineVitBakeSettings
{
    public const string DefaultOutputFolder = "Assets/BakedSequences";

    public SkeletonDataAsset SkeletonDataAsset;
    public string SkinName = string.Empty;
    public string[] AnimationNames = Array.Empty<string>();
    public int FrameRate = 20;
    public string OutputFolder = DefaultOutputFolder;
    public string OutputName = string.Empty;
}
#endif
