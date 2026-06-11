using UnityEngine;

public abstract class BattleRenderAssetBase : ScriptableObject
{
    public Vector2 renderOffset = Vector2.zero;
    public Vector2 renderScale = Vector2.one;
    public float renderRotationDeg;

    public Vector4 RenderTransform
    {
        get
        {
            Vector2 safeScale = renderScale == Vector2.zero ? Vector2.one : renderScale;
            return new Vector4(renderOffset.x, renderOffset.y, safeScale.x, safeScale.y);
        }
    }

    public Vector4 RenderRotation
    {
        get
        {
            float radians = renderRotationDeg * Mathf.Deg2Rad;
            return new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f);
        }
    }
}
