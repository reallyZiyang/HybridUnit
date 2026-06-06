using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class FloatTextPlayer : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
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

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Mesh mesh;
    private readonly List<Vector3> vertices = new List<Vector3>(32);
    private readonly List<Vector2> uvs = new List<Vector2>(32);
    private readonly List<Color32> colors = new List<Color32>(32);
    private readonly List<int> indices = new List<int>(48);
    private Vector3 baseLocalPosition;
    private float elapsed;
    private bool playing;

    public bool IsPlaying => playing;
    public FloatTextFontAsset FontAsset => fontAsset;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        EnsureComponents();
        if (playOnEnable)
        {
            Play(previewText, previewStyle);
        }
    }

    private void OnDisable()
    {
        playing = false;
    }

    private void OnValidate()
    {
        EnsureComponents();
        if (!Application.isPlaying && fontAsset != null)
        {
            BuildMesh(previewText, previewStyle);
            ApplyAlpha(1f);
        }
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        Tick(Application.isPlaying ? Time.deltaTime : Time.unscaledDeltaTime);
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
        EnsureComponents();
        BuildMesh(text, style);
        baseLocalPosition = transform.localPosition;
        elapsed = 0f;
        playing = mesh != null && mesh.vertexCount > 0;
        if (meshRenderer != null)
        {
            meshRenderer.enabled = playing;
        }

        ApplyAlpha(1f);
        ApplyMotion(0f);
    }

    public void Stop()
    {
        playing = false;
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
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

    private void Tick(float deltaTime)
    {
        elapsed += Mathf.Max(0f, deltaTime);
        float normalizedTime = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;
        ApplyMotion(normalizedTime);
        ApplyAlpha(CalculateAlpha(normalizedTime));

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
            punch = Mathf.Lerp(punchScale, 1f, Smooth01(punchT));
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

    private void ApplyAlpha(float alpha)
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.Clear();
        Color instanceColor = color;
        instanceColor.a *= Mathf.Clamp01(alpha);
        propertyBlock.SetColor(InstanceColorId, instanceColor);
        if (fontAsset != null && fontAsset.atlas != null)
        {
            propertyBlock.SetTexture(MainTexId, fontAsset.atlas);
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void BuildMesh(string text, FloatTextStyleId style)
    {
        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        indices.Clear();

        if (fontAsset == null || string.IsNullOrEmpty(text))
        {
            AssignMesh();
            return;
        }

        List<ResolvedGlyph> resolvedGlyphs = ResolveGlyphs(text, style);
        if (resolvedGlyphs.Count == 0)
        {
            AssignMesh();
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

            AddQuad(xMin, yMin, xMax, yMax, glyph.uvRect);
            cursor += GetAdvance(glyph) * glyphScale;
        }

        AssignMesh();
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

    private void AddQuad(float xMin, float yMin, float xMax, float yMax, Vector4 uvRect)
    {
        int vertexStart = vertices.Count;
        vertices.Add(new Vector3(xMin, yMin, 0f));
        vertices.Add(new Vector3(xMax, yMin, 0f));
        vertices.Add(new Vector3(xMax, yMax, 0f));
        vertices.Add(new Vector3(xMin, yMax, 0f));

        float uMin = uvRect.x;
        float uMax = uvRect.x + uvRect.z;
        float vMin = uvRect.y;
        float vMax = uvRect.y + uvRect.w;
        uvs.Add(new Vector2(uMin, vMin));
        uvs.Add(new Vector2(uMax, vMin));
        uvs.Add(new Vector2(uMax, vMax));
        uvs.Add(new Vector2(uMin, vMax));

        colors.Add(Color.white);
        colors.Add(Color.white);
        colors.Add(Color.white);
        colors.Add(Color.white);

        indices.Add(vertexStart);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 1);
        indices.Add(vertexStart);
        indices.Add(vertexStart + 3);
        indices.Add(vertexStart + 2);
    }

    private void AssignMesh()
    {
        EnsureOwnedMesh();
        mesh.Clear();
        if (vertices.Count > 0)
        {
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
        }

        meshFilter.sharedMesh = mesh;
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

        if (fontAsset != null && fontAsset.material != null)
        {
            fontAsset.material.enableInstancing = true;
            if (fontAsset.atlas != null && fontAsset.material.HasProperty(MainTexId))
            {
                fontAsset.material.SetTexture(MainTexId, fontAsset.atlas);
            }

            meshRenderer.sharedMaterial = fontAsset.material;
        }

        EnsureOwnedMesh();
        if (meshFilter.sharedMesh != mesh)
        {
            meshFilter.sharedMesh = mesh;
        }
    }

    private void EnsureOwnedMesh()
    {
        if (mesh != null)
        {
            return;
        }

        mesh = new Mesh
        {
            name = "Float Text Mesh"
        };
        mesh.MarkDynamic();
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

    private readonly struct ResolvedGlyph
    {
        public ResolvedGlyph(FloatTextGlyph glyph)
        {
            Glyph = glyph;
        }

        public FloatTextGlyph Glyph { get; }
    }
}
