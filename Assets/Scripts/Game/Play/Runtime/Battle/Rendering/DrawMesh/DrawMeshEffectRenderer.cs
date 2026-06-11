using System;
using Game.Play.Rendering.Runtime;
using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    internal sealed class DrawMeshEffectRenderer
    {
        private const float FallbackProjectileSize = 0.2f;
        private const float FallbackUnitSize = 0.65f;
        private static readonly Color FallbackProjectileColor = new(1f, 0.45f, 0.05f, 1f);
        private static readonly Color FallbackUnitColor = new(0.35f, 0.65f, 1f, 1f);

        private static bool warnedFallbackSpawnFailed;

        private readonly BattleDrawMeshInstanceManager drawMeshInstances;
        private readonly Action ensureDrawMeshRenderHost;

        public DrawMeshEffectRenderer(BattleDrawMeshInstanceManager drawMeshInstances, Action ensureDrawMeshRenderHost)
        {
            this.drawMeshInstances = drawMeshInstances;
            this.ensureDrawMeshRenderHost = ensureDrawMeshRenderHost;
        }

        public void BindSequence(BattleRenderEntry entry, EffectDrawRenderState state, BakedSequenceAsset asset)
        {
            if (entry.kind != BattleRenderEntryKind.Effect || asset == null || asset.material == null || asset.atlas == null)
            {
                return;
            }

            BakedSequenceMetadata metadata = DrawMeshRenderMaterialUtility.LoadSequenceMetadata(asset);
            if (metadata == null || metadata.frameRects == null || metadata.frameRects.Length == 0)
            {
                return;
            }

            DrawMeshRenderMaterialUtility.ApplySequenceMaterial(asset);
            entry.instanceHandle = drawMeshInstances.Spawn(new BattleDrawMeshInstanceDesc
            {
                mesh = BattleDrawMeshInstanceManager.GetSharedQuadMesh(),
                material = asset.material,
                position = new Vector3(entry.position.x, entry.position.y, 0f),
                rotation = Quaternion.Euler(0f, 0f, entry.angleDeg),
                scale = Vector3.one,
                color = asset.color,
                renderTransform = asset.RenderTransform,
                renderRotation = asset.RenderRotation,
                layer = 0,
                bounds = new Bounds(Vector3.zero, Vector3.one)
            });
            if (!entry.instanceHandle.IsValid)
            {
                return;
            }

            state.sequenceAsset = asset;
            state.metadata = metadata;
            state.time = 0f;
            state.playing = true;
            entry.backend = BattleRenderBackend.Sequence;
            ensureDrawMeshRenderHost();
            ApplyEntryTransform(entry);
            ApplySequenceFrame(entry, state, asset.skipEmptyFrames ? DrawMeshRenderMaterialUtility.FindNextVisibleFrame(metadata, 0) : 0);
        }

        public void BindAtlas(BattleRenderEntry entry, AtlasRenderAsset asset)
        {
            if (entry.kind != BattleRenderEntryKind.Effect || asset == null || asset.material == null)
            {
                return;
            }

            DrawMeshRenderMaterialUtility.ApplyAtlasMaterial(asset);
            Vector4 uvRect = DrawMeshRenderMaterialUtility.GetAtlasUvRect(asset);
            entry.instanceHandle = drawMeshInstances.Spawn(new BattleDrawMeshInstanceDesc
            {
                mesh = BattleDrawMeshInstanceManager.GetSharedQuadMesh(),
                material = asset.material,
                position = new Vector3(entry.position.x, entry.position.y, 0f),
                rotation = Quaternion.Euler(0f, 0f, entry.angleDeg),
                scale = new Vector3(Mathf.Max(0.0001f, asset.size.x), Mathf.Max(0.0001f, asset.size.y), 1f),
                color = asset.color,
                frameUvRect = DrawMeshRenderMaterialUtility.GetSafeUvRect(uvRect),
                frameUvClamp = DrawMeshRenderMaterialUtility.GetUvClamp(uvRect),
                frameTransform = new Vector4(0f, 0f, 1f, 1f),
                renderTransform = asset.RenderTransform,
                renderRotation = asset.RenderRotation,
                layer = 0,
                bounds = new Bounds(Vector3.zero, Vector3.one)
            });
            if (!entry.instanceHandle.IsValid)
            {
                return;
            }

            entry.backend = BattleRenderBackend.Atlas;
            ensureDrawMeshRenderHost();
            ApplyEntryTransform(entry);
        }

        public void CreateFallback(BattleRenderEntry entry)
        {
            if (entry == null || entry.instanceHandle.IsValid)
            {
                return;
            }

            bool unit = entry.kind == BattleRenderEntryKind.Unit;
            entry.instanceHandle = drawMeshInstances.Spawn(new BattleDrawMeshInstanceDesc
            {
                mesh = BattleDrawMeshInstanceManager.GetSharedQuadMesh(),
                material = unit
                    ? DrawMeshRenderMaterialUtility.GetFallbackUnitMaterial()
                    : DrawMeshRenderMaterialUtility.GetFallbackProjectileMaterial(),
                position = new Vector3(entry.position.x, entry.position.y, 0f),
                rotation = Quaternion.Euler(0f, 0f, entry.angleDeg),
                scale = unit
                    ? new Vector3(FallbackUnitSize, FallbackUnitSize, 1f)
                    : new Vector3(FallbackProjectileSize, FallbackProjectileSize, 1f),
                color = unit ? FallbackUnitColor : FallbackProjectileColor,
                renderTransform = new Vector4(0f, 0f, 1f, 1f),
                renderRotation = new Vector4(1f, 0f, 0f, 0f),
                layer = 0,
                bounds = new Bounds(Vector3.zero, Vector3.one)
            });
            entry.fallback = true;
            entry.backend = BattleRenderBackend.Fallback;
            if (!entry.instanceHandle.IsValid)
            {
                WarnFallbackSpawnFailed();
                return;
            }

            ensureDrawMeshRenderHost();
            ApplyEntryTransform(entry);
        }

        public void Tick(BattleRenderEntry entry, EffectDrawRenderState state, float deltaTime)
        {
            if (entry == null || entry.kind != BattleRenderEntryKind.Effect || state == null)
            {
                return;
            }

            if (!entry.instanceHandle.IsValid
                || !state.playing
                || state.sequenceAsset == null
                || state.metadata == null
                || state.metadata.frameRects == null)
            {
                return;
            }

            state.time += deltaTime * Mathf.Max(0f, state.sequenceAsset.speed);
            float duration = Mathf.Max(0.0001f, state.metadata.effectiveDuration);
            if (state.time >= duration)
            {
                if (state.sequenceAsset.loop)
                {
                    state.time %= duration;
                }
                else
                {
                    state.time = duration;
                    state.playing = false;
                }
            }

            int frame = Mathf.Clamp(
                Mathf.FloorToInt(state.time * Mathf.Max(1, state.metadata.frameRate)),
                0,
                state.metadata.frameCount - 1);
            ApplySequenceFrame(
                entry,
                state,
                state.sequenceAsset.skipEmptyFrames
                    ? DrawMeshRenderMaterialUtility.FindNextVisibleFrame(state.metadata, frame)
                    : frame);
        }

        private void ApplySequenceFrame(BattleRenderEntry entry, EffectDrawRenderState state, int frame)
        {
            if (!entry.instanceHandle.IsValid || state.metadata?.frameRects == null || state.metadata.frameRects.Length == 0)
            {
                return;
            }

            int safeFrame = Mathf.Clamp(frame, 0, state.metadata.frameRects.Length - 1);
            BakedSequenceFrameRect rect = state.metadata.frameRects[safeFrame];
            state.currentFrame = safeFrame;
            bool visible = rect.uvWidth > 0f && rect.uvHeight > 0f && rect.quadWidth > 0f && rect.quadHeight > 0f;
            drawMeshInstances.SetVisible(entry.instanceHandle, entry.visible && visible);
            if (!visible)
            {
                return;
            }

            BakedSequenceAsset asset = state.sequenceAsset;
            float uvX = asset.flipU ? rect.uvX + rect.uvWidth : rect.uvX;
            float uvY = asset.flipV ? rect.uvY + rect.uvHeight : rect.uvY;
            float uvWidth = asset.flipU ? -rect.uvWidth : rect.uvWidth;
            float uvHeight = asset.flipV ? -rect.uvHeight : rect.uvHeight;
            Vector4 uvRect = new(uvX, uvY, uvWidth, uvHeight);
            float safeDisplayScale = Mathf.Max(0.0001f, asset.displayScale);
            Vector4 frameTransform = new(
                rect.quadOffsetX * safeDisplayScale,
                rect.quadOffsetY * safeDisplayScale,
                rect.quadWidth * safeDisplayScale,
                rect.quadHeight * safeDisplayScale);
            drawMeshInstances.SetFrameData(
                entry.instanceHandle,
                uvRect,
                DrawMeshRenderMaterialUtility.CalculateUvClamp(asset.atlas, uvRect),
                frameTransform);
            drawMeshInstances.SetColor(entry.instanceHandle, asset.color);
        }

        private void ApplyEntryTransform(BattleRenderEntry entry)
        {
            drawMeshInstances.SetPosition(entry.instanceHandle, new Vector3(entry.position.x, entry.position.y, 0f));
            drawMeshInstances.SetRotation(entry.instanceHandle, Quaternion.Euler(0f, 0f, entry.angleDeg));
            drawMeshInstances.SetVisible(entry.instanceHandle, entry.visible);
        }

        private static void WarnFallbackSpawnFailed()
        {
            if (warnedFallbackSpawnFailed)
            {
                return;
            }

            warnedFallbackSpawnFailed = true;
            Debug.LogWarning("[BattleRender] DrawMesh fallback spawn failed. Check fallback mesh/material/shader.");
        }
    }
}
