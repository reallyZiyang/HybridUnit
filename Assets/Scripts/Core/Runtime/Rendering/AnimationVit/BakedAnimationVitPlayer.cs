using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class BakedAnimationVitPlayer : BakedTickPlayer
{
    private static readonly int RenderTrans = Shader.PropertyToID("_RenderTrans");
    private static readonly int FrameIndexId = Shader.PropertyToID("_FrameIndex");
    private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int PositionTexId = Shader.PropertyToID("_PositionTex");
    private static readonly int ColorTexId = Shader.PropertyToID("_ColorTex");

    [SerializeField] private BakedAnimationVitAsset asset;
    [SerializeField] private string defaultClip;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;
    [SerializeField] private float speed = 1f;
    [SerializeField] private Color color = Color.white;
    [SerializeField, Min(0)] private int previewFrame;

    [SerializeField]
    private Vector2 renderOffset = new Vector2(0, 0);

    private BakedAnimationVitClip currentClip;
    private float time;
    private bool playing;

    public BakedAnimationVitAsset Asset => asset;
    public bool IsPlaying => playing;
    public string CurrentClip => currentClip != null ? currentClip.name : string.Empty;
    public int CurrentFrame { get; private set; }
    public Color InstanceColor => color;
    protected override bool IsRuntimeTickActive => playing;
    protected override bool IsEditorTickActive => playing;

    private void Awake()
    {
        EnsureComponents();
        ApplyDefaultClip();
        ApplyFrame(previewFrame);
    }

    protected override void OnPlayerEnable()
    {
        EnsureComponents();
        ApplyDefaultClip();
        if (playOnEnable)
        {
            Play(defaultClip);
        }
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

    public void Play(string clipName)
    {
        if (asset == null)
        {
            playing = false;
            return;
        }

        if (!asset.TryGetClip(string.IsNullOrEmpty(clipName) ? defaultClip : clipName, out currentClip))
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

    public void SetFrame(string clipName, int frame)
    {
        if (asset == null || !asset.TryGetClip(clipName, out currentClip))
        {
            return;
        }

        playing = false;
        ApplyFrame(frame);
    }

    public void SetInstanceColor(Color instanceColor)
    {
        color = instanceColor;
        ApplyFrame(previewFrame);
    }

    protected override void Tick(float deltaTime)
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

    protected override void OnBeforeEditorTick()
    {
        EnsureComponents();
    }

    protected override void OnEditorPreviewTick()
    {
        ApplyFrame(previewFrame);
    }

    private void ApplyDefaultClip()
    {
        if (asset == null)
        {
            currentClip = null;
            return;
        }

        if (currentClip == null || !string.IsNullOrEmpty(defaultClip) && currentClip.name != defaultClip)
        {
            asset.TryGetClip(defaultClip, out currentClip);
        }
    }

    private void ApplyFrame(int localFrame)
    {
        if (PlayerRenderer == null)
        {
            return;
        }

        if (asset == null || currentClip == null || currentClip.frameCount <= 0)
        {
            SetRendererVisible(false);
            return;
        }

        int safeLocalFrame = Mathf.Clamp(localFrame, 0, currentClip.frameCount - 1);
        int absoluteFrame = currentClip.startFrame + safeLocalFrame;
        CurrentFrame = absoluteFrame;
        previewFrame = safeLocalFrame;

        bool visible = asset.mesh != null && asset.material != null;
        SetRendererVisible(visible);
        if (!visible)
        {
            ClearPropertyBlock();
            return;
        }

        MaterialPropertyBlock propertyBlock = BeginPropertyBlock();
        propertyBlock.SetFloat(FrameIndexId, absoluteFrame);
        propertyBlock.SetColor(InstanceColorId, color);
        //propertyBlock.SetVector(RenderTrans, renderTrans);
        ApplyPropertyBlock();
    }

    private void EnsureComponents()
    {
        EnsureRendererComponents();

        if (asset == null)
        {
            SetSharedMesh(null);
            SetSharedMaterial(null);
            return;
        }

        SetSharedMesh(asset.mesh);
        if (asset.material != null)
        {
            BakedMaterialUtility.SetTextureIfNeeded(asset.material, MainTexId, asset.sourceTexture);
            BakedMaterialUtility.SetTextureIfNeeded(asset.material, PositionTexId, asset.positionTexture);
            BakedMaterialUtility.SetTextureIfNeeded(asset.material, ColorTexId, asset.colorTexture);
        }

        SetSharedMaterial(asset.material);
    }
}
