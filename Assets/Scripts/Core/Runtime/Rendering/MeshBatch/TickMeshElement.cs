using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public abstract class TickMeshElement : MeshElement
{
    [SerializeField] private bool simulateInEditMode = true;

#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    protected bool SimulateInEditMode => simulateInEditMode;

    protected virtual bool IsRuntimeTickActive => true;
    protected virtual bool IsEditorTickActive => IsRuntimeTickActive;

    protected override void OnEnable()
    {
        base.OnEnable();
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;
        lastEditorTime = EditorApplication.timeSinceStartup;
#endif
        OnElementEnable();
    }

    protected override void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
        OnElementDisable();
        base.OnDisable();
    }

    protected void Update()
    {
        if (Application.isPlaying && IsRuntimeTickActive)
        {
            Tick(Time.deltaTime);
        }
    }

    protected virtual void OnElementEnable()
    {
    }

    protected virtual void OnElementDisable()
    {
    }

    protected abstract void Tick(float deltaTime);

#if UNITY_EDITOR
    protected virtual void OnAfterEditorTick()
    {
        if (MeshPlayer != null)
        {
            MeshPlayer.Rebuild();
        }

        SceneView.RepaintAll();
    }

    private void EditorUpdate()
    {
        if (Application.isPlaying || !simulateInEditMode || !IsEditorTickActive)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Min(0.1f, Mathf.Max(0f, (float)(now - lastEditorTime)));
        lastEditorTime = now;
        Tick(deltaTime);
        OnAfterEditorTick();
    }
#endif
}
