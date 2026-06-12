using System;
using Game.Play.Rendering.Runtime;
using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public static class BattleRenderCapacityConfig
    {
        public const int UnitVitCapacity = 512;
        public const int Effect2DCapacity = 1024;
        public const int MeshElementCapacity = 2048;
        public const bool AllowRuntimeGrow = false;

        internal const int SlotBits = 12;
        internal const int SlotMask = (1 << SlotBits) - 1;
        internal const int SegmentBits = 4;
        internal const int SegmentMask = (1 << SegmentBits) - 1;
        internal const int MaxGeneration = 0x7fff;
        internal const int TotalCapacity = UnitVitCapacity + Effect2DCapacity + MeshElementCapacity;
    }

    internal enum BattleRenderEntryKind
    {
        Unit,
        Effect
    }

    internal enum BattleRenderSegment
    {
        None = 0,
        UnitVit = 1,
        Effect2D = 2,
        MeshElement = 3
    }

    internal sealed class BattleRenderEntry
    {
        public bool active;
        public int handle;
        public int slot;
        public int generation = 1;
        public BattleRenderSegment segment;
        public BattleRenderEntryKind kind;
        public string key;
        public BattleRenderBackend backend;
        public Vector2 position;
        public float angleDeg;
        public bool flipX;
        public BattleDrawMeshInstanceHandle instanceHandle;
        public bool assetRequestStarted;
        public bool assetRequestCompleted;
        public bool fallback;
        public bool visible = true;

        public void ResetForSpawn(
            int handle,
            int slot,
            int generation,
            BattleRenderSegment segment,
            BattleRenderEntryKind kind,
            string key,
            Vector2 position,
            float angleDeg)
        {
            active = true;
            this.handle = handle;
            this.slot = slot;
            this.generation = generation;
            this.segment = segment;
            this.kind = kind;
            this.key = key;
            backend = BattleRenderBackend.None;
            this.position = position;
            this.angleDeg = angleDeg;
            flipX = false;
            instanceHandle = BattleDrawMeshInstanceHandle.Invalid;
            assetRequestStarted = false;
            assetRequestCompleted = false;
            fallback = false;
            visible = true;
        }

        public void ResetForDespawn()
        {
            active = false;
            handle = -1;
            key = null;
            backend = BattleRenderBackend.None;
            position = default;
            angleDeg = 0f;
            flipX = false;
            instanceHandle = BattleDrawMeshInstanceHandle.Invalid;
            assetRequestStarted = false;
            assetRequestCompleted = false;
            fallback = false;
            visible = true;
        }
    }

    internal sealed class UnitDrawRenderState
    {
        public string pendingAction;
        public BakedAnimationVitAsset animationAsset;
        public BakedSpineVitAsset spineAsset;
        public BakedAnimationVitClip animationClip;
        public BakedSpineVitClip spineClip;
        public float time;
        public float speed = 1f;
        public bool loop;
        public Color color = Color.white;
        public int returnIdleMs;
        public int deathFadeDelayMs;
        public int deathFadeElapsedMs;
        public bool dead;

        public void Reset()
        {
            pendingAction = null;
            animationAsset = null;
            spineAsset = null;
            animationClip = null;
            spineClip = null;
            time = 0f;
            speed = 1f;
            loop = false;
            color = Color.white;
            returnIdleMs = 0;
            deathFadeDelayMs = 0;
            deathFadeElapsedMs = 0;
            dead = false;
        }
    }

    internal sealed class EffectDrawRenderState
    {
        public BakedSequenceAsset sequenceAsset;
        public BakedSequenceMetadata metadata;
        public float time;
        public int currentFrame;
        public bool playing;

        public void Reset()
        {
            sequenceAsset = null;
            metadata = null;
            time = 0f;
            currentFrame = 0;
            playing = false;
        }
    }

    [Serializable]
    internal sealed class BakedSequenceMetadata
    {
        public float effectiveDuration;
        public int frameRate;
        public int frameCount;
        public int firstVisibleFrame;
        public int lastVisibleFrame;
        public BakedSequenceFrameRect[] frameRects;
    }

    [Serializable]
    internal sealed class BakedSequenceFrameRect
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
