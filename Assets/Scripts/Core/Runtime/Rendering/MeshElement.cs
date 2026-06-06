using UnityEngine;

[ExecuteAlways]
public abstract class MeshElement : MonoBehaviour
{
    [SerializeField] private MeshPlayer meshPlayer;
    [SerializeField] private bool visible = true;

    public MeshPlayer MeshPlayer => meshPlayer;
    public bool Visible
    {
        get => visible;
        set => visible = value;
    }

    public virtual bool CanWriteQuads => visible && isActiveAndEnabled;

    protected virtual void OnEnable()
    {
        RegisterToMeshPlayer();
    }

    protected virtual void OnDisable()
    {
        if (meshPlayer != null)
        {
            meshPlayer.Unregister(this);
        }
    }

    protected virtual void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            RegisterToMeshPlayer();
        }
    }

    public void SetMeshPlayer(MeshPlayer target)
    {
        if (meshPlayer == target)
        {
            return;
        }

        if (meshPlayer != null)
        {
            meshPlayer.Unregister(this);
        }

        meshPlayer = target;
        if (meshPlayer != null && isActiveAndEnabled)
        {
            meshPlayer.Register(this);
        }
    }

    public abstract void WriteQuads(MeshQuadWriter writer);

    protected void RegisterToMeshPlayer()
    {
        if (meshPlayer == null)
        {
            meshPlayer = GetComponentInParent<MeshPlayer>();
        }

        if (meshPlayer == null)
        {
#pragma warning disable CS0618
            meshPlayer = FindObjectOfType<MeshPlayer>();
#pragma warning restore CS0618
        }

        if (meshPlayer != null)
        {
            meshPlayer.Register(this);
        }
    }
}
