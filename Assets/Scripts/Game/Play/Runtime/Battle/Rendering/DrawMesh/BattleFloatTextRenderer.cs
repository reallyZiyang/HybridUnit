using System.Collections.Generic;
using UniKit.Asset;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Play.Battle.Rendering
{
    internal sealed class BattleFloatTextRenderer
    {
        private const string FloatTextFontAssetKey = "FloatTextFontAsset";
        private const int MaxFloatTextValue = 99999999;
        private const int MaxPendingFloatTextCount = 64;
        private const float Lifetime = 0.8f;
        private const float PunchDuration = 0.12f;
        private const float PunchScale = 1.25f;
        private const float FloatDistance = 0.8f;
        private const float FadeStart = 0.65f;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
        private static readonly HashSet<string> MissingGlyphWarnings = new();
        private static bool warnedMissingFloatTextFont;

        private readonly List<FloatTextInstance> instances = new(64);
        private readonly Stack<int> freeIndices = new(64);
        private readonly Queue<PendingFloatText> pendingFloatTexts = new();
        private readonly MeshQuadWriter writer = new();
        private readonly MaterialPropertyBlock propertyBlock = new();

        private FloatTextFontAsset floatTextFontAsset;
        private Mesh mesh;
        private bool requestedFloatTextFont;
        private bool paused;

        private readonly struct PendingFloatText
        {
            public readonly Vector2 position;
            public readonly int value;
            public readonly FloatTextStyleId style;

            public PendingFloatText(Vector2 position, int value, FloatTextStyleId style)
            {
                this.position = position;
                this.value = value;
                this.style = style;
            }
        }

        private sealed class FloatTextInstance
        {
            public readonly List<FloatTextQuad> quads = new(16);
            public bool active;
            public Vector2 position;
            public float elapsed;
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

        public void SetPaused(bool paused)
        {
            this.paused = paused;
        }

        public void ShowDamageText(Vector2 worldPosition, long value)
        {
            ShowFloatText(worldPosition, value, FloatTextStyleId.Damage);
        }

        public void ShowHealText(Vector2 worldPosition, long value)
        {
            ShowFloatText(worldPosition, value, FloatTextStyleId.Heal);
        }

        public void Tick(float deltaTime)
        {
            if (paused)
            {
                return;
            }

            float safeDelta = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < instances.Count; i++)
            {
                FloatTextInstance instance = instances[i];
                if (instance == null || !instance.active)
                {
                    continue;
                }

                instance.elapsed += safeDelta;
                if (instance.elapsed >= Lifetime)
                {
                    Release(i);
                }
            }
        }

        public void Draw()
        {
            if (floatTextFontAsset == null || floatTextFontAsset.material == null || floatTextFontAsset.atlas == null)
            {
                return;
            }

            EnsureMesh();
            writer.Begin(Matrix4x4.identity);
            for (int i = 0; i < instances.Count; i++)
            {
                FloatTextInstance instance = instances[i];
                if (instance != null && instance.active)
                {
                    WriteInstance(instance);
                }
            }

            writer.ApplyTo(mesh);
            if (writer.VertexCount <= 0)
            {
                return;
            }

            Material material = floatTextFontAsset.material;
            material.enableInstancing = true;
            if (material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, floatTextFontAsset.atlas);
            }

            propertyBlock.Clear();
            propertyBlock.SetTexture(MainTexId, floatTextFontAsset.atlas);
            propertyBlock.SetColor(InstanceColorId, Color.white);
            Graphics.DrawMesh(
                mesh,
                Matrix4x4.identity,
                material,
                0,
                null,
                0,
                propertyBlock,
                ShadowCastingMode.Off,
                false);
        }

        public void Clear()
        {
            instances.Clear();
            freeIndices.Clear();
            pendingFloatTexts.Clear();
            if (mesh != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(mesh);
                }
                else
                {
                    Object.DestroyImmediate(mesh);
                }

                mesh = null;
            }

            requestedFloatTextFont = floatTextFontAsset != null;
        }

        private void ShowFloatText(Vector2 worldPosition, long value, FloatTextStyleId style)
        {
            if (value <= 0)
            {
                return;
            }

            int safeValue = Mathf.Clamp(value > int.MaxValue ? int.MaxValue : (int)value, 0, MaxFloatTextValue);
            if (GetFloatTextFontAsset() == null)
            {
                EnqueuePendingFloatText(worldPosition, safeValue, style);
                return;
            }

            PlayFloatText(worldPosition, safeValue, style);
        }

        private void PlayFloatText(Vector2 worldPosition, int value, FloatTextStyleId style)
        {
            string text = style == FloatTextStyleId.Heal
                ? "+" + Mathf.Abs(value)
                : Mathf.Abs(value).ToString();
            int index = Allocate();
            FloatTextInstance instance = instances[index];
            instance.position = worldPosition;
            instance.elapsed = 0f;
            instance.active = true;
            BuildQuads(instance.quads, text, style);
            if (instance.quads.Count == 0)
            {
                Release(index);
            }
        }

        private int Allocate()
        {
            int index = freeIndices.Count > 0 ? freeIndices.Pop() : instances.Count;
            if (index < instances.Count)
            {
                return index;
            }

            instances.Add(new FloatTextInstance());
            return index;
        }

        private void Release(int index)
        {
            if (index < 0 || index >= instances.Count)
            {
                return;
            }

            FloatTextInstance instance = instances[index];
            if (instance == null || !instance.active)
            {
                return;
            }

            instance.active = false;
            instance.quads.Clear();
            freeIndices.Push(index);
        }

        private void WriteInstance(FloatTextInstance instance)
        {
            float normalizedTime = Lifetime > 0f ? Mathf.Clamp01(instance.elapsed / Lifetime) : 1f;
            float punch = 1f;
            if (PunchDuration > 0f && instance.elapsed < PunchDuration)
            {
                float punchT = Mathf.Clamp01(instance.elapsed / PunchDuration);
                punch = Mathf.Lerp(Mathf.Max(0.0001f, PunchScale), 1f, Smooth01(punchT));
            }

            float alpha = CalculateAlpha(normalizedTime);
            Color32 vertexColor = new Color(1f, 1f, 1f, alpha);
            Vector3 position = new(instance.position.x, instance.position.y + FloatDistance * Smooth01(normalizedTime), 0f);
            Matrix4x4 localToWorld = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * punch);
            for (int i = 0; i < instance.quads.Count; i++)
            {
                FloatTextQuad quad = instance.quads[i];
                writer.AddQuad(localToWorld, quad.XMin, quad.YMin, quad.XMax, quad.YMax, quad.UvRect, vertexColor);
            }
        }

        private void BuildQuads(List<FloatTextQuad> quads, string text, FloatTextStyleId style)
        {
            quads.Clear();
            if (floatTextFontAsset == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            List<ResolvedGlyph> resolvedGlyphs = ResolveGlyphs(text, style);
            if (resolvedGlyphs.Count == 0)
            {
                return;
            }

            float pixelsPerUnit = Mathf.Max(0.0001f, floatTextFontAsset.pixelsPerUnit);
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
            List<ResolvedGlyph> result = new(text.Length);
            int index = 0;
            while (index < text.Length)
            {
                if (text.IndexOf("crit_icon", index, System.StringComparison.Ordinal) == index &&
                    floatTextFontAsset.TryGetGlyph("crit_icon", FloatTextStyleId.Icon, out FloatTextGlyph iconGlyph))
                {
                    result.Add(new ResolvedGlyph(iconGlyph));
                    index += "crit_icon".Length;
                    continue;
                }

                if (text.IndexOf("MISS", index, System.StringComparison.Ordinal) == index &&
                    floatTextFontAsset.TryGetGlyph("MISS", FloatTextStyleId.Token, out FloatTextGlyph missGlyph))
                {
                    result.Add(new ResolvedGlyph(missGlyph));
                    index += "MISS".Length;
                    continue;
                }

                string key = text[index].ToString();
                FloatTextStyleId glyphStyle = key == "+" ? FloatTextStyleId.Heal : style;
                if (floatTextFontAsset.TryGetGlyph(key, glyphStyle, out FloatTextGlyph glyph))
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

        private FloatTextFontAsset GetFloatTextFontAsset()
        {
            if (floatTextFontAsset != null)
            {
                return floatTextFontAsset;
            }

            if (AssetManager.ContainsAsset(FloatTextFontAssetKey)
                && AssetManager.TryGetAsset(FloatTextFontAssetKey, out FloatTextFontAsset cachedAsset))
            {
                floatTextFontAsset = cachedAsset;
                FlushPendingFloatTexts();
                return floatTextFontAsset;
            }

            if (!requestedFloatTextFont)
            {
                requestedFloatTextFont = true;
                AssetManager.LoadAssetDelegate<FloatTextFontAsset>(
                    FloatTextFontAssetKey,
                    (_, asset) =>
                    {
                        floatTextFontAsset = asset;
                        FlushPendingFloatTexts();
                    },
                    _ =>
                    {
                        pendingFloatTexts.Clear();
                        WarnMissingFloatTextFont();
                    });
            }

            return floatTextFontAsset;
        }

        private void EnqueuePendingFloatText(Vector2 worldPosition, int value, FloatTextStyleId style)
        {
            while (pendingFloatTexts.Count >= MaxPendingFloatTextCount)
            {
                pendingFloatTexts.Dequeue();
            }

            pendingFloatTexts.Enqueue(new PendingFloatText(worldPosition, value, style));
        }

        private void FlushPendingFloatTexts()
        {
            if (floatTextFontAsset == null || pendingFloatTexts.Count == 0)
            {
                return;
            }

            while (pendingFloatTexts.Count > 0)
            {
                PendingFloatText pending = pendingFloatTexts.Dequeue();
                PlayFloatText(pending.position, pending.value, pending.style);
            }
        }

        private void EnsureMesh()
        {
            if (mesh != null)
            {
                return;
            }

            mesh = new Mesh
            {
                name = "Battle Float Text Mesh",
                hideFlags = HideFlags.DontSave
            };
            mesh.MarkDynamic();
        }

        private static float GetAdvance(FloatTextGlyph glyph)
        {
            return glyph.advance > 0f ? glyph.advance : glyph.pixelSize.x;
        }

        private static float CalculateAlpha(float normalizedTime)
        {
            if (normalizedTime <= FadeStart)
            {
                return 1f;
            }

            float fadeT = Mathf.InverseLerp(FadeStart, 1f, normalizedTime);
            return 1f - Smooth01(fadeT);
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

        private static void WarnMissingFloatTextFont()
        {
            if (warnedMissingFloatTextFont)
            {
                return;
            }

            warnedMissingFloatTextFont = true;
            Debug.LogWarning("[BattleRender] Missing FloatTextFontAsset, battle float text will be skipped.");
        }
    }
}
