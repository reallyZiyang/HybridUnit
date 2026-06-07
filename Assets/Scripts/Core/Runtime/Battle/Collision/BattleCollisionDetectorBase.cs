using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public abstract class BattleCollisionDetectorBase : MonoBehaviour
{
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private bool detectEveryFrame = true;
    [SerializeField] private bool detectInEditMode;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 1f, 1f, 0.75f);

    private readonly List<BakedSpineVitPlayer> queryResults = new List<BakedSpineVitPlayer>(64);
    private readonly List<BakedSpineVitPlayer> currentHits = new List<BakedSpineVitPlayer>(64);
    private readonly List<BakedSpineVitPlayer> previousHits = new List<BakedSpineVitPlayer>(64);
    private readonly HashSet<BakedSpineVitPlayer> currentHitSet = new HashSet<BakedSpineVitPlayer>();

    protected Color HitColor => hitColor;
    protected Color GizmoColor => gizmoColor;
    protected bool HasHits => previousHits.Count > 0;

    private void Update()
    {
        if (!detectEveryFrame)
        {
            return;
        }

        if (!Application.isPlaying && !detectInEditMode)
        {
            return;
        }

        Detect();
    }

    private void OnDisable()
    {
        RestorePreviousHits();
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = HasHits ? hitColor : gizmoColor;
        DrawShapeGizmos();
    }

    [ContextMenu("Detect Once")]
    public void Detect()
    {
        BattleCollisionWorld world = BattleCollisionWorld.FindWorld();
        if (world == null)
        {
            return;
        }

        Query(world, queryResults);
        ApplyHits(queryResults);
    }

    protected abstract void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results);

    protected abstract void DrawShapeGizmos();

    protected static void DrawWireCircle(Vector2 center, float radius, float z, int segments = 32)
    {
        float safeRadius = Mathf.Max(0f, radius);
        int safeSegments = Mathf.Max(8, segments);
        Vector3 previous = new Vector3(center.x + safeRadius, center.y, z);
        for (int i = 1; i <= safeSegments; i++)
        {
            float angle = i / (float)safeSegments * Mathf.PI * 2f;
            Vector3 current = new Vector3(center.x + Mathf.Cos(angle) * safeRadius, center.y + Mathf.Sin(angle) * safeRadius, z);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    protected static void DrawWireRect(Vector2 center, Vector2 size, float rotationDeg, float z)
    {
        Vector2 halfSize = new Vector2(Mathf.Max(0f, size.x) * 0.5f, Mathf.Max(0f, size.y) * 0.5f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        Vector3 c0 = ToVector3(center + (Vector2)(rotation * new Vector3(-halfSize.x, -halfSize.y, 0f)), z);
        Vector3 c1 = ToVector3(center + (Vector2)(rotation * new Vector3(-halfSize.x, halfSize.y, 0f)), z);
        Vector3 c2 = ToVector3(center + (Vector2)(rotation * new Vector3(halfSize.x, halfSize.y, 0f)), z);
        Vector3 c3 = ToVector3(center + (Vector2)(rotation * new Vector3(halfSize.x, -halfSize.y, 0f)), z);
        Gizmos.DrawLine(c0, c1);
        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c2, c3);
        Gizmos.DrawLine(c3, c0);
    }

    protected static void DrawWireSector(Vector2 center, Vector2 forward, float radius, float angleDeg, float z, int segments = 24)
    {
        Vector2 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
        float safeRadius = Mathf.Max(0f, radius);
        float halfAngle = Mathf.Clamp(angleDeg * 0.5f, 0f, 180f);
        int safeSegments = Mathf.Max(2, segments);
        Vector2 left = BattleCollisionMath.Rotate(safeForward, halfAngle);
        Vector2 right = BattleCollisionMath.Rotate(safeForward, -halfAngle);
        Vector3 center3 = ToVector3(center, z);
        Gizmos.DrawLine(center3, ToVector3(center + left * safeRadius, z));
        Gizmos.DrawLine(center3, ToVector3(center + right * safeRadius, z));

        Vector3 previous = ToVector3(center + left * safeRadius, z);
        for (int i = 1; i <= safeSegments; i++)
        {
            float t = i / (float)safeSegments;
            float angle = Mathf.Lerp(halfAngle, -halfAngle, t);
            Vector2 point = center + BattleCollisionMath.Rotate(safeForward, angle) * safeRadius;
            Vector3 current = ToVector3(point, z);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    protected static void DrawWireCapsuleSegment(Vector2 start, Vector2 end, float width, float z, int circleSegments = 16)
    {
        float radius = Mathf.Max(0f, width) * 0.5f;
        Vector2 segment = end - start;
        Vector2 direction = segment.sqrMagnitude > 0.0001f ? segment.normalized : Vector2.right;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        Gizmos.DrawLine(ToVector3(start + normal * radius, z), ToVector3(end + normal * radius, z));
        Gizmos.DrawLine(ToVector3(start - normal * radius, z), ToVector3(end - normal * radius, z));
        DrawWireCircle(start, radius, z, circleSegments);
        DrawWireCircle(end, radius, z, circleSegments);
    }

    protected static Vector2 Position2D(Transform targetTransform)
    {
        Vector3 position = targetTransform.position;
        return new Vector2(position.x, position.y);
    }

    protected static Vector3 ToVector3(Vector2 value, float z)
    {
        return new Vector3(value.x, value.y, z);
    }

    private void ApplyHits(List<BakedSpineVitPlayer> hits)
    {
        currentHits.Clear();
        currentHitSet.Clear();
        for (int i = 0; i < hits.Count; i++)
        {
            BakedSpineVitPlayer player = hits[i];
            if (player != null && currentHitSet.Add(player))
            {
                currentHits.Add(player);
            }
        }

        for (int i = 0; i < previousHits.Count; i++)
        {
            BakedSpineVitPlayer player = previousHits[i];
            if (player != null && !currentHitSet.Contains(player))
            {
                SpineVitColorController controller = SpineVitColorController.GetOrAdd(player);
                if (controller != null)
                {
                    controller.RestoreOriginalColor();
                }
            }
        }

        for (int i = 0; i < currentHits.Count; i++)
        {
            SpineVitColorController controller = SpineVitColorController.GetOrAdd(currentHits[i]);
            if (controller != null)
            {
                controller.SetColor(hitColor);
            }
        }

        previousHits.Clear();
        previousHits.AddRange(currentHits);
    }

    private void RestorePreviousHits()
    {
        for (int i = 0; i < previousHits.Count; i++)
        {
            BakedSpineVitPlayer player = previousHits[i];
            if (player == null)
            {
                continue;
            }

            SpineVitColorController controller = SpineVitColorController.GetOrAdd(player);
            if (controller != null)
            {
                controller.RestoreOriginalColor();
            }
        }

        previousHits.Clear();
        currentHits.Clear();
        currentHitSet.Clear();
    }
}
