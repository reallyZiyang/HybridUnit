using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Play.Rendering.Runtime
{
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
        public int layer;
        public Bounds bounds;
    }

    public sealed class BattleDrawMeshInstanceManager
    {
        private const int DefaultCapacity = 128;
        private const int MaxBatchSize = 1023;
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");
        private static Mesh sharedQuadMesh;

        private readonly List<Slot> slots = new(DefaultCapacity);
        private readonly Stack<int> freeIndices = new(DefaultCapacity);
        private readonly Dictionary<BatchKey, List<int>> batches = new();
        private readonly List<BatchKey> batchKeys = new(32);
        private readonly Matrix4x4[] matrices = new Matrix4x4[MaxBatchSize];
        private readonly Vector4[] colors = new Vector4[MaxBatchSize];
        private readonly MaterialPropertyBlock propertyBlock = new();

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
            public int layer;
            public Bounds bounds;
        }

        private readonly struct BatchKey
        {
            private readonly int meshId;
            private readonly int materialId;
            private readonly int layer;

            public BatchKey(Mesh mesh, Material material, int layer)
            {
                meshId = mesh != null ? mesh.GetInstanceID() : 0;
                materialId = material != null ? material.GetInstanceID() : 0;
                this.layer = layer;
            }
        }

        public int ActiveCount { get; private set; }

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
            for (int keyIndex = 0; keyIndex < batchKeys.Count; keyIndex++)
            {
                BatchKey key = batchKeys[keyIndex];
                List<int> indices = batches[key];
                drawCount += DrawBatch(indices, camera);
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
            batches.Clear();
            batchKeys.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (!slot.active || !slot.visible || slot.mesh == null || slot.material == null)
                {
                    continue;
                }

                BatchKey key = new(slot.mesh, slot.material, slot.layer);
                if (!batches.TryGetValue(key, out List<int> indices))
                {
                    indices = new List<int>(MaxBatchSize);
                    batches.Add(key, indices);
                    batchKeys.Add(key);
                }

                indices.Add(i);
            }
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
                }

                propertyBlock.Clear();
                propertyBlock.SetVectorArray(InstanceColorId, colors);
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
