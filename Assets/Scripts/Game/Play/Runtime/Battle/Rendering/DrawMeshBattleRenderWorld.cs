using Game.Play.Rendering.Runtime;
using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public sealed class DrawMeshBattleRenderWorld : IBattleRenderWorld
    {
        private const int InvalidHandle = -1;

        private readonly BattleRenderEntry[] unitEntries = new BattleRenderEntry[BattleRenderCapacityConfig.UnitVitCapacity];
        private readonly UnitDrawRenderState[] unitStates = new UnitDrawRenderState[BattleRenderCapacityConfig.UnitVitCapacity];
        private readonly int[] unitFreeSlots = new int[BattleRenderCapacityConfig.UnitVitCapacity];

        private readonly BattleRenderEntry[] effectEntries = new BattleRenderEntry[BattleRenderCapacityConfig.Effect2DCapacity];
        private readonly EffectDrawRenderState[] effectStates = new EffectDrawRenderState[BattleRenderCapacityConfig.Effect2DCapacity];
        private readonly int[] effectFreeSlots = new int[BattleRenderCapacityConfig.Effect2DCapacity];

        private readonly BattleRenderEntry[] meshElementEntries = new BattleRenderEntry[BattleRenderCapacityConfig.MeshElementCapacity];
        private readonly int[] meshElementFreeSlots = new int[BattleRenderCapacityConfig.MeshElementCapacity];

        private readonly int[] completedRenderHandles = new int[BattleRenderCapacityConfig.TotalCapacity];
        private readonly BattleDrawMeshInstanceManager drawMeshInstances = new();
        private readonly BattleRenderAssetLoader assetLoader;
        private readonly BattleFloatTextRenderer floatTextRenderer = new();
        private readonly DrawMeshUnitRenderer unitRenderer;
        private readonly DrawMeshEffectRenderer effectRenderer;
        private readonly DrawMeshRenderAssetBinder assetBinder;

        private GameObject drawMeshRoot;
        private BattleDrawMeshInstanceRenderHost drawMeshRenderHost;
        private bool paused;
        private int unitFreeCount;
        private int effectFreeCount;
        private int meshElementFreeCount;
        private int activeCount;
        private int completedRenderHandleCount;
        private bool warnedUnitCapacityExceeded;
        private bool warnedEffectCapacityExceeded;
        private bool warnedMeshElementCapacityExceeded;
        private bool warnedCompletedQueueFull;

        public DrawMeshBattleRenderWorld()
        {
            InitializePools();
            assetLoader = new BattleRenderAssetLoader(ResolveEntry, BindRenderAsset);
            unitRenderer = new DrawMeshUnitRenderer(drawMeshInstances, EnsureDrawMeshRenderHost, MarkCompleted);
            effectRenderer = new DrawMeshEffectRenderer(drawMeshInstances, EnsureDrawMeshRenderHost);
            assetBinder = new DrawMeshRenderAssetBinder(unitRenderer, effectRenderer);
        }

        public int SpawnUnit(string renderKey, Vector2 position)
        {
            int handle = Spawn(BattleRenderEntryKind.Unit, renderKey, BattleDrawMeshRenderLayer.Unit, position, 0f);
            PlayUnitIdle(handle);
            return handle;
        }

        public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg)
        {
            return Spawn(BattleRenderEntryKind.Effect, projectileKey, BattleDrawMeshRenderLayer.Projectile, position, angleDeg);
        }

        public int PlayUnitAction(int renderHandle, string actionName)
        {
            if (!TryGetUnit(renderHandle, out BattleRenderEntry entry, out UnitDrawRenderState state))
            {
                return 0;
            }

            state.pendingAction = actionName;
            string safeAction = string.IsNullOrEmpty(actionName) ? DrawMeshUnitRenderer.IdleAction : actionName;
            return unitRenderer.PlayAction(entry, state, safeAction, false);
        }

        public void PlayUnitIdle(int renderHandle)
        {
            if (!TryGetUnit(renderHandle, out BattleRenderEntry entry, out UnitDrawRenderState state))
            {
                return;
            }

            state.pendingAction = null;
            unitRenderer.PlayLoopOrIdle(entry, state, DrawMeshUnitRenderer.IdleAction);
        }

        public void PlayUnitWalk(int renderHandle)
        {
            if (!TryGetUnit(renderHandle, out BattleRenderEntry entry, out UnitDrawRenderState state))
            {
                return;
            }

            state.pendingAction = null;
            unitRenderer.PlayLoopOrIdle(entry, state, DrawMeshUnitRenderer.WalkAction);
        }

        public int PlayUnitHit(int renderHandle)
        {
            if (!TryGetUnit(renderHandle, out BattleRenderEntry entry, out UnitDrawRenderState state))
            {
                return DrawMeshUnitRenderer.DefaultHitLockMs;
            }

            int durationMs = unitRenderer.PlayHitOrIdle(entry, state);
            if (!state.dead)
            {
                state.returnIdleMs = Mathf.Max(1, durationMs);
            }

            return Mathf.Max(1, durationMs);
        }

        public void PlayUnitDead(int renderHandle)
        {
            if (!TryGetUnit(renderHandle, out BattleRenderEntry entry, out UnitDrawRenderState state))
            {
                return;
            }

            state.pendingAction = null;
            state.dead = true;
            state.returnIdleMs = 0;
            state.deathFadeElapsedMs = 0;
            state.deathFadeDelayMs = Mathf.Max(0, unitRenderer.PlayAction(entry, state, DrawMeshUnitRenderer.DeadAction, false));
            unitRenderer.SetAlpha(entry, state, 1f);
        }

        public void ShowDamageText(Vector2 worldPosition, long value)
        {
            floatTextRenderer.ShowDamageText(worldPosition, value);
        }

        public void ShowHealText(Vector2 worldPosition, long value)
        {
            floatTextRenderer.ShowHealText(worldPosition, value);
        }

        public void SetPaused(bool paused)
        {
            this.paused = paused;
            floatTextRenderer.SetPaused(paused);
        }

        public void SetSortingGrid(float gridMinY, float cellSize)
        {
            drawMeshInstances.SetUnitSortingGrid(gridMinY, cellSize);
        }

        public void SetPosition(int renderHandle, Vector2 position)
        {
            if (!TryGetEntry(renderHandle, out BattleRenderEntry entry))
            {
                return;
            }

            entry.position = position;
            if (entry.instanceHandle.IsValid)
            {
                drawMeshInstances.SetPosition(entry.instanceHandle, new Vector3(position.x, position.y, 0f));
            }
        }

        public void SetRotation(int renderHandle, float angleDeg)
        {
            if (!TryGetEntry(renderHandle, out BattleRenderEntry entry))
            {
                return;
            }

            entry.angleDeg = angleDeg;
            if (entry.instanceHandle.IsValid)
            {
                drawMeshInstances.SetRotation(entry.instanceHandle, Quaternion.Euler(0f, 0f, angleDeg));
            }
        }

        public void SetUnitFlipX(int renderHandle, bool flipX)
        {
            if (!TryGetUnit(renderHandle, out BattleRenderEntry entry, out _))
            {
                return;
            }

            unitRenderer.SetFlipX(entry, flipX);
        }

        public void SetVisible(int renderHandle, bool visible)
        {
            if (!TryGetEntry(renderHandle, out BattleRenderEntry entry))
            {
                return;
            }

            entry.visible = visible;
            if (entry.instanceHandle.IsValid)
            {
                drawMeshInstances.SetVisible(entry.instanceHandle, visible);
            }
        }

        public void Despawn(int renderHandle)
        {
            if (TryGetEntry(renderHandle, out BattleRenderEntry entry))
            {
                FreeEntry(entry);
            }
        }

        public void Tick(float deltaTime)
        {
            if (!paused && activeCount > 0)
            {
                TickUnitEntries(deltaTime);
                TickEffectEntries(deltaTime);
            }

            ReleaseCompletedRenderEntries();
            floatTextRenderer.Tick(deltaTime);
            SubmitDrawMeshInstances();
            floatTextRenderer.Draw();
        }

        public void Clear()
        {
            ResetPool(unitEntries, unitFreeSlots, ref unitFreeCount, BattleRenderSegment.UnitVit);
            ResetPool(effectEntries, effectFreeSlots, ref effectFreeCount, BattleRenderSegment.Effect2D);
            ResetPool(meshElementEntries, meshElementFreeSlots, ref meshElementFreeCount, BattleRenderSegment.MeshElement);
            ResetUnitStates();
            ResetEffectStates();
            activeCount = 0;
            completedRenderHandleCount = 0;
            drawMeshInstances.Clear();
            BattleRenderObjectUtility.DestroyObject(drawMeshRoot);
            drawMeshRoot = null;
            drawMeshRenderHost = null;
            assetLoader.ClearPending();
            floatTextRenderer.Clear();
        }

        private int Spawn(BattleRenderEntryKind kind, string key, BattleDrawMeshRenderLayer renderLayer, Vector2 position, float angleDeg)
        {
            BattleRenderSegment segment = kind == BattleRenderEntryKind.Unit
                ? BattleRenderSegment.UnitVit
                : BattleRenderSegment.Effect2D;

            if (!TryAllocateEntry(segment, kind, key, renderLayer, position, angleDeg, out BattleRenderEntry entry))
            {
                WarnCapacityExceeded(segment, key);
                return InvalidHandle;
            }

            ResetStateForEntry(entry);
            if (string.IsNullOrEmpty(key))
            {
                effectRenderer.CreateFallback(entry);
            }

            return entry.handle;
        }

        private void TickUnitEntries(float deltaTime)
        {
            for (int i = 0; i < unitEntries.Length; i++)
            {
                BattleRenderEntry entry = unitEntries[i];
                if (!entry.active)
                {
                    continue;
                }

                EnsureEntryBound(entry);
                unitRenderer.Tick(entry, unitStates[i], deltaTime);
            }
        }

        private void TickEffectEntries(float deltaTime)
        {
            for (int i = 0; i < effectEntries.Length; i++)
            {
                BattleRenderEntry entry = effectEntries[i];
                if (!entry.active)
                {
                    continue;
                }

                EnsureEntryBound(entry);
                effectRenderer.Tick(entry, effectStates[i], deltaTime);
            }
        }

        private void EnsureEntryBound(BattleRenderEntry entry)
        {
            if (entry == null || !entry.active || entry.fallback || entry.instanceHandle.IsValid)
            {
                return;
            }

            if (!entry.assetRequestStarted)
            {
                assetLoader.Request(entry);
            }

            if (!entry.instanceHandle.IsValid && entry.assetRequestCompleted)
            {
                effectRenderer.CreateFallback(entry);
            }
        }

        private void BindRenderAsset(BattleRenderEntry entry, BattleRenderAssetBase asset)
        {
            UnitDrawRenderState unitState = entry.segment == BattleRenderSegment.UnitVit ? unitStates[entry.slot] : null;
            EffectDrawRenderState effectState = entry.segment == BattleRenderSegment.Effect2D ? effectStates[entry.slot] : null;
            assetBinder.Bind(entry, asset, unitState, effectState);
        }

        private BattleRenderEntry ResolveEntry(int renderHandle, string key)
        {
            return TryGetEntry(renderHandle, out BattleRenderEntry entry) && entry.key == key ? entry : null;
        }

        private bool TryGetUnit(int renderHandle, out BattleRenderEntry entry, out UnitDrawRenderState state)
        {
            if (!TryGetEntry(renderHandle, out entry)
                || entry.kind != BattleRenderEntryKind.Unit
                || entry.segment != BattleRenderSegment.UnitVit)
            {
                entry = null;
                state = null;
                return false;
            }

            state = unitStates[entry.slot];
            return true;
        }

        private bool TryGetEntry(int renderHandle, out BattleRenderEntry entry)
        {
            entry = null;
            if (!TryDecodeHandle(renderHandle, out BattleRenderSegment segment, out int slot, out int generation))
            {
                return false;
            }

            BattleRenderEntry[] entries = GetEntries(segment);
            if (entries == null || slot < 0 || slot >= entries.Length)
            {
                return false;
            }

            BattleRenderEntry candidate = entries[slot];
            if (candidate == null
                || !candidate.active
                || candidate.generation != generation
                || candidate.handle != renderHandle)
            {
                return false;
            }

            entry = candidate;
            return true;
        }

        private void MarkCompleted(int renderHandle)
        {
            if (!TryGetEntry(renderHandle, out _))
            {
                return;
            }

            for (int i = 0; i < completedRenderHandleCount; i++)
            {
                if (completedRenderHandles[i] == renderHandle)
                {
                    return;
                }
            }

            if (completedRenderHandleCount >= completedRenderHandles.Length)
            {
                WarnCompletedQueueFull();
                return;
            }

            completedRenderHandles[completedRenderHandleCount++] = renderHandle;
        }

        private void ReleaseCompletedRenderEntries()
        {
            if (completedRenderHandleCount == 0)
            {
                return;
            }

            for (int i = 0; i < completedRenderHandleCount; i++)
            {
                if (TryGetEntry(completedRenderHandles[i], out BattleRenderEntry entry))
                {
                    FreeEntry(entry);
                }
            }

            completedRenderHandleCount = 0;
        }

        private void FreeEntry(BattleRenderEntry entry)
        {
            if (entry == null || !entry.active)
            {
                return;
            }

            BattleRenderSegment segment = entry.segment;
            int slot = entry.slot;
            Release(entry);
            entry.generation = NextGeneration(entry.generation);
            entry.ResetForDespawn();
            entry.segment = segment;
            entry.slot = slot;
            PushFreeSlot(segment, slot);
            activeCount = Mathf.Max(0, activeCount - 1);
        }

        private void Release(BattleRenderEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (entry.instanceHandle.IsValid)
            {
                drawMeshInstances.Despawn(entry.instanceHandle);
            }

            entry.instanceHandle = BattleDrawMeshInstanceHandle.Invalid;
            entry.assetRequestStarted = false;
            entry.assetRequestCompleted = false;
            entry.backend = BattleRenderBackend.None;
            entry.fallback = false;
        }

        private bool TryAllocateEntry(
            BattleRenderSegment segment,
            BattleRenderEntryKind kind,
            string key,
            BattleDrawMeshRenderLayer renderLayer,
            Vector2 position,
            float angleDeg,
            out BattleRenderEntry entry)
        {
            entry = null;
            if (!TryPopFreeSlot(segment, out int slot))
            {
                return false;
            }

            BattleRenderEntry[] entries = GetEntries(segment);
            entry = entries[slot];
            int generation = entry.generation <= 0 ? 1 : entry.generation;
            int handle = EncodeHandle(segment, slot, generation);
            entry.ResetForSpawn(handle, slot, generation, segment, kind, key, renderLayer, position, angleDeg);
            activeCount++;
            return true;
        }

        private void ResetStateForEntry(BattleRenderEntry entry)
        {
            if (entry.segment == BattleRenderSegment.UnitVit)
            {
                unitStates[entry.slot].Reset();
            }
            else if (entry.segment == BattleRenderSegment.Effect2D)
            {
                effectStates[entry.slot].Reset();
            }
        }

        private void InitializePools()
        {
            InitializeEntryPool(unitEntries, unitFreeSlots, ref unitFreeCount, BattleRenderSegment.UnitVit);
            InitializeEntryPool(effectEntries, effectFreeSlots, ref effectFreeCount, BattleRenderSegment.Effect2D);
            InitializeEntryPool(meshElementEntries, meshElementFreeSlots, ref meshElementFreeCount, BattleRenderSegment.MeshElement);

            for (int i = 0; i < unitStates.Length; i++)
            {
                unitStates[i] = new UnitDrawRenderState();
            }

            for (int i = 0; i < effectStates.Length; i++)
            {
                effectStates[i] = new EffectDrawRenderState();
            }
        }

        private static void InitializeEntryPool(
            BattleRenderEntry[] entries,
            int[] freeSlots,
            ref int freeCount,
            BattleRenderSegment segment)
        {
            freeCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new BattleRenderEntry
                {
                    active = false,
                    slot = i,
                    generation = 1,
                    segment = segment,
                    handle = InvalidHandle,
                    instanceHandle = BattleDrawMeshInstanceHandle.Invalid,
                    visible = true
                };
            }

            FillFreeSlots(freeSlots, ref freeCount);
        }

        private void ResetPool(
            BattleRenderEntry[] entries,
            int[] freeSlots,
            ref int freeCount,
            BattleRenderSegment segment)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                BattleRenderEntry entry = entries[i];
                if (entry.active)
                {
                    Release(entry);
                }

                entry.generation = NextGeneration(entry.generation);
                entry.ResetForDespawn();
                entry.segment = segment;
                entry.slot = i;
            }

            FillFreeSlots(freeSlots, ref freeCount);
        }

        private static void FillFreeSlots(int[] freeSlots, ref int freeCount)
        {
            freeCount = 0;
            for (int i = freeSlots.Length - 1; i >= 0; i--)
            {
                freeSlots[freeCount++] = i;
            }
        }

        private void ResetUnitStates()
        {
            for (int i = 0; i < unitStates.Length; i++)
            {
                unitStates[i].Reset();
            }
        }

        private void ResetEffectStates()
        {
            for (int i = 0; i < effectStates.Length; i++)
            {
                effectStates[i].Reset();
            }
        }

        private bool TryPopFreeSlot(BattleRenderSegment segment, out int slot)
        {
            slot = -1;
            switch (segment)
            {
                case BattleRenderSegment.UnitVit:
                    if (unitFreeCount <= 0)
                    {
                        return false;
                    }

                    slot = unitFreeSlots[--unitFreeCount];
                    return true;
                case BattleRenderSegment.Effect2D:
                    if (effectFreeCount <= 0)
                    {
                        return false;
                    }

                    slot = effectFreeSlots[--effectFreeCount];
                    return true;
                case BattleRenderSegment.MeshElement:
                    if (meshElementFreeCount <= 0)
                    {
                        return false;
                    }

                    slot = meshElementFreeSlots[--meshElementFreeCount];
                    return true;
                default:
                    return false;
            }
        }

        private void PushFreeSlot(BattleRenderSegment segment, int slot)
        {
            switch (segment)
            {
                case BattleRenderSegment.UnitVit:
                    unitFreeSlots[unitFreeCount++] = slot;
                    break;
                case BattleRenderSegment.Effect2D:
                    effectFreeSlots[effectFreeCount++] = slot;
                    break;
                case BattleRenderSegment.MeshElement:
                    meshElementFreeSlots[meshElementFreeCount++] = slot;
                    break;
            }
        }

        private BattleRenderEntry[] GetEntries(BattleRenderSegment segment)
        {
            return segment switch
            {
                BattleRenderSegment.UnitVit => unitEntries,
                BattleRenderSegment.Effect2D => effectEntries,
                BattleRenderSegment.MeshElement => meshElementEntries,
                _ => null
            };
        }

        private static int EncodeHandle(BattleRenderSegment segment, int slot, int generation)
        {
            return (generation << (BattleRenderCapacityConfig.SegmentBits + BattleRenderCapacityConfig.SlotBits))
                | ((int)segment << BattleRenderCapacityConfig.SlotBits)
                | slot;
        }

        private static bool TryDecodeHandle(int handle, out BattleRenderSegment segment, out int slot, out int generation)
        {
            segment = BattleRenderSegment.None;
            slot = -1;
            generation = 0;
            if (handle <= 0)
            {
                return false;
            }

            slot = handle & BattleRenderCapacityConfig.SlotMask;
            segment = (BattleRenderSegment)((handle >> BattleRenderCapacityConfig.SlotBits) & BattleRenderCapacityConfig.SegmentMask);
            generation = handle >> (BattleRenderCapacityConfig.SegmentBits + BattleRenderCapacityConfig.SlotBits);
            return generation > 0 && segment != BattleRenderSegment.None;
        }

        private static int NextGeneration(int generation)
        {
            return generation >= BattleRenderCapacityConfig.MaxGeneration ? 1 : generation + 1;
        }

        private void WarnCapacityExceeded(BattleRenderSegment segment, string key)
        {
            switch (segment)
            {
                case BattleRenderSegment.UnitVit:
                    if (warnedUnitCapacityExceeded)
                    {
                        return;
                    }

                    warnedUnitCapacityExceeded = true;
                    Debug.LogError($"[BattleRender] UnitVit capacity exceeded. capacity={BattleRenderCapacityConfig.UnitVitCapacity}, key={key}");
                    break;
                case BattleRenderSegment.Effect2D:
                    if (warnedEffectCapacityExceeded)
                    {
                        return;
                    }

                    warnedEffectCapacityExceeded = true;
                    Debug.LogError($"[BattleRender] Effect2D capacity exceeded. capacity={BattleRenderCapacityConfig.Effect2DCapacity}, key={key}");
                    break;
                case BattleRenderSegment.MeshElement:
                    if (warnedMeshElementCapacityExceeded)
                    {
                        return;
                    }

                    warnedMeshElementCapacityExceeded = true;
                    Debug.LogError($"[BattleRender] MeshElement capacity exceeded. capacity={BattleRenderCapacityConfig.MeshElementCapacity}, key={key}");
                    break;
            }
        }

        private void WarnCompletedQueueFull()
        {
            if (warnedCompletedQueueFull)
            {
                return;
            }

            warnedCompletedQueueFull = true;
            Debug.LogError("[BattleRender] Completed render handle queue is full.");
        }

        private void EnsureDrawMeshRenderHost()
        {
            if (drawMeshRenderHost != null)
            {
                return;
            }

            drawMeshRoot = new GameObject("Battle DrawMesh Instance World")
            {
                hideFlags = HideFlags.DontSave
            };
            drawMeshRenderHost = drawMeshRoot.AddComponent<BattleDrawMeshInstanceRenderHost>();
            drawMeshRenderHost.Bind(drawMeshInstances);
        }

        private void SubmitDrawMeshInstances()
        {
            int drawn = drawMeshInstances.Draw(null);
            drawMeshRenderHost?.RecordDrawStats(drawMeshInstances.ActiveCount, drawn, "<all cameras>");
        }
    }
}
