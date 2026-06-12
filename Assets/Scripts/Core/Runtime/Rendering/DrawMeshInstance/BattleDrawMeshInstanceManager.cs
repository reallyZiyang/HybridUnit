using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Play.Rendering.Runtime
{
    public enum BattleDrawMeshRenderLayer
    {
        Effect = 0,
        GroundEffect = 1,
        Unit = 2,
        Projectile = 3,
        MeshElement = 4
    }

    public readonly struct BattleDrawMeshInstanceHandle
    {
        public static readonly BattleDrawMeshInstanceHandle Invalid = new(-1, 0);

        public readonly int index;
        public readonly int generation;

        public BattleDrawMeshInstanceHandle(int index, int generation)
        {
            this.index = index;
            this.generation = generation;
        }

        public bool IsValid => index >= 0 && generation > 0;
    }

    public struct BattleDrawMeshInstanceDesc
    {
        public Mesh mesh;
        public Material material;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Color color;
        public Vector4 frameUvRect;
        public Vector4 frameUvClamp;
        public Vector4 frameTransform;
        public float frameIndex;
        public Vector4 renderTransform;
        public Vector4 renderRotation;
        public BattleDrawMeshRenderLayer renderLayer;
        public int layer;
        public Bounds bounds;
    }

    public sealed class BattleDrawMeshInstanceManager
    {
        private const int DefaultCapacity = 128;
        private const int MaxBatchSize = 1023;
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
        private static readonly int FrameUvRectId = Shader.PropertyToID("_FrameUVRect");
        private static readonly int FrameUvClampId = Shader.PropertyToID("_FrameUVClamp");
        private static readonly int FrameTransformId = Shader.PropertyToID("_FrameTransform");
        private static readonly int FrameIndexId = Shader.PropertyToID("_FrameIndex");
        private static readonly int RenderTransId = Shader.PropertyToID("_RenderTrans");
        private static readonly int RenderRotationId = Shader.PropertyToID("_RenderRotation");
        private static readonly BattleDrawMeshRenderLayer[] DrawOrder =
        {
            BattleDrawMeshRenderLayer.GroundEffect,
            BattleDrawMeshRenderLayer.Unit,
            BattleDrawMeshRenderLayer.Projectile,
            BattleDrawMeshRenderLayer.Effect,
            BattleDrawMeshRenderLayer.MeshElement
        };

        private static Mesh sharedQuadMesh;

        private readonly List<Slot> slots = new(DefaultCapacity);
        private readonly Stack<int> freeIndices = new(DefaultCapacity);
        private readonly Dictionary<BatchKey, List<int>> batches = new();
        private readonly List<BatchKey> batchKeys = new(32);
        private readonly Matrix4x4[] matrices = new Matrix4x4[MaxBatchSize];
        private readonly Vector4[] colors = new Vector4[MaxBatchSize];
        private readonly Vector4[] frameUvRects = new Vector4[MaxBatchSize];
        private readonly Vector4[] frameUvClamps = new Vector4[MaxBatchSize];
        private readonly Vector4[] frameTransforms = new Vector4[MaxBatchSize];
        private readonly float[] frameIndices = new float[MaxBatchSize];
        private readonly Vector4[] renderTransforms = new Vector4[MaxBatchSize];
        private readonly Vector4[] renderRotations = new Vector4[MaxBatchSize];
        private readonly MaterialPropertyBlock propertyBlock = new();
        private float sortingGridMinY;
        private float unitSortingStep;

        private struct Slot
        {
            public bool active;
            public bool visible;
            public int generation;
            public Mesh mesh;
            public Material material;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Color color;
            public Vector4 frameUvRect;
            public Vector4 frameUvClamp;
            public Vector4 frameTransform;
            public float frameIndex;
            public Vector4 renderTransform;
            public Vector4 renderRotation;
            public BattleDrawMeshRenderLayer renderLayer;
            public int layer;
            public Bounds bounds;
        }

        private readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly BattleDrawMeshRenderLayer renderLayer;
            public readonly int sortBucket;
            private readonly int meshId;
            private readonly int materialId;
            private readonly int layer;

            public BatchKey(Mesh mesh, Material material, int layer, BattleDrawMeshRenderLayer renderLayer, int sortBucket)
            {
                this.renderLayer = renderLayer;
                this.sortBucket = sortBucket;
                meshId = mesh != null ? mesh.GetInstanceID() : 0;
                materialId = material != null ? material.GetInstanceID() : 0;
                this.layer = layer;
            }

            public bool Equals(BatchKey other)
            {
                return renderLayer == other.renderLayer
                    && sortBucket == other.sortBucket
                    && meshId == other.meshId
                    && materialId == other.materialId
                    && layer == other.layer;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)renderLayer;
                    hash = (hash * 397) ^ sortBucket;
                    hash = (hash * 397) ^ meshId;
                    hash = (hash * 397) ^ materialId;
                    hash = (hash * 397) ^ layer;
                    return hash;
                }
            }
        }

        public int ActiveCount { get; private set; }

        public void SetUnitSortingGrid(float gridMinY, float cellSize)
        {
            sortingGridMinY = gridMinY;
            unitSortingStep = Mathf.Max(0f, cellSize) * 0.5f;
        }

        public BattleDrawMeshInstanceHandle Spawn(in BattleDrawMeshInstanceDesc desc)
        {
            if (desc.mesh == null || desc.material == null)
            {
                return BattleDrawMeshInstanceHandle.Invalid;
            }

            Material material = desc.material;
            material.enableInstancing = true;
            int index = freeIndices.Count > 0 ? freeIndices.Pop() : slots.Count;
            Slot slot = index < slots.Count ? slots[index] : default;
            int generation = slot.generation + 1;
            if (generation <= 0)
            {
                generation = 1;
            }

            slot.active = true;
            slot.visible = true;
            slot.generation = generation;
            slot.mesh = desc.mesh;
            slot.material = material;
            slot.position = desc.position;
            slot.rotation = desc.rotation == default ? Quaternion.identity : desc.rotation;
            slot.scale = desc.scale == Vector3.zero ? Vector3.one : desc.scale;
            slot.color = desc.color == default ? Color.white : desc.color;
            slot.frameUvRect = desc.frameUvRect == default ? new Vector4(0f, 0f, 1f, 1f) : desc.frameUvRect;
            slot.frameUvClamp = desc.frameUvClamp == default ? new Vector4(0f, 0f, 1f, 1f) : desc.frameUvClamp;
            slot.frameTransform = desc.frameTransform == default ? new Vector4(0f, 0f, 1f, 1f) : desc.frameTransform;
            slot.frameIndex = Mathf.Max(0f, desc.frameIndex);
            slot.renderTransform = desc.renderTransform == default ? new Vector4(0f, 0f, 1f, 1f) : desc.renderTransform;
            slot.renderRotation = desc.renderRotation == default ? new Vector4(1f, 0f, 0f, 0f) : desc.renderRotation;
            slot.renderLayer = desc.renderLayer == default ? BattleDrawMeshRenderLayer.Effect : desc.renderLayer;
            slot.layer = desc.layer;
            slot.bounds = desc.bounds;

            if (index < slots.Count)
            {
                slots[index] = slot;
            }
            else
            {
                slots.Add(slot);
            }

            ActiveCount++;
            return new BattleDrawMeshInstanceHandle(index, generation);
        }

        public bool IsValid(BattleDrawMeshInstanceHandle handle)
        {
            return handle.index >= 0
                && handle.index < slots.Count
                && slots[handle.index].active
                && slots[handle.index].generation == handle.generation;
        }

        public void SetPosition(BattleDrawMeshInstanceHandle handle, Vector3 position)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.position = position;
            slots[handle.index] = slot;
        }

        public void SetRotation(BattleDrawMeshInstanceHandle handle, Quaternion rotation)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.rotation = rotation;
            slots[handle.index] = slot;
        }

        public void SetScale(BattleDrawMeshInstanceHandle handle, Vector3 scale)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.scale = scale == Vector3.zero ? Vector3.one : scale;
            slots[handle.index] = slot;
        }

        public void SetColor(BattleDrawMeshInstanceHandle handle, Color color)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.color = color;
            slots[handle.index] = slot;
        }

        public void SetRenderTransform(BattleDrawMeshInstanceHandle handle, Vector4 renderTransform, Vector4 renderRotation)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.renderTransform = renderTransform == default ? new Vector4(0f, 0f, 1f, 1f) : renderTransform;
            slot.renderRotation = renderRotation == default ? new Vector4(1f, 0f, 0f, 0f) : renderRotation;
            slots[handle.index] = slot;
        }

        public void SetFrameData(BattleDrawMeshInstanceHandle handle, Vector4 frameUvRect, Vector4 frameUvClamp, Vector4 frameTransform)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.frameUvRect = frameUvRect == default ? new Vector4(0f, 0f, 1f, 1f) : frameUvRect;
            slot.frameUvClamp = frameUvClamp == default ? new Vector4(0f, 0f, 1f, 1f) : frameUvClamp;
            slot.frameTransform = frameTransform == default ? new Vector4(0f, 0f, 1f, 1f) : frameTransform;
            slots[handle.index] = slot;
        }

        public void SetFrameIndex(BattleDrawMeshInstanceHandle handle, float frameIndex)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.frameIndex = Mathf.Max(0f, frameIndex);
            slots[handle.index] = slot;
        }

        public void SetVisible(BattleDrawMeshInstanceHandle handle, bool visible)
        {
            if (!IsValid(handle))
            {
                return;
            }

            Slot slot = slots[handle.index];
            slot.visible = visible;
            slots[handle.index] = slot;
        }

        public void Despawn(BattleDrawMeshInstanceHandle handle)
        {
            if (!IsValid(handle))
            {
                return;
            }

            int index = handle.index;
            Slot slot = slots[index];
            slot.active = false;
            slot.visible = false;
            slot.mesh = null;
            slot.material = null;
            slots[index] = slot;
            freeIndices.Push(index);
            ActiveCount--;
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                slot.active = false;
                slot.visible = false;
                slot.mesh = null;
                slot.material = null;
                slots[i] = slot;
            }

            freeIndices.Clear();
            slots.Clear();
            batches.Clear();
            batchKeys.Clear();
            ActiveCount = 0;
        }

        public int Draw(Camera camera = null)
        {
            if (ActiveCount <= 0)
            {
                return 0;
            }

            int drawCount = 0;
            BuildBatches();
            for (int layerIndex = 0; layerIndex < DrawOrder.Length; layerIndex++)
            {
                drawCount += DrawLayer(DrawOrder[layerIndex], camera);
            }

            return drawCount;
        }

        public static Mesh GetSharedQuadMesh()
        {
            if (sharedQuadMesh != null)
            {
                return sharedQuadMesh;
            }

            sharedQuadMesh = new Mesh
            {
                name = "Battle DrawMeshInstance Quad",
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
            sharedQuadMesh.bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 10f));
            sharedQuadMesh.RecalculateNormals();
            return sharedQuadMesh;
        }

        private void BuildBatches()
        {
            for (int keyIndex = 0; keyIndex < batchKeys.Count; keyIndex++)
            {
                if (batches.TryGetValue(batchKeys[keyIndex], out List<int> indices))
                {
                    indices.Clear();
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (!slot.active || !slot.visible || slot.mesh == null || slot.material == null)
                {
                    continue;
                }

                BattleDrawMeshRenderLayer renderLayer = slot.renderLayer;
                int sortBucket = ResolveSortBucket(slot);
                BatchKey key = new(slot.mesh, slot.material, slot.layer, renderLayer, sortBucket);
                if (!batches.TryGetValue(key, out List<int> indices))
                {
                    indices = new List<int>(MaxBatchSize);
                    batches.Add(key, indices);
                    batchKeys.Add(key);
                }

                indices.Add(i);
            }
        }

        private int DrawLayer(BattleDrawMeshRenderLayer renderLayer, Camera camera)
        {
            return renderLayer == BattleDrawMeshRenderLayer.Unit
                ? DrawUnitLayer(camera)
                : DrawFlatLayer(renderLayer, camera);
        }

        private int DrawFlatLayer(BattleDrawMeshRenderLayer renderLayer, Camera camera)
        {
            int drawCount = 0;
            for (int keyIndex = 0; keyIndex < batchKeys.Count; keyIndex++)
            {
                BatchKey key = batchKeys[keyIndex];
                if (key.renderLayer != renderLayer
                    || !batches.TryGetValue(key, out List<int> indices)
                    || indices.Count == 0)
                {
                    continue;
                }

                drawCount += DrawBatch(indices, camera);
            }

            return drawCount;
        }

        private int DrawUnitLayer(Camera camera)
        {
            int drawCount = 0;
            int maxBucket = int.MinValue;
            int minBucket = int.MaxValue;
            for (int keyIndex = 0; keyIndex < batchKeys.Count; keyIndex++)
            {
                BatchKey key = batchKeys[keyIndex];
                if (key.renderLayer != BattleDrawMeshRenderLayer.Unit
                    || !batches.TryGetValue(key, out List<int> indices)
                    || indices.Count == 0)
                {
                    continue;
                }

                maxBucket = Mathf.Max(maxBucket, key.sortBucket);
                minBucket = Mathf.Min(minBucket, key.sortBucket);
            }

            if (maxBucket == int.MinValue)
            {
                return 0;
            }

            for (int bucket = maxBucket; bucket >= minBucket; bucket--)
            {
                for (int keyIndex = 0; keyIndex < batchKeys.Count; keyIndex++)
                {
                    BatchKey key = batchKeys[keyIndex];
                    if (key.renderLayer != BattleDrawMeshRenderLayer.Unit
                        || key.sortBucket != bucket
                        || !batches.TryGetValue(key, out List<int> indices)
                        || indices.Count == 0)
                    {
                        continue;
                    }

                    drawCount += DrawBatch(indices, camera);
                }
            }

            return drawCount;
        }

        private int ResolveSortBucket(in Slot slot)
        {
            if (slot.renderLayer != BattleDrawMeshRenderLayer.Unit || unitSortingStep <= 0f)
            {
                return 0;
            }

            return Mathf.FloorToInt((slot.position.y - sortingGridMinY) / unitSortingStep);
        }

        private int DrawBatch(List<int> indices, Camera camera)
        {
            int cursor = 0;
            int drawCount = 0;
            while (cursor < indices.Count)
            {
                int count = Mathf.Min(MaxBatchSize, indices.Count - cursor);
                Slot first = slots[indices[cursor]];
                for (int i = 0; i < count; i++)
                {
                    Slot slot = slots[indices[cursor + i]];
                    matrices[i] = Matrix4x4.TRS(slot.position, slot.rotation, slot.scale);
                    colors[i] = slot.color;
                    frameUvRects[i] = slot.frameUvRect;
                    frameUvClamps[i] = slot.frameUvClamp;
                    frameTransforms[i] = slot.frameTransform;
                    frameIndices[i] = slot.frameIndex;
                    renderTransforms[i] = slot.renderTransform;
                    renderRotations[i] = slot.renderRotation;
                }

                propertyBlock.Clear();
                propertyBlock.SetVectorArray(InstanceColorId, colors);
                propertyBlock.SetVectorArray(FrameUvRectId, frameUvRects);
                propertyBlock.SetVectorArray(FrameUvClampId, frameUvClamps);
                propertyBlock.SetVectorArray(FrameTransformId, frameTransforms);
                propertyBlock.SetFloatArray(FrameIndexId, frameIndices);
                propertyBlock.SetVectorArray(RenderTransId, renderTransforms);
                propertyBlock.SetVectorArray(RenderRotationId, renderRotations);
                Graphics.DrawMeshInstanced(
                    first.mesh,
                    0,
                    first.material,
                    matrices,
                    count,
                    propertyBlock,
                    ShadowCastingMode.Off,
                    false,
                    first.layer,
                    camera);
                drawCount += count;
                cursor += count;
            }

            return drawCount;
        }
    }
}
