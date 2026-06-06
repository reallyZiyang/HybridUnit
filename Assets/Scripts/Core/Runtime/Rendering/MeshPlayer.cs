using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class MeshPlayer : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int InstanceColorId = Shader.PropertyToID("_InstanceColor");

    [SerializeField] private Material material;
    [SerializeField] private Texture texture;
    [SerializeField] private bool rebuildInEditMode = true;
    [SerializeField] private Color color = Color.white;

    private readonly List<MeshElement> elements = new List<MeshElement>(128);
    private readonly MeshQuadWriter writer = new MeshQuadWriter();
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Mesh mesh;

    public Material Material => material;
    public Texture Texture => texture;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        EnsureComponents();
        Rebuild();
    }

    private void OnValidate()
    {
        EnsureComponents();
        ApplyMaterial();
        if (!Application.isPlaying && rebuildInEditMode)
        {
            Rebuild();
        }
    }

    private void LateUpdate()
    {
        if (Application.isPlaying || rebuildInEditMode)
        {
            Rebuild();
        }
    }

    public void SetMaterial(Material targetMaterial, Texture targetTexture)
    {
        material = targetMaterial;
        texture = targetTexture;
        ApplyMaterial();
    }

    public void Register(MeshElement element)
    {
        if (element == null || elements.Contains(element))
        {
            return;
        }

        elements.Add(element);
    }

    public void Unregister(MeshElement element)
    {
        elements.Remove(element);
    }

    public void Rebuild()
    {
        EnsureComponents();
        writer.Begin(transform.worldToLocalMatrix);

        for (int i = elements.Count - 1; i >= 0; i--)
        {
            MeshElement element = elements[i];
            if (element == null)
            {
                elements.RemoveAt(i);
                continue;
            }

            if (element.CanWriteQuads)
            {
                element.WriteQuads(writer);
            }
        }

        mesh.indexFormat = writer.VertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        writer.ApplyTo(mesh);
        meshRenderer.enabled = writer.VertexCount > 0 && material != null;
        meshFilter.sharedMesh = mesh;
        ApplyPropertyBlock();
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "Mesh Player Mesh"
            };
            mesh.MarkDynamic();
        }

        if (meshFilter.sharedMesh != mesh)
        {
            meshFilter.sharedMesh = mesh;
        }

        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (material != null)
        {
            material.enableInstancing = true;
            if (texture != null && material.HasProperty(MainTexId))
            {
                material.SetTexture(MainTexId, texture);
            }
        }

        meshRenderer.sharedMaterial = material;
    }

    private void ApplyPropertyBlock()
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.Clear();
        propertyBlock.SetColor(InstanceColorId, color);
        if (texture != null)
        {
            propertyBlock.SetTexture(MainTexId, texture);
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
