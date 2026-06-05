using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class BakedSpineVitPlayer : MonoBehaviour
{
    private static readonly int FrameIndexId = Shader.PropertyToID("_FrameIndex");
    private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int PositionTexId = Shader.PropertyToID("_PositionTex");
    private static readonly int ColorTexId = Shader.PropertyToID("_ColorTex");

    [SerializeField] private BakedSpineVitAsset asset;
    [SerializeField] private string defaultAnimation;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float speed = 1f;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool simulateInEditMode = true;
    [SerializeField, Min(0)] private int previewFrame;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private BakedSpineVitClip currentClip;
    private float time;
    private bool playing;
#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    public bool IsPlaying => playing;
    public string CurrentAnimation => currentClip != null ? currentClip.name : string.Empty;
    public int CurrentFrame { get; private set; }

    private void Awake()
    {
        EnsureComponents();
        ApplyDefaultClip();
        ApplyFrame(previewFrame);
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;
        lastEditorTime = EditorApplication.timeSinceStartup;
#endif
        EnsureComponents();
        ApplyDefaultClip();
        if (playOnEnable)
        {
            Play(defaultAnimation);
        }
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
    }

    private void OnValidate()
    {
        EnsureComponents();
        ApplyDefaultClip();
        if (!Application.isPlaying && !playing)
        {
            ApplyFrame(previewFrame);
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Tick(Time.deltaTime);
        }
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (Application.isPlaying || !simulateInEditMode)
        {
            return;
        }

        EnsureComponents();

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Min(0.1f, Mathf.Max(0f, (float)(now - lastEditorTime)));
        lastEditorTime = now;

        if (playing)
        {
            Tick(deltaTime);
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
        }
        else
        {
            ApplyFrame(previewFrame);
        }
    }
#endif

    public void Play(string animationName)
    {
        if (asset == null)
        {
            playing = false;
            return;
        }

        if (!asset.TryGetClip(string.IsNullOrEmpty(animationName) ? defaultAnimation : animationName, out currentClip))
        {
            playing = false;
            return;
        }

        time = 0f;
        playing = true;
        ApplyFrame(0);
    }

    public void Stop()
    {
        playing = false;
        time = 0f;
        ApplyFrame(0);
    }

    public void SetFrame(string animationName, int frame)
    {
        if (asset == null || !asset.TryGetClip(animationName, out currentClip))
        {
            return;
        }

        playing = false;
        ApplyFrame(frame);
    }

    private void Tick(float deltaTime)
    {
        if (!playing || currentClip == null || asset == null || currentClip.frameCount <= 0)
        {
            return;
        }

        time += deltaTime * Mathf.Max(0f, speed);
        float duration = Mathf.Max(0.0001f, currentClip.duration);
        if (time >= duration)
        {
            if (loop && currentClip.loop)
            {
                time %= duration;
            }
            else
            {
                time = duration;
                playing = false;
            }
        }

        int localFrame = Mathf.Clamp(Mathf.FloorToInt(time * Mathf.Max(1f, asset.frameRate)), 0, currentClip.frameCount - 1);
        ApplyFrame(localFrame);
    }

    private void ApplyDefaultClip()
    {
        if (asset == null)
        {
            currentClip = null;
            return;
        }

        if (currentClip == null || !string.IsNullOrEmpty(defaultAnimation) && currentClip.name != defaultAnimation)
        {
            asset.TryGetClip(defaultAnimation, out currentClip);
        }
    }

    private void ApplyFrame(int localFrame)
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (asset == null || currentClip == null || currentClip.frameCount <= 0)
        {
            meshRenderer.enabled = false;
            return;
        }

        int safeLocalFrame = Mathf.Clamp(localFrame, 0, currentClip.frameCount - 1);
        int absoluteFrame = currentClip.startFrame + safeLocalFrame;
        CurrentFrame = absoluteFrame;
        previewFrame = safeLocalFrame;

        meshRenderer.enabled = asset.mesh != null && asset.material != null;
        if (!meshRenderer.enabled)
        {
            meshRenderer.SetPropertyBlock(null);
            return;
        }

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.Clear();
        propertyBlock.SetFloat(FrameIndexId, absoluteFrame);
        propertyBlock.SetColor(InstanceColorId, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (asset == null)
        {
            meshFilter.sharedMesh = null;
            meshRenderer.sharedMaterial = null;
            return;
        }

        meshFilter.sharedMesh = asset.mesh;
        if (asset.material != null)
        {
            asset.material.enableInstancing = true;
            SetMaterialTextureIfNeeded(asset.material, MainTexId, asset.sourceTexture);
            SetMaterialTextureIfNeeded(asset.material, PositionTexId, asset.positionTexture);
            SetMaterialTextureIfNeeded(asset.material, ColorTexId, asset.colorTexture);
        }

        meshRenderer.sharedMaterial = asset.material;
    }

    private static void SetMaterialTextureIfNeeded(Material targetMaterial, int propertyId, Texture texture)
    {
        if (targetMaterial != null && texture != null && targetMaterial.HasProperty(propertyId) && targetMaterial.GetTexture(propertyId) != texture)
        {
            targetMaterial.SetTexture(propertyId, texture);
        }
    }
}
