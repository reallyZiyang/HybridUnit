using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FloatTextElement : TickMeshElement
{
    private static readonly HashSet<string> MissingGlyphWarnings = new HashSet<string>();

    [SerializeField] private FloatTextFontAsset fontAsset;
    [SerializeField] private bool playOnEnable;
    [SerializeField] private string previewText = "1200";
    [SerializeField] private FloatTextStyleId previewStyle = FloatTextStyleId.Damage;
    [SerializeField, Min(0.05f)] private float lifetime = 0.8f;
    [SerializeField, Min(0f)] private float punchDuration = 0.12f;
    [SerializeField] private float punchScale = 1.25f;
    [SerializeField] private float floatDistance = 0.8f;
    [SerializeField, Range(0f, 1f)] private float fadeStart = 0.65f;
    [SerializeField] private Color color = Color.white;

    private readonly List<FloatTextQuad> quads = new List<FloatTextQuad>(16);
    private Vector3 baseLocalPosition;
    private float elapsed;
    private float currentAlpha = 1f;
    private bool playing;
    private bool paused;
    private bool editorPreviewVisible;

    public bool IsPlaying => playing;
    public FloatTextFontAsset FontAsset => fontAsset;
    public override bool CanWriteQuads => base.CanWriteQuads && fontAsset != null && quads.Count > 0 && (playing || editorPreviewVisible || !Application.isPlaying);
    protected override bool IsRuntimeTickActive => playing && !paused;
    protected override bool IsEditorTickActive => playing && !paused;

    protected override void OnElementEnable()
    {
        EnsureFontAsset();
        ConfigureMeshPlayer();
        DisableLegacyRenderer();
        if (playOnEnable)
        {
            Play(previewText, previewStyle);
        }
        else if (!Application.isPlaying)
        {
            BuildQuads(previewText, previewStyle);
            editorPreviewVisible = true;
        }
    }

    protected override void OnElementDisable()
    {
        playing = false;
        editorPreviewVisible = false;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureFontAsset();
        ConfigureMeshPlayer();
        DisableLegacyRenderer();
        if (!Application.isPlaying && fontAsset != null)
        {
            BuildQuads(previewText, previewStyle);
            currentAlpha = 1f;
            editorPreviewVisible = true;
        }
    }

    public void PlayDamage(int value)
    {
        Play(Mathf.Abs(value).ToString(), FloatTextStyleId.Damage);
    }

    public void PlayDamage(int value, Vector3 worldPosition)
    {
        transform.position = worldPosition;
        PlayDamage(value);
    }

    public void PlayHeal(int value)
    {
        Play("+" + Mathf.Abs(value), FloatTextStyleId.Heal);
    }

    public void PlayHeal(int value, Vector3 worldPosition)
    {
        transform.position = worldPosition;
        PlayHeal(value);
    }

    public void PlayCritical(int value)
    {
        Play("crit_icon" + Mathf.Abs(value), FloatTextStyleId.Damage);
    }

    public void PlayCritical(int value, Vector3 worldPosition)
    {
        transform.position = worldPosition;
        PlayCritical(value);
    }

    public void PlayMiss()
    {
        Play("MISS", FloatTextStyleId.Token);
    }

    public void PlayMiss(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        PlayMiss();
    }

    public void Play(string text, FloatTextStyleId style)
    {
        EnsureFontAsset();
        ConfigureMeshPlayer();
        BuildQuads(text, style);
        baseLocalPosition = transform.localPosition;
        elapsed = 0f;
        currentAlpha = 1f;
        playing = quads.Count > 0;
        editorPreviewVisible = false;
        ApplyMotion(0f);
    }

    public void Bind(FloatTextFontAsset targetFontAsset, MeshPlayer targetMeshPlayer)
    {
        if (targetFontAsset != null)
        {
            fontAsset = targetFontAsset;
        }

        if (targetMeshPlayer != null)
        {
            SetMeshPlayer(targetMeshPlayer);
        }

        ConfigureMeshPlayer();
        DisableLegacyRenderer();
    }

    public void SetPaused(bool value)
    {
        paused = value;
    }

    public void Stop()
    {
        playing = false;
        editorPreviewVisible = false;
    }

    public override void WriteQuads(MeshQuadWriter writer)
    {
        Color instanceColor = color;
        instanceColor.a *= Mathf.Clamp01(currentAlpha);
        Color32 vertexColor = instanceColor;
        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        for (int i = 0; i < quads.Count; i++)
        {
            FloatTextQuad quad = quads[i];
            writer.AddQuad(localToWorld, quad.XMin, quad.YMin, quad.XMax, quad.YMax, quad.UvRect, vertexColor);
        }
    }

    [ContextMenu("Preview Damage")]
    private void PreviewDamage()
    {
        Play(previewText, FloatTextStyleId.Damage);
    }

    [ContextMenu("Preview Heal")]
    private void PreviewHeal()
    {
        Play("+350", FloatTextStyleId.Heal);
    }

    [ContextMenu("Preview Critical")]
    private void PreviewCritical()
    {
        Play("crit_icon9999", FloatTextStyleId.Damage);
    }

    [ContextMenu("Preview Miss")]
    private void PreviewMiss()
    {
        PlayMiss();
    }

    protected override void Tick(float deltaTime)
    {
        elapsed += Mathf.Max(0f, deltaTime);
        float normalizedTime = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;
        ApplyMotion(normalizedTime);
        currentAlpha = CalculateAlpha(normalizedTime);

        if (elapsed >= lifetime)
        {
            Stop();
        }
    }

    private void ApplyMotion(float normalizedTime)
    {
        float punch = 1f;
        if (punchDuration > 0f && elapsed < punchDuration)
        {
            float punchT = Mathf.Clamp01(elapsed / punchDuration);
            punch = Mathf.Lerp(Mathf.Max(0.0001f, punchScale), 1f, Smooth01(punchT));
        }

        transform.localScale = Vector3.one * punch;
        transform.localPosition = baseLocalPosition + Vector3.up * (floatDistance * Smooth01(normalizedTime));
    }

    private float CalculateAlpha(float normalizedTime)
    {
        if (normalizedTime <= fadeStart)
        {
            return 1f;
        }

        float fadeT = Mathf.InverseLerp(fadeStart, 1f, normalizedTime);
        return 1f - Smooth01(fadeT);
    }

    private void BuildQuads(string text, FloatTextStyleId style)
    {
        quads.Clear();
        if (fontAsset == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        List<ResolvedGlyph> resolvedGlyphs = ResolveGlyphs(text, style);
        if (resolvedGlyphs.Count == 0)
        {
            return;
        }

        float pixelsPerUnit = Mathf.Max(0.0001f, fontAsset.pixelsPerUnit);
        float totalAdvance = 0f;
        for (int i = 0; i < resolvedGlyphs.Count; i++)
        {
            FloatTextGlyph glyph = resolvedGlyphs[i].Glyph;
            totalAdvance += GetAdvance(glyph) * Mathf.Max(0.0001f, glyph.scale);
        }

        float cursor = -totalAdvance * 0.5f;
        for (int i = 0; i < resolvedGlyphs.Count; i++)
        {
            FloatTextGlyph glyph = resolvedGlyphs[i].Glyph;
            float glyphScale = Mathf.Max(0.0001f, glyph.scale);
            float width = glyph.pixelSize.x * glyphScale;
            float height = glyph.pixelSize.y * glyphScale;
            float xMin = (cursor + glyph.offset.x * glyphScale) / pixelsPerUnit;
            float yMin = (glyph.offset.y * glyphScale) / pixelsPerUnit;
            float xMax = xMin + width / pixelsPerUnit;
            float yMax = yMin + height / pixelsPerUnit;

            quads.Add(new FloatTextQuad(xMin, yMin, xMax, yMax, glyph.uvRect));
            cursor += GetAdvance(glyph) * glyphScale;
        }
    }

    private List<ResolvedGlyph> ResolveGlyphs(string text, FloatTextStyleId style)
    {
        List<ResolvedGlyph> result = new List<ResolvedGlyph>(text.Length);
        int index = 0;
        while (index < text.Length)
        {
            if (text.IndexOf("crit_icon", index, System.StringComparison.Ordinal) == index &&
                fontAsset.TryGetGlyph("crit_icon", FloatTextStyleId.Icon, out FloatTextGlyph iconGlyph))
            {
                result.Add(new ResolvedGlyph(iconGlyph));
                index += "crit_icon".Length;
                continue;
            }

            if (text.IndexOf("MISS", index, System.StringComparison.Ordinal) == index &&
                fontAsset.TryGetGlyph("MISS", FloatTextStyleId.Token, out FloatTextGlyph missGlyph))
            {
                result.Add(new ResolvedGlyph(missGlyph));
                index += "MISS".Length;
                continue;
            }

            string key = text[index].ToString();
            FloatTextStyleId glyphStyle = style;
            if (key == "+")
            {
                glyphStyle = FloatTextStyleId.Heal;
            }

            if (fontAsset.TryGetGlyph(key, glyphStyle, out FloatTextGlyph glyph))
            {
                result.Add(new ResolvedGlyph(glyph));
            }
            else
            {
                WarnMissingGlyph(key, glyphStyle);
            }

            index++;
        }

        return result;
    }

    private void EnsureFontAsset()
    {
        if (fontAsset != null)
        {
            return;
        }

#if UNITY_EDITOR
        fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<FloatTextFontAsset>("Assets/BakedSequences/FloatText/FloatTextFontAsset.asset");
#endif
    }

    private void ConfigureMeshPlayer()
    {
        if (MeshPlayer != null && fontAsset != null)
        {
            MeshPlayer.SetMaterial(fontAsset.material, fontAsset.atlas);
        }
    }

    private void DisableLegacyRenderer()
    {
        if (TryGetComponent<MeshPlayer>(out _))
        {
            return;
        }

        if (TryGetComponent<MeshRenderer>(out MeshRenderer legacyRenderer))
        {
            legacyRenderer.enabled = false;
        }

        if (TryGetComponent<MeshFilter>(out MeshFilter legacyFilter))
        {
            legacyFilter.sharedMesh = null;
        }
    }

    private static float GetAdvance(FloatTextGlyph glyph)
    {
        return glyph.advance > 0f ? glyph.advance : glyph.pixelSize.x;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void WarnMissingGlyph(string key, FloatTextStyleId style)
    {
        string warningKey = style + ":" + key;
        if (MissingGlyphWarnings.Add(warningKey))
        {
            Debug.LogWarning("Missing float text glyph: " + warningKey);
        }
    }

    private readonly struct FloatTextQuad
    {
        public FloatTextQuad(float xMin, float yMin, float xMax, float yMax, Vector4 uvRect)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
            UvRect = uvRect;
        }

        public float XMin { get; }
        public float YMin { get; }
        public float XMax { get; }
        public float YMax { get; }
        public Vector4 UvRect { get; }
    }

    private readonly struct ResolvedGlyph
    {
        public ResolvedGlyph(FloatTextGlyph glyph)
        {
            Glyph = glyph;
        }

        public FloatTextGlyph Glyph { get; }
    }
}
