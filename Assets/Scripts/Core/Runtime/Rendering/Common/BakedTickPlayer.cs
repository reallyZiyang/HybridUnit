using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

public abstract class BakedTickPlayer : BakedMeshPlayerBase
{
    [SerializeField] private bool simulateInEditMode = true;

#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    protected bool SimulateInEditMode => simulateInEditMode;

    protected virtual bool IsRuntimeTickActive => true;
    protected virtual bool IsEditorTickActive => true;

    protected void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;
        lastEditorTime = EditorApplication.timeSinceStartup;
#endif
        OnPlayerEnable();
    }

    protected void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
        OnPlayerDisable();
    }

    protected void Update()
    {
        if (Application.isPlaying && IsRuntimeTickActive)
        {
            Tick(Time.deltaTime);
        }
    }

    protected virtual void OnPlayerEnable()
    {
    }

    protected virtual void OnPlayerDisable()
    {
    }

    protected abstract void Tick(float deltaTime);

    protected virtual void OnBeforeEditorTick()
    {
        EnsureRendererComponents();
    }

    protected virtual void OnEditorPreviewTick()
    {
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (Application.isPlaying || !simulateInEditMode)
        {
            return;
        }

        OnBeforeEditorTick();

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Min(0.1f, Mathf.Max(0f, (float)(now - lastEditorTime)));
        lastEditorTime = now;

        if (IsEditorTickActive)
        {
            Tick(deltaTime);
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
        }
        else
        {
            OnEditorPreviewTick();
        }
    }
#endif
}
