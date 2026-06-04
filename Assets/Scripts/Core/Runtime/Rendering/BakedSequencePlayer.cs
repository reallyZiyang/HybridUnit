using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class BakedSequencePlayer : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int FrameUVRectId = Shader.PropertyToID("_FrameUVRect");
    private static readonly int FrameTransformId = Shader.PropertyToID("_FrameTransform");
    private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
    private static Mesh sharedQuadMesh;
    private static readonly Matrix4x4[] MatrixBuffer = new Matrix4x4[1];
    private static readonly Vector4[] UVRectBuffer = new Vector4[1];
    private static readonly Vector4[] FrameTransformBuffer = new Vector4[1];
    private static readonly Vector4[] ColorBuffer = new Vector4[1];

    [SerializeField] private Texture2D atlas;
    [SerializeField] private TextAsset metadataJson;
    [SerializeField] private Material material;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float displayScale = 1f;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool simulateInEditMode = true;
    [SerializeField] private bool skipEmptyFrames = true;
    [SerializeField] private bool flipU;
    [SerializeField] private bool flipV;
    [SerializeField, Min(0)] private int previewFrame;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Material materialInstance;
    private Material sourceMaterial;
    private BakedSequenceMetadata metadata;
    private float time;
    private int currentFrame = -1;
    private bool playing;
    private bool currentFrameVisible;
#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    public bool IsPlaying => playing;
    public int CurrentFrame => currentFrame;
    public float Duration => metadata != null ? metadata.effectiveDuration : 0f;

    private void Awake()
    {
        EnsureComponents();
        LoadMetadata();
        ApplyFrame(0);
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;
        lastEditorTime = EditorApplication.timeSinceStartup;
#endif
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
        DestroyMaterialInstance();
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

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Tick(Time.deltaTime);
        DrawCurrentFrame();
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
            DrawCurrentFrame();
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
        }
        else
        {
            ApplyFrame(previewFrame);
            DrawCurrentFrame();
        }
    }
#endif

    private void Tick(float deltaTime)
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
        if (meshRenderer == null)
        {
            return;
        }

        if (metadata == null || metadata.frameRects == null || metadata.frameRects.Length == 0)
        {
            meshRenderer.enabled = false;
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
        meshRenderer.enabled = false;
        if (!visible)
        {
            return;
        }

        meshRenderer.GetPropertyBlock(propertyBlock);

        float uvX = flipU ? rect.uvX + rect.uvWidth : rect.uvX;
        float uvY = flipV ? rect.uvY + rect.uvHeight : rect.uvY;
        float uvWidth = flipU ? -rect.uvWidth : rect.uvWidth;
        float uvHeight = flipV ? -rect.uvHeight : rect.uvHeight;
        Vector4 uvRect = new Vector4(uvX, uvY, uvWidth, uvHeight);
        float safeDisplayScale = Mathf.Max(0.0001f, displayScale);
        Vector4 frameTransform = new Vector4(
            rect.quadOffsetX * safeDisplayScale,
            rect.quadOffsetY * safeDisplayScale,
            rect.quadWidth * safeDisplayScale,
            rect.quadHeight * safeDisplayScale);
        MatrixBuffer[0] = transform.localToWorldMatrix;
        UVRectBuffer[0] = uvRect;
        FrameTransformBuffer[0] = frameTransform;
        ColorBuffer[0] = color;
        propertyBlock.SetVector(FrameUVRectId, uvRect);
        propertyBlock.SetVector(FrameTransformId, frameTransform);
        propertyBlock.SetColor(InstanceColorId, color);
        propertyBlock.SetVectorArray(FrameUVRectId, UVRectBuffer);
        propertyBlock.SetVectorArray(FrameTransformId, FrameTransformBuffer);
        propertyBlock.SetVectorArray(InstanceColorId, ColorBuffer);
        if (atlas != null)
        {
            propertyBlock.SetTexture(MainTexId, atlas);
        }
    }

    private void EnsureComponents()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = GetQuadMesh();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (material == null)
        {
            DestroyMaterialInstance();
        }
        else if (materialInstance == null || sourceMaterial != material)
        {
            DestroyMaterialInstance();
            materialInstance = new Material(material)
            {
                name = material.name + " (BakedSequencePlayer Instance)",
                hideFlags = HideFlags.DontSave
            };
            materialInstance.enableInstancing = true;
            sourceMaterial = material;
        }

        if (materialInstance != null && meshRenderer.sharedMaterial != materialInstance)
        {
            meshRenderer.sharedMaterial = materialInstance;
        }

        meshRenderer.enabled = false;

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void DrawCurrentFrame()
    {
        if (!currentFrameVisible || materialInstance == null || propertyBlock == null)
        {
            return;
        }

        MatrixBuffer[0] = transform.localToWorldMatrix;
        Graphics.DrawMeshInstanced(
            GetQuadMesh(),
            0,
            materialInstance,
            MatrixBuffer,
            1,
            propertyBlock,
            ShadowCastingMode.Off,
            false,
            gameObject.layer,
            null,
            LightProbeUsage.Off);
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

    private void DestroyMaterialInstance()
    {
        if (materialInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(materialInstance);
        }
        else
        {
            DestroyImmediate(materialInstance);
        }

        materialInstance = null;
        sourceMaterial = null;
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
        sharedQuadMesh.bounds = new Bounds(Vector3.zero, Vector3.one);
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
