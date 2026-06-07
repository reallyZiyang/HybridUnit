using System.Collections.Generic;
using UnityEngine;

public sealed class MeshQuadWriter
{
    private readonly List<Vector3> vertices = new List<Vector3>(256);
    private readonly List<Vector2> uvs = new List<Vector2>(256);
    private readonly List<Color32> colors = new List<Color32>(256);
    private readonly List<int> indices = new List<int>(384);
    private Matrix4x4 worldToLayer;

    public int VertexCount => vertices.Count;

    public void Begin(Matrix4x4 layerWorldToLocal)
    {
        worldToLayer = layerWorldToLocal;
        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        indices.Clear();
    }

    public void AddQuad(Matrix4x4 localToWorld, float xMin, float yMin, float xMax, float yMax, Vector4 uvRect, Color32 color)
    {
        Matrix4x4 localToLayer = worldToLayer * localToWorld;
        int vertexStart = vertices.Count;
        vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMin, yMin, 0f)));
        vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMax, yMin, 0f)));
        vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMax, yMax, 0f)));
        vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMin, yMax, 0f)));

        float uMin = uvRect.x;
        float uMax = uvRect.x + uvRect.z;
        float vMin = uvRect.y;
        float vMax = uvRect.y + uvRect.w;
        uvs.Add(new Vector2(uMin, vMin));
        uvs.Add(new Vector2(uMax, vMin));
        uvs.Add(new Vector2(uMax, vMax));
        uvs.Add(new Vector2(uMin, vMax));

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        indices.Add(vertexStart);
        indices.Add(vertexStart + 2);
        indices.Add(vertexStart + 1);
        indices.Add(vertexStart);
        indices.Add(vertexStart + 3);
        indices.Add(vertexStart + 2);
    }

    public void ApplyTo(Mesh mesh)
    {
        mesh.Clear();
        if (vertices.Count == 0)
        {
            return;
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
    }
}
