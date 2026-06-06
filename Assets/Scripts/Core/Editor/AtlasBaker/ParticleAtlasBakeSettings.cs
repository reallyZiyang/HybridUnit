#if UNITY_EDITOR
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ParticleAtlasBakeSettings", menuName = "Hybrid/Rendering/Particle Atlas Bake Settings")]
public sealed class ParticleAtlasBakeSettings : ScriptableObject
{
    public const string DefaultOutputFolder = "Assets/BakedSequences";

    public GameObject Prefab;
    public string RendererNameFilter = string.Empty;

    public ParticleAtlasBakeResolutionPreset ResolutionPreset = ParticleAtlasBakeResolutionPreset.Medium;
    public ParticleAtlasBakeFrameRatePreset FrameRatePreset = ParticleAtlasBakeFrameRatePreset.Medium;

    public bool Loop;
    public bool LoopBlend;
    public int LoopBlendFrames = 4;
    public float Duration = 1.0f;
    public int FrameRate = 20;
    public int FrameWidth = 128;
    public int FrameHeight = 128;
    public int Columns;
    public float MaxAtlasAspect = 4f;
    public int MaxAtlasSize = 8192;

    public string OutputFolder = DefaultOutputFolder;
    public string OutputName = string.Empty;

    public bool TransparentBackground = true;
    public Color BackgroundColor = new Color(0f, 0f, 0f, 0f);
    public bool FirstFrameTopLeft = true;
    public bool AlphaFromColor = true;
    public bool UseBakeParticleMaterial = true;
    public bool TrimEmptyHead = true;
    public bool TrimEmptyTail = true;
    public bool TrimFrameRects = true;
    public int FrameRectPadding = 2;
    public bool GenerateMetadata = true;
    public bool ConfigureTextureImporter = true;
    public bool ForceRandomSeed = true;
    public uint RandomSeed = 1;

    public bool AddDirectionalLight;
    public int AntiAliasing = 1;
    public int BakeLayer = 31;

    public bool Orthographic = true;
    public bool AutoFrameCamera = true;
    public float OrthographicSize = 5f;
    public float FieldOfView = 30f;
    public Vector3 CameraPosition = new Vector3(0f, 0f, -10f);
    public Vector3 CameraEulerAngles = Vector3.zero;

    public Vector3 PrefabPosition = Vector3.zero;
    public Vector3 PrefabEulerAngles = Vector3.zero;
    public Vector3 PrefabScale = Vector3.one;

    public void ApplyResolutionPreset()
    {
        switch (ResolutionPreset)
        {
            case ParticleAtlasBakeResolutionPreset.Low:
                FrameWidth = 64;
                FrameHeight = 64;
                break;
            case ParticleAtlasBakeResolutionPreset.Medium:
                FrameWidth = 128;
                FrameHeight = 128;
                break;
            case ParticleAtlasBakeResolutionPreset.High:
                FrameWidth = 256;
                FrameHeight = 256;
                break;
        }
    }

    public void ApplyFrameRatePreset()
    {
        switch (FrameRatePreset)
        {
            case ParticleAtlasBakeFrameRatePreset.Low:
                FrameRate = 15;
                break;
            case ParticleAtlasBakeFrameRatePreset.Medium:
                FrameRate = 20;
                break;
            case ParticleAtlasBakeFrameRatePreset.High:
                FrameRate = 30;
                break;
        }
    }
}

public enum ParticleAtlasBakeResolutionPreset
{
    Low,
    Medium,
    High,
    Custom
}

public enum ParticleAtlasBakeFrameRatePreset
{
    Low,
    Medium,
    High,
    Custom
}

#endif
