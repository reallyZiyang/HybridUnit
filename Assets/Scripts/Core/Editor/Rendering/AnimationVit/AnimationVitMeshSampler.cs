#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class AnimationVitMeshSampler : IDisposable
{
    private readonly GameObject instanceRoot;
    private readonly SpriteRenderer[] renderers;

    public AnimationVitMeshSampler(GameObject sourceRoot)
    {
        if (sourceRoot == null)
        {
            throw new ArgumentNullException(nameof(sourceRoot));
        }

        instanceRoot = UnityEngine.Object.Instantiate(sourceRoot);
        instanceRoot.name = sourceRoot.name + "_AnimationVitSample";
        instanceRoot.hideFlags = HideFlags.HideAndDontSave;
        instanceRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instanceRoot.transform.localScale = Vector3.one;
        instanceRoot.SetActive(true);

        renderers = instanceRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(instanceRoot);
            throw new InvalidOperationException("Animation VIT v1 requires at least one SpriteRenderer under SourceRoot.");
        }

        Array.Sort(renderers, new SpriteRendererDrawOrderComparer(instanceRoot.transform));
    }

    public AnimationVitSample Sample(AnimationClip clip, float sampleTime)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        bool startedAnimationMode = !AnimationMode.InAnimationMode();
        if (startedAnimationMode)
        {
            AnimationMode.StartAnimationMode();
        }

        try
        {
            AnimationMode.SampleAnimationClip(instanceRoot, clip, Mathf.Clamp(sampleTime, 0f, Mathf.Max(0f, clip.length)));
            return BuildSample();
        }
        finally
        {
            if (startedAnimationMode)
            {
                AnimationMode.StopAnimationMode();
            }
        }
    }

    private AnimationVitSample BuildSample()
    {
        List<Vector3> vertices = new List<Vector3>(renderers.Length * 4);
        List<Vector2> uvs = new List<Vector2>(renderers.Length * 4);
        List<Color32> colors = new List<Color32>(renderers.Length * 4);
        List<int> triangles = new List<int>(renderers.Length * 6);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        Matrix4x4 rootWorldToLocal = instanceRoot.transform.worldToLocalMatrix;
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            SpriteRenderer spriteRenderer = renderers[rendererIndex];
            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                continue;
            }

            Vector2[] spriteVertices = sprite.vertices;
            Vector2[] spriteUvs = sprite.uv;
            ushort[] spriteTriangles = sprite.triangles;
            if (spriteVertices == null || spriteVertices.Length == 0 || spriteUvs == null || spriteUvs.Length != spriteVertices.Length)
            {
                continue;
            }

            int baseVertex = vertices.Count;
            Matrix4x4 rendererToRoot = rootWorldToLocal * spriteRenderer.transform.localToWorldMatrix;
            bool visible = spriteRenderer.enabled && spriteRenderer.gameObject.activeInHierarchy;
            Color vertexColor = visible ? spriteRenderer.color : new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0f);
            Color32 color32 = vertexColor;

            for (int vertexIndex = 0; vertexIndex < spriteVertices.Length; vertexIndex++)
            {
                Vector2 spriteVertex = spriteVertices[vertexIndex];
                Vector3 vertex = rendererToRoot.MultiplyPoint3x4(new Vector3(spriteVertex.x, spriteVertex.y, 0f));
                vertices.Add(vertex);
                uvs.Add(spriteUvs[vertexIndex]);
                colors.Add(color32);

                if (hasBounds)
                {
                    bounds.Encapsulate(vertex);
                }
                else
                {
                    bounds = new Bounds(vertex, Vector3.zero);
                    hasBounds = true;
                }
            }

            for (int triangleIndex = 0; triangleIndex < spriteTriangles.Length; triangleIndex++)
            {
                triangles.Add(baseVertex + spriteTriangles[triangleIndex]);
            }
        }

        return new AnimationVitSample(vertices.ToArray(), uvs.ToArray(), colors.ToArray(), triangles.ToArray(), hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero));
    }

    public void Dispose()
    {
        if (instanceRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(instanceRoot);
        }
    }

    private static string GetHierarchyPath(Transform transform, Transform root)
    {
        if (transform == root)
        {
            return transform.name;
        }

        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private sealed class SpriteRendererDrawOrderComparer : IComparer<SpriteRenderer>
    {
        private readonly Transform root;

        public SpriteRendererDrawOrderComparer(Transform root)
        {
            this.root = root;
        }

        public int Compare(SpriteRenderer x, SpriteRenderer y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            int layerCompare = SortingLayer.GetLayerValueFromID(x.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(y.sortingLayerID));
            if (layerCompare != 0)
            {
                return layerCompare;
            }

            int orderCompare = x.sortingOrder.CompareTo(y.sortingOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.CompareOrdinal(GetHierarchyPath(x.transform, root), GetHierarchyPath(y.transform, root));
        }
    }
}

public struct AnimationVitSample
{
    public AnimationVitSample(Vector3[] vertices, Vector2[] uvs, Color32[] colors, int[] triangles, Bounds bounds)
    {
        Vertices = vertices;
        Uvs = uvs;
        Colors = colors;
        Triangles = triangles;
        Bounds = bounds;
    }

    public Vector3[] Vertices { get; }
    public Vector2[] Uvs { get; }
    public Color32[] Colors { get; }
    public int[] Triangles { get; }
    public Bounds Bounds { get; }
}
#endif
