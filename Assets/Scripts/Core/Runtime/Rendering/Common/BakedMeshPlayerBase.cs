using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public abstract class BakedMeshPlayerBase : MonoBehaviour
{
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    protected MeshFilter PlayerMeshFilter => meshFilter;
    protected MeshRenderer PlayerRenderer => meshRenderer;
    protected MaterialPropertyBlock PropertyBlock => propertyBlock;

    protected void EnsureRendererComponents()
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

        ApplySorting();
    }

    protected void SetSharedMesh(Mesh mesh)
    {
        EnsureRendererComponents();
        if (meshFilter.sharedMesh != mesh)
        {
            meshFilter.sharedMesh = mesh;
        }
    }

    protected void SetSharedMaterial(Material material, bool enableInstancing = true)
    {
        EnsureRendererComponents();
        if (material != null && enableInstancing)
        {
            material.enableInstancing = true;
        }

        if (meshRenderer.sharedMaterial != material)
        {
            meshRenderer.sharedMaterial = material;
        }
    }

    protected MaterialPropertyBlock BeginPropertyBlock()
    {
        EnsureRendererComponents();
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.Clear();
        return propertyBlock;
    }

    protected void ApplyPropertyBlock()
    {
        EnsureRendererComponents();
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    protected void ClearPropertyBlock()
    {
        EnsureRendererComponents();
        meshRenderer.SetPropertyBlock(null);
    }

    protected void SetRendererVisible(bool visible)
    {
        EnsureRendererComponents();
        meshRenderer.enabled = visible;
    }

    protected void ApplySorting()
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
    }
}
