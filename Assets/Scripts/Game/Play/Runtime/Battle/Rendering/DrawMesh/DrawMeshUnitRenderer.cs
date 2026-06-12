using System;
using Game.Play.Rendering.Runtime;
using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    internal sealed class DrawMeshUnitRenderer
    {
        public const string IdleAction = "idle";
        public const string WalkAction = "walk";
        public const string HitAction = "hit";
        public const string DeadAction = "dead";

        public const int DefaultHitLockMs = 300;

        private const int UnitDeathFadeMs = 1000;

        private readonly BattleDrawMeshInstanceManager drawMeshInstances;
        private readonly Action ensureDrawMeshRenderHost;
        private readonly Action<int> markCompleted;

        public DrawMeshUnitRenderer(
            BattleDrawMeshInstanceManager drawMeshInstances,
            Action ensureDrawMeshRenderHost,
            Action<int> markCompleted)
        {
            this.drawMeshInstances = drawMeshInstances;
            this.ensureDrawMeshRenderHost = ensureDrawMeshRenderHost;
            this.markCompleted = markCompleted;
        }

        public void BindAnimationVit(BattleRenderEntry entry, UnitDrawRenderState state, BakedAnimationVitAsset asset)
        {
            if (entry.kind != BattleRenderEntryKind.Unit || asset == null || asset.mesh == null || asset.material == null)
            {
                return;
            }

            DrawMeshRenderMaterialUtility.ApplyVitMaterial(asset.material, asset.sourceTexture, asset.positionTexture, asset.colorTexture);
            entry.instanceHandle = drawMeshInstances.Spawn(new BattleDrawMeshInstanceDesc
            {
                mesh = asset.mesh,
                material = asset.material,
                position = new Vector3(entry.position.x, entry.position.y, 0f),
                rotation = Quaternion.Euler(0f, 0f, entry.angleDeg),
                scale = Vector3.one,
                color = Color.white,
                renderTransform = asset.RenderTransform,
                renderRotation = asset.RenderRotation,
                layer = 0,
                bounds = asset.bounds
            });
            if (!entry.instanceHandle.IsValid)
            {
                return;
            }

            state.animationAsset = asset;
            state.spineAsset = null;
            state.color = Color.white;
            entry.backend = BattleRenderBackend.AnimationVit;
            ensureDrawMeshRenderHost();
            ApplyEntryTransform(entry);
            Resume(entry, state);
        }

        public void BindSpineVit(BattleRenderEntry entry, UnitDrawRenderState state, BakedSpineVitAsset asset)
        {
            if (entry.kind != BattleRenderEntryKind.Unit || asset == null || asset.mesh == null || asset.material == null)
            {
                return;
            }

            DrawMeshRenderMaterialUtility.ApplyVitMaterial(asset.material, asset.sourceTexture, asset.positionTexture, asset.colorTexture);
            entry.instanceHandle = drawMeshInstances.Spawn(new BattleDrawMeshInstanceDesc
            {
                mesh = asset.mesh,
                material = asset.material,
                position = new Vector3(entry.position.x, entry.position.y, 0f),
                rotation = Quaternion.Euler(0f, 0f, entry.angleDeg),
                scale = Vector3.one,
                color = Color.white,
                renderTransform = asset.RenderTransform,
                renderRotation = asset.RenderRotation,
                layer = 0,
                bounds = asset.bounds
            });
            if (!entry.instanceHandle.IsValid)
            {
                return;
            }

            state.animationAsset = null;
            state.spineAsset = asset;
            state.color = Color.white;
            entry.backend = BattleRenderBackend.SpineVit;
            ensureDrawMeshRenderHost();
            ApplyEntryTransform(entry);
            Resume(entry, state);
        }

        public int PlayAction(BattleRenderEntry entry, UnitDrawRenderState state, string actionName, bool loop)
        {
            if (entry == null || state == null || !entry.instanceHandle.IsValid || entry.fallback)
            {
                return 0;
            }

            state.time = 0f;
            state.loop = loop;
            if (state.animationAsset != null && state.animationAsset.TryGetClip(actionName, out BakedAnimationVitClip animationClip))
            {
                state.animationClip = animationClip;
                state.spineClip = null;
                ApplyVitFrame(entry, state, animationClip.startFrame);
                return BattleRenderObjectUtility.SecondsToMilliseconds(animationClip.duration);
            }

            if (state.spineAsset != null && state.spineAsset.TryGetClip(actionName, out BakedSpineVitClip spineClip))
            {
                state.animationClip = null;
                state.spineClip = spineClip;
                ApplyVitFrame(entry, state, spineClip.startFrame);
                return BattleRenderObjectUtility.SecondsToMilliseconds(spineClip.duration);
            }

            return 0;
        }

        public int PlayLoopOrIdle(BattleRenderEntry entry, UnitDrawRenderState state, string actionName)
        {
            int durationMs = PlayAction(entry, state, actionName, true);
            if (durationMs > 0)
            {
                return durationMs;
            }

            return PlayAction(entry, state, IdleAction, true);
        }

        public int PlayHitOrIdle(BattleRenderEntry entry, UnitDrawRenderState state)
        {
            int durationMs = PlayAction(entry, state, HitAction, false);
            if (durationMs > 0)
            {
                return durationMs;
            }

            PlayAction(entry, state, IdleAction, true);
            return DefaultHitLockMs;
        }

        public void SetFlipX(BattleRenderEntry entry, bool flipX)
        {
            if (entry == null)
            {
                return;
            }

            entry.flipX = flipX;
            ApplyEntryScale(entry);
        }

        public void Resume(BattleRenderEntry entry, UnitDrawRenderState state)
        {
            SetAlpha(entry, state, 1f);
            if (state.dead)
            {
                state.deathFadeDelayMs = Mathf.Max(0, PlayAction(entry, state, DeadAction, false));
                state.deathFadeElapsedMs = 0;
            }
            else if (string.IsNullOrEmpty(state.pendingAction))
            {
                PlayAction(entry, state, IdleAction, true);
            }
            else
            {
                PlayAction(entry, state, state.pendingAction, false);
            }
        }

        public void Tick(BattleRenderEntry entry, UnitDrawRenderState state, float deltaTime)
        {
            if (entry == null || entry.kind != BattleRenderEntryKind.Unit || state == null)
            {
                return;
            }

            TickVitPlayback(entry, state, deltaTime);
            if (state.dead)
            {
                TickDeath(entry, state, deltaTime);
                return;
            }

            if (state.returnIdleMs <= 0)
            {
                return;
            }

            state.returnIdleMs = Mathf.Max(0, state.returnIdleMs - Mathf.CeilToInt(Mathf.Max(0f, deltaTime) * 1000f));
            if (state.returnIdleMs == 0)
            {
                PlayAction(entry, state, IdleAction, true);
            }
        }

        public void SetAlpha(BattleRenderEntry entry, UnitDrawRenderState state, float alpha)
        {
            if (entry == null || state == null || !entry.instanceHandle.IsValid)
            {
                return;
            }

            Color color = state.color;
            color.a = Mathf.Clamp01(alpha);
            state.color = color;
            drawMeshInstances.SetColor(entry.instanceHandle, color);
        }

        private void TickVitPlayback(BattleRenderEntry entry, UnitDrawRenderState state, float deltaTime)
        {
            if (!entry.instanceHandle.IsValid)
            {
                return;
            }

            float frameRate;
            int startFrame;
            int frameCount;
            float duration;
            bool clipLoop;
            if (state.animationClip != null && state.animationAsset != null)
            {
                frameRate = state.animationAsset.frameRate;
                startFrame = state.animationClip.startFrame;
                frameCount = state.animationClip.frameCount;
                duration = state.animationClip.duration;
                clipLoop = state.animationClip.loop;
            }
            else if (state.spineClip != null && state.spineAsset != null)
            {
                frameRate = state.spineAsset.frameRate;
                startFrame = state.spineClip.startFrame;
                frameCount = state.spineClip.frameCount;
                duration = state.spineClip.duration;
                clipLoop = state.spineClip.loop;
            }
            else
            {
                return;
            }

            if (frameCount <= 0)
            {
                drawMeshInstances.SetVisible(entry.instanceHandle, false);
                return;
            }

            state.time += deltaTime * Mathf.Max(0f, state.speed);
            duration = Mathf.Max(0.0001f, duration);
            if (state.time >= duration)
            {
                if (state.loop && clipLoop)
                {
                    state.time %= duration;
                }
                else
                {
                    state.time = duration;
                }
            }

            int localFrame = Mathf.Clamp(Mathf.FloorToInt(state.time * Mathf.Max(1f, frameRate)), 0, frameCount - 1);
            ApplyVitFrame(entry, state, startFrame + localFrame);
        }

        private void ApplyVitFrame(BattleRenderEntry entry, UnitDrawRenderState state, int absoluteFrame)
        {
            if (!entry.instanceHandle.IsValid)
            {
                return;
            }

            drawMeshInstances.SetFrameIndex(entry.instanceHandle, absoluteFrame);
            drawMeshInstances.SetColor(entry.instanceHandle, state.color);
            drawMeshInstances.SetVisible(entry.instanceHandle, entry.visible);
        }

        private void TickDeath(BattleRenderEntry entry, UnitDrawRenderState state, float deltaTime)
        {
            int deltaMs = Mathf.CeilToInt(Mathf.Max(0f, deltaTime) * 1000f);
            if (state.deathFadeDelayMs > 0)
            {
                state.deathFadeDelayMs = Mathf.Max(0, state.deathFadeDelayMs - deltaMs);
                SetAlpha(entry, state, 1f);
                return;
            }

            state.deathFadeElapsedMs = Mathf.Min(UnitDeathFadeMs, state.deathFadeElapsedMs + deltaMs);
            float alpha = 1f - Mathf.Clamp01(state.deathFadeElapsedMs / (float)UnitDeathFadeMs);
            SetAlpha(entry, state, alpha);
            if (state.deathFadeElapsedMs >= UnitDeathFadeMs)
            {
                markCompleted(entry.handle);
            }
        }

        private void ApplyEntryTransform(BattleRenderEntry entry)
        {
            drawMeshInstances.SetPosition(entry.instanceHandle, new Vector3(entry.position.x, entry.position.y, 0f));
            drawMeshInstances.SetRotation(entry.instanceHandle, Quaternion.Euler(0f, 0f, entry.angleDeg));
            ApplyEntryScale(entry);
            drawMeshInstances.SetVisible(entry.instanceHandle, entry.visible);
        }

        private void ApplyEntryScale(BattleRenderEntry entry)
        {
            if (entry != null && entry.instanceHandle.IsValid)
            {
                drawMeshInstances.SetScale(entry.instanceHandle, new Vector3(entry.flipX ? -1f : 1f, 1f, 1f));
            }
        }
    }
}
