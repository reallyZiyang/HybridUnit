using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class BakedSequencePlayer : BakedTickPlayer
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int FrameUVRectId = Shader.PropertyToID("_FrameUVRect");
    private static readonly int FrameUVClampId = Shader.PropertyToID("_FrameUVClamp");
    private static readonly int FrameTransformId = Shader.PropertyToID("_FrameTransform");
    private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
    private static Mesh sharedQuadMesh;

    [SerializeField] private Texture2D atlas;
    [SerializeField] private TextAsset metadataJson;
    [SerializeField] private Material material;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float displayScale = 1f;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool skipEmptyFrames = true;
    [SerializeField] private bool flipU;
    [SerializeField] private bool flipV;
    [SerializeField, Min(0)] private int previewFrame;

    private BakedSequenceMetadata metadata;
    private float time;
    private int currentFrame = -1;
    private bool playing;
    private bool currentFrameVisible;

    public bool IsPlaying => playing;
    public int CurrentFrame => currentFrame;
    public float Duration => metadata != null ? metadata.effectiveDuration : 0f;
    protected override bool IsRuntimeTickActive => playing;
    protected override bool IsEditorTickActive => playing;

    private void Awake()
    {
        EnsureComponents();
        LoadMetadata();
        ApplyFrame(0);
    }

    protected override void OnPlayerEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnValidate()
    {
        EnsureComponents();
        LoadMetadata();
        if (!Application.isPlaying && !playing)
        {
            ApplyFrame(previewFrame);
        }
        else
        {
            ApplyFrame(currentFrame < 0 ? 0 : currentFrame);
        }
    }

    protected override void Tick(float deltaTime)
    {
        if (!playing || metadata == null || metadata.frameCount <= 0)
        {
            return;
        }

        time += deltaTime * Mathf.Max(0f, speed);
        float duration = Mathf.Max(0.0001f, metadata.effectiveDuration);
        if (time >= duration)
        {
            if (loop)
            {
                time %= duration;
            }
            else
            {
                time = duration;
                playing = false;
            }
        }

        int frame = Mathf.Clamp(Mathf.FloorToInt(time * metadata.frameRate), 0, metadata.frameCount - 1);
        ApplyFrame(frame);
    }

    protected override void OnBeforeEditorTick()
    {
        EnsureComponents();
    }

    protected override void OnEditorPreviewTick()
    {
        ApplyFrame(previewFrame);
    }

    public void Play()
    {
        if (metadata == null)
        {
            LoadMetadata();
        }

        time = 0f;
        playing = true;
        ApplyFrame(skipEmptyFrames ? FindNextVisibleFrame(0) : 0);
    }

    public void Stop()
    {
        playing = false;
        time = 0f;
        ApplyFrame(0);
    }

    public void SetFrame(int frame)
    {
        playing = false;
        ApplyFrame(frame);
    }

    private void LoadMetadata()
    {
        metadata = null;
        if (metadataJson == null || string.IsNullOrWhiteSpace(metadataJson.text))
        {
            return;
        }

        metadata = JsonUtility.FromJson<BakedSequenceMetadata>(metadataJson.text);
        previewFrame = metadata != null ? Mathf.Clamp(previewFrame, 0, Mathf.Max(0, metadata.frameCount - 1)) : 0;
    }

    private void ApplyFrame(int frame)
    {
        if (PlayerRenderer == null)
        {
            return;
        }

        if (metadata == null || metadata.frameRects == null || metadata.frameRects.Length == 0)
        {
            SetRendererVisible(false);
            return;
        }

        int safeFrame = Mathf.Clamp(frame, 0, metadata.frameRects.Length - 1);
        if (skipEmptyFrames)
        {
            safeFrame = FindNextVisibleFrame(safeFrame);
        }

        BakedSequenceFrameRect rect = metadata.frameRects[safeFrame];
        currentFrame = safeFrame;
        previewFrame = safeFrame;

        bool visible = rect.uvWidth > 0f && rect.uvHeight > 0f && rect.quadWidth > 0f && rect.quadHeight > 0f;
        currentFrameVisible = visible;
        SetRendererVisible(visible);
        if (!visible)
        {
            ClearPropertyBlock();
            return;
        }

        MaterialPropertyBlock propertyBlock = BeginPropertyBlock();

        float uvX = flipU ? rect.uvX + rect.uvWidth : rect.uvX;
        float uvY = flipV ? rect.uvY + rect.uvHeight : rect.uvY;
        float uvWidth = flipU ? -rect.uvWidth : rect.uvWidth;
        float uvHeight = flipV ? -rect.uvHeight : rect.uvHeight;
        Vector4 uvRect = new Vector4(uvX, uvY, uvWidth, uvHeight);
        Vector4 uvClamp = CalculateUvClamp(uvX, uvY, uvWidth, uvHeight);
        float safeDisplayScale = Mathf.Max(0.0001f, displayScale);
        Vector4 frameTransform = new Vector4(
            rect.quadOffsetX * safeDisplayScale,
            rect.quadOffsetY * safeDisplayScale,
            rect.quadWidth * safeDisplayScale,
            rect.quadHeight * safeDisplayScale);
        propertyBlock.SetVector(FrameUVRectId, uvRect);
        propertyBlock.SetVector(FrameUVClampId, uvClamp);
        propertyBlock.SetVector(FrameTransformId, frameTransform);
        propertyBlock.SetColor(InstanceColorId, color);
        if (atlas != null)
        {
            propertyBlock.SetTexture(MainTexId, atlas);
        }

        ApplyPropertyBlock();
    }

    private Vector4 CalculateUvClamp(float uvX, float uvY, float uvWidth, float uvHeight)
    {
        float minX = Mathf.Min(uvX, uvX + uvWidth);
        float maxX = Mathf.Max(uvX, uvX + uvWidth);
        float minY = Mathf.Min(uvY, uvY + uvHeight);
        float maxY = Mathf.Max(uvY, uvY + uvHeight);

        if (atlas == null)
        {
            return new Vector4(minX, minY, maxX, maxY);
        }

        // Bilinear 采样到 atlas 单元格边缘时会混到相邻帧，半 texel 内缩可以避免边界串帧。
        float insetX = 0.5f / Mathf.Max(1, atlas.width);
        float insetY = 0.5f / Mathf.Max(1, atlas.height);
        return new Vector4(
            Mathf.Min(minX + insetX, maxX),
            Mathf.Min(minY + insetY, maxY),
            Mathf.Max(maxX - insetX, minX),
            Mathf.Max(maxY - insetY, minY));
    }

    private void EnsureComponents()
    {
        EnsureRendererComponents();
        SetSharedMesh(GetQuadMesh());

        if (material == null)
        {
            SetSharedMaterial(null);
        }
        else
        {
            if (atlas != null)
            {
                atlas.wrapMode = TextureWrapMode.Clamp;
            }

            SetSharedMaterial(material);
        }
    }

    private int FindNextVisibleFrame(int startFrame)
    {
        if (metadata == null || metadata.frameRects == null || metadata.frameRects.Length == 0)
        {
            return startFrame;
        }

        int safeStart = Mathf.Clamp(startFrame, 0, metadata.frameRects.Length - 1);
        for (int offset = 0; offset < metadata.frameRects.Length; offset++)
        {
            int frame = (safeStart + offset) % metadata.frameRects.Length;
            BakedSequenceFrameRect rect = metadata.frameRects[frame];
            if (rect.uvWidth > 0f && rect.uvHeight > 0f && rect.quadWidth > 0f && rect.quadHeight > 0f)
            {
                return frame;
            }
        }

        return safeStart;
    }

    private static Mesh GetQuadMesh()
    {
        if (sharedQuadMesh != null)
        {
            return sharedQuadMesh;
        }

        sharedQuadMesh = new Mesh
        {
            name = "Baked Sequence Quad",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };
        sharedQuadMesh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 1f));
        sharedQuadMesh.RecalculateNormals();
        return sharedQuadMesh;
    }

    [Serializable]
    private sealed class BakedSequenceMetadata
    {
        public float effectiveDuration;
        public int frameRate;
        public int frameCount;
        public int firstVisibleFrame;
        public int lastVisibleFrame;
        public BakedSequenceFrameRect[] frameRects;
    }

    [Serializable]
    private sealed class BakedSequenceFrameRect
    {
        public float uvX;
        public float uvY;
        public float uvWidth;
        public float uvHeight;
        public float quadOffsetX;
        public float quadOffsetY;
        public float quadWidth;
        public float quadHeight;
    }
}
