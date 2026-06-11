using System.Collections.Generic;
using UniKit.Asset;
using UniKit.Asset.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Play.Battle.Rendering
{
    public sealed class GameObjectBattleRenderWorld : IBattleRenderWorld
    {
        private enum RenderEntryKind
        {
            Unit,
            Effect
        }

        private sealed class RenderEntry
        {
            public int handle;
            public RenderEntryKind kind;
            public string key;
            public Vector2 position;
            public float angleDeg;
            public string pendingUnitAction;
            public GameObject gameObject;
            public BattleUnitRenderController unit;
            public BattleEffectRenderController effect;
            public BakedAnimationVitPlayer unitPlayer;
            public BakedSequencePlayer sequence;
            public ParticleSystem particle;
            public int unitReturnIdleMs;
            public int unitDeathFadeDelayMs;
            public int unitDeathFadeElapsedMs;
            public bool unitDead;
            public bool fallback;
            public bool visible = true;
        }

        private const string DefaultUnitIdleAction = "idle";
        private const string DefaultUnitHitAction = "hit";
        private const string DefaultUnitDeadAction = "dead";
        private const string FloatTextFontAssetKey = "FloatTextFontAsset";
        private const int MaxFloatTextValue = 99999999;
        private const int MaxPendingFloatTextCount = 64;
        private const int UnitDeathFadeMs = 1000;
        private const float FallbackProjectileScale = 0.25f;

        private readonly Dictionary<int, RenderEntry> entries = new();
        private readonly List<FloatTextElement> activeFloatTexts = new();
        private readonly Stack<FloatTextElement> pooledFloatTexts = new();
        private readonly Queue<PendingFloatText> pendingFloatTexts = new();
        private readonly List<int> completedRenderHandles = new();
        private static Material fallbackProjectileMaterial;
        private static bool warnedMissingFloatTextFont;
        private GameObject floatTextRoot;
        private MeshPlayer floatTextMeshPlayer;
        private FloatTextFontAsset floatTextFontAsset;
        private bool requestedFloatTextFont;
        private bool paused;
        private int nextHandle = 1;

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

        public int SpawnUnit(string renderKey, Vector2 position)
        {
            int handle = Spawn(RenderEntryKind.Unit, renderKey, position, 0f);
            PlayUnitIdle(handle);
            return handle;
        }

        public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg)
        {
            int handle = Spawn(RenderEntryKind.Effect, projectileKey, position, angleDeg);
            if (entries.TryGetValue(handle, out RenderEntry entry))
            {
                entry.effect?.Play();
            }

            return handle;
        }

        public int PlayUnitAction(int renderHandle, string actionName)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry) || entry.kind != RenderEntryKind.Unit)
            {
                return 0;
            }

            entry.pendingUnitAction = actionName;
            return PlayUnitEntryAction(entry, string.IsNullOrEmpty(actionName) ? DefaultUnitIdleAction : actionName, false);
        }

        public void PlayUnitIdle(int renderHandle)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry) || entry.kind != RenderEntryKind.Unit)
            {
                return;
            }

            entry.pendingUnitAction = null;
            PlayUnitEntryAction(entry, DefaultUnitIdleAction, true);
        }

        public void PlayUnitHit(int renderHandle)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry) || entry.kind != RenderEntryKind.Unit)
            {
                return;
            }

            int durationMs = PlayUnitHitEntry(entry);
            if (!entry.unitDead)
            {
                entry.unitReturnIdleMs = Mathf.Max(1, durationMs);
            }
        }

        public void PlayUnitDead(int renderHandle)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry) || entry.kind != RenderEntryKind.Unit)
            {
                return;
            }

            entry.pendingUnitAction = null;
            entry.unitDead = true;
            entry.unitReturnIdleMs = 0;
            entry.unitDeathFadeElapsedMs = 0;
            entry.unitDeathFadeDelayMs = Mathf.Max(0, PlayUnitDeadEntry(entry));
            SetUnitAlpha(entry, 1f);
        }

        public void ShowDamageText(Vector2 worldPosition, long value)
        {
            ShowFloatText(worldPosition, value, FloatTextStyleId.Damage);
        }

        public void ShowHealText(Vector2 worldPosition, long value)
        {
            ShowFloatText(worldPosition, value, FloatTextStyleId.Heal);
        }

        public void SetPaused(bool paused)
        {
            this.paused = paused;
            for (int i = 0; i < activeFloatTexts.Count; i++)
            {
                if (activeFloatTexts[i] != null)
                {
                    activeFloatTexts[i].SetPaused(paused);
                }
            }
        }

        public void SetPosition(int renderHandle, Vector2 position)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.position = position;
            if (entry.gameObject != null)
            {
                entry.gameObject.transform.position = new Vector3(position.x, position.y, entry.gameObject.transform.position.z);
            }
        }

        public void SetRotation(int renderHandle, float angleDeg)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.angleDeg = angleDeg;
            if (entry.gameObject != null)
            {
                entry.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }

        public void SetVisible(int renderHandle, bool visible)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            entry.visible = visible;
            if (entry.gameObject != null)
            {
                entry.gameObject.SetActive(visible);
            }
        }

        public void Despawn(int renderHandle)
        {
            if (!entries.TryGetValue(renderHandle, out RenderEntry entry))
            {
                return;
            }

            Release(entry);
            entries.Remove(renderHandle);
        }

        public void Tick(float deltaTime)
        {
            if (paused)
            {
                return;
            }

            if (entries.Count > 0)
            {
                foreach (RenderEntry entry in entries.Values)
                {
                    if (entry.gameObject == null)
                    {
                        TryBind(entry);
                    }
                    else
                    {
                        TickUnitEntry(entry, deltaTime);
                    }
                }
            }

            ReleaseCompletedRenderEntries();
            RecycleCompletedFloatTexts();
        }

        public void Clear()
        {
            foreach (RenderEntry entry in entries.Values)
            {
                Release(entry);
            }

            entries.Clear();
            completedRenderHandles.Clear();
            activeFloatTexts.Clear();
            pooledFloatTexts.Clear();
            pendingFloatTexts.Clear();
            DestroyObject(floatTextRoot);
            floatTextRoot = null;
            floatTextMeshPlayer = null;
            requestedFloatTextFont = floatTextFontAsset != null;
        }

        private int Spawn(RenderEntryKind kind, string key, Vector2 position, float angleDeg)
        {
            int handle = nextHandle++;
            RenderEntry entry = new()
            {
                handle = handle,
                kind = kind,
                key = key,
                position = position,
                angleDeg = angleDeg
            };
            entries.Add(handle, entry);
            if (kind == RenderEntryKind.Effect && string.IsNullOrEmpty(key))
            {
                CreateFallbackProjectile(entry);
                return handle;
            }

            TryBind(entry);
            if (kind == RenderEntryKind.Effect && entry.gameObject == null)
            {
                CreateFallbackProjectile(entry);
            }

            return handle;
        }

        private void TryBind(RenderEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key))
            {
                return;
            }

            AssetPoolObjects poolObjects = GetPoolObjects();
            if (poolObjects == null)
            {
                return;
            }

            AssetPool pool = poolObjects.Find(entry.key);
            if (pool == null)
            {
                poolObjects.CreatePool(entry.key);
                return;
            }

            if (!pool.isLoading)
            {
                return;
            }

            AssetReference reference = pool.Get();
            if (reference == null || reference.gameObject == null)
            {
                return;
            }

            GameObject go = reference.gameObject;
            if (go == null)
            {
                return;
            }

            entry.gameObject = go;
            entry.unit = entry.kind == RenderEntryKind.Unit ? go.GetComponentInChildren<BattleUnitRenderController>(true) : null;
            entry.effect = entry.kind == RenderEntryKind.Effect ? go.GetComponentInChildren<BattleEffectRenderController>(true) : null;
            entry.unitPlayer = entry.kind == RenderEntryKind.Unit ? go.GetComponentInChildren<BakedAnimationVitPlayer>(true) : null;
            entry.sequence = entry.kind == RenderEntryKind.Effect ? go.GetComponentInChildren<BakedSequencePlayer>(true) : null;
            entry.particle = entry.kind == RenderEntryKind.Effect ? go.GetComponentInChildren<ParticleSystem>(true) : null;

            go.transform.position = new Vector3(entry.position.x, entry.position.y, go.transform.position.z);
            go.transform.rotation = Quaternion.Euler(0f, 0f, entry.angleDeg);
            go.SetActive(entry.visible);

            if (entry.kind == RenderEntryKind.Unit)
            {
                SetUnitAlpha(entry, 1f);
                if (entry.unitDead)
                {
                    entry.unitDeathFadeDelayMs = Mathf.Max(0, PlayUnitDeadEntry(entry));
                    entry.unitDeathFadeElapsedMs = 0;
                }
                else if (string.IsNullOrEmpty(entry.pendingUnitAction))
                {
                    PlayUnitEntryAction(entry, DefaultUnitIdleAction, true);
                }
                else
                {
                    PlayUnitEntryAction(entry, entry.pendingUnitAction, false);
                }
            }
            else
            {
                PlayEffectEntry(entry);
            }
        }

        private static AssetPoolObjects GetPoolObjects()
        {
            AssetPoolObjects poolObjects = AssetPoolObjects.Instance;
            return poolObjects != null ? poolObjects : null;
        }

        private static void Release(RenderEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            GameObject go = entry.gameObject;
            BattleEffectRenderController effect = entry.effect;
            bool fallback = entry.fallback;
            entry.gameObject = null;
            entry.unit = null;
            entry.effect = null;
            entry.unitPlayer = null;
            entry.sequence = null;
            entry.particle = null;
            entry.unitReturnIdleMs = 0;
            entry.unitDeathFadeDelayMs = 0;
            entry.unitDeathFadeElapsedMs = 0;
            entry.unitDead = false;
            entry.fallback = false;

            if (go == null)
            {
                return;
            }

            effect?.Stop();
            if (fallback)
            {
                DestroyObject(go);
            }
            else
            {
                go.Dispose();
            }
        }

        private static int PlayUnitEntryAction(RenderEntry entry, string actionName, bool loop)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.unit != null)
            {
                float duration = loop ? entry.unit.PlayIdle() : entry.unit.PlayAction(actionName);
                return SecondsToMilliseconds(duration);
            }

            return entry.unitPlayer != null ? SecondsToMilliseconds(entry.unitPlayer.Play(actionName, loop)) : 0;
        }

        private static int PlayUnitHitEntry(RenderEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.unit != null)
            {
                return SecondsToMilliseconds(entry.unit.PlayHit());
            }

            return entry.unitPlayer != null ? SecondsToMilliseconds(entry.unitPlayer.Play(DefaultUnitHitAction, false)) : 0;
        }

        private static int PlayUnitDeadEntry(RenderEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.unit != null)
            {
                return SecondsToMilliseconds(entry.unit.PlayDead());
            }

            return entry.unitPlayer != null ? SecondsToMilliseconds(entry.unitPlayer.Play(DefaultUnitDeadAction, false)) : 0;
        }

        private static int SecondsToMilliseconds(float seconds)
        {
            return seconds > 0f ? Mathf.CeilToInt(seconds * 1000f) : 0;
        }

        private void TickUnitEntry(RenderEntry entry, float deltaTime)
        {
            if (entry == null || entry.kind != RenderEntryKind.Unit)
            {
                return;
            }

            if (entry.unitDead)
            {
                TickUnitDeath(entry, deltaTime);
                return;
            }

            if (entry.unitReturnIdleMs <= 0)
            {
                return;
            }

            entry.unitReturnIdleMs = Mathf.Max(0, entry.unitReturnIdleMs - Mathf.CeilToInt(Mathf.Max(0f, deltaTime) * 1000f));
            if (entry.unitReturnIdleMs == 0)
            {
                PlayUnitEntryAction(entry, DefaultUnitIdleAction, true);
            }
        }

        private void TickUnitDeath(RenderEntry entry, float deltaTime)
        {
            int deltaMs = Mathf.CeilToInt(Mathf.Max(0f, deltaTime) * 1000f);
            if (entry.unitDeathFadeDelayMs > 0)
            {
                entry.unitDeathFadeDelayMs = Mathf.Max(0, entry.unitDeathFadeDelayMs - deltaMs);
                SetUnitAlpha(entry, 1f);
                return;
            }

            entry.unitDeathFadeElapsedMs = Mathf.Min(UnitDeathFadeMs, entry.unitDeathFadeElapsedMs + deltaMs);
            float alpha = 1f - Mathf.Clamp01(entry.unitDeathFadeElapsedMs / (float)UnitDeathFadeMs);
            SetUnitAlpha(entry, alpha);
            if (entry.unitDeathFadeElapsedMs < UnitDeathFadeMs)
            {
                return;
            }

            completedRenderHandles.Add(entry.handle);
        }

        private void ReleaseCompletedRenderEntries()
        {
            if (completedRenderHandles.Count == 0)
            {
                return;
            }

            for (int i = 0; i < completedRenderHandles.Count; i++)
            {
                int handle = completedRenderHandles[i];
                if (!entries.TryGetValue(handle, out RenderEntry entry))
                {
                    continue;
                }

                Release(entry);
                entries.Remove(handle);
            }

            completedRenderHandles.Clear();
        }

        private static void SetUnitAlpha(RenderEntry entry, float alpha)
        {
            if (entry == null || entry.kind != RenderEntryKind.Unit)
            {
                return;
            }

            if (entry.unit != null)
            {
                entry.unit.SetAlpha(alpha);
                return;
            }

            if (entry.unitPlayer == null)
            {
                return;
            }

            Color color = entry.unitPlayer.InstanceColor;
            color.a = Mathf.Clamp01(alpha);
            entry.unitPlayer.SetInstanceColor(color);
        }

        private static void PlayEffectEntry(RenderEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (entry.effect != null)
            {
                entry.effect.Play();
                return;
            }

            if (entry.sequence != null)
            {
                entry.sequence.Play();
                return;
            }

            entry.particle?.Play(true);
        }

        private static void CreateFallbackProjectile(RenderEntry entry)
        {
            if (entry == null || entry.kind != RenderEntryKind.Effect || entry.gameObject != null)
            {
                return;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Battle Projectile Fallback";
            go.hideFlags = HideFlags.DontSave;
            go.transform.position = new Vector3(entry.position.x, entry.position.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, entry.angleDeg);
            go.transform.localScale = Vector3.one * FallbackProjectileScale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetFallbackProjectileMaterial();
            }

            go.SetActive(entry.visible);
            entry.gameObject = go;
            entry.fallback = true;
        }

        private void ShowFloatText(Vector2 worldPosition, long value, FloatTextStyleId style)
        {
            if (value <= 0)
            {
                return;
            }

            int safeValue = Mathf.Clamp(value > int.MaxValue ? int.MaxValue : (int)value, 0, MaxFloatTextValue);
            if (!EnsureFloatTextRoot())
            {
                return;
            }

            if (GetFloatTextFontAsset() == null)
            {
                EnqueuePendingFloatText(worldPosition, safeValue, style);
                return;
            }

            PlayFloatText(worldPosition, safeValue, style);
        }

        private void PlayFloatText(Vector2 worldPosition, int value, FloatTextStyleId style)
        {
            FloatTextElement element = GetFloatTextElement();
            if (element == null)
            {
                return;
            }

            element.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            element.SetPaused(paused);
            if (style == FloatTextStyleId.Heal)
            {
                element.PlayHeal(value);
            }
            else
            {
                element.PlayDamage(value);
            }
        }

        private bool EnsureFloatTextRoot()
        {
            if (floatTextRoot == null)
            {
                floatTextRoot = new GameObject("Battle Float Text World")
                {
                    hideFlags = HideFlags.DontSave
                };
                floatTextMeshPlayer = floatTextRoot.AddComponent<MeshPlayer>();
            }

            if (floatTextMeshPlayer == null)
            {
                return false;
            }

            if (floatTextFontAsset != null)
            {
                floatTextMeshPlayer.SetMaterial(floatTextFontAsset.material, floatTextFontAsset.atlas);
            }

            return true;
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
            if (floatTextFontAsset == null || pendingFloatTexts.Count == 0 || !EnsureFloatTextRoot())
            {
                return;
            }

            while (pendingFloatTexts.Count > 0)
            {
                PendingFloatText pending = pendingFloatTexts.Dequeue();
                PlayFloatText(pending.position, pending.value, pending.style);
            }
        }

        private FloatTextElement GetFloatTextElement()
        {
            FloatTextElement element = null;
            while (pooledFloatTexts.Count > 0 && element == null)
            {
                element = pooledFloatTexts.Pop();
            }

            if (element == null)
            {
                GameObject go = new("Battle Float Text")
                {
                    hideFlags = HideFlags.DontSave
                };
                go.transform.SetParent(floatTextRoot.transform, false);
                element = go.AddComponent<FloatTextElement>();
            }

            element.gameObject.SetActive(true);
            element.Bind(floatTextFontAsset, floatTextMeshPlayer);
            activeFloatTexts.Add(element);
            return element;
        }

        private void RecycleCompletedFloatTexts()
        {
            for (int i = activeFloatTexts.Count - 1; i >= 0; i--)
            {
                FloatTextElement element = activeFloatTexts[i];
                if (element == null)
                {
                    activeFloatTexts.RemoveAt(i);
                    continue;
                }

                if (element.IsPlaying)
                {
                    continue;
                }

                activeFloatTexts.RemoveAt(i);
                element.Stop();
                element.gameObject.SetActive(false);
                pooledFloatTexts.Push(element);
            }
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
                        if (floatTextMeshPlayer != null && asset != null)
                        {
                            floatTextMeshPlayer.SetMaterial(asset.material, asset.atlas);
                        }

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

        private static void WarnMissingFloatTextFont()
        {
            if (warnedMissingFloatTextFont)
            {
                return;
            }

            warnedMissingFloatTextFont = true;
            Debug.LogWarning("[BattleRender] Missing FloatTextFontAsset, battle float text will be skipped.");
        }

        private static Material GetFallbackProjectileMaterial()
        {
            if (fallbackProjectileMaterial != null)
            {
                return fallbackProjectileMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            fallbackProjectileMaterial = new Material(shader)
            {
                name = "Battle Projectile Fallback Material",
                color = new Color(1f, 0.45f, 0.05f, 1f),
                hideFlags = HideFlags.DontSave
            };
            return fallbackProjectileMaterial;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
