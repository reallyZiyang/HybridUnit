using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CapsuleSegmentCollisionDetector : BattleCollisionDetectorBase
{
    [SerializeField] private Vector2 localStart = new Vector2(-1f, 0f);
    [SerializeField] private Vector2 localEnd = new Vector2(1f, 0f);
    [SerializeField, Min(0f)] private float width = 0.5f;

    protected override void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results)
    {
        GetWorldSegment(out Vector2 start, out Vector2 end);
        world.QueryCapsuleSegment(start, end, width, results);
    }

    protected override void DrawShapeGizmos()
    {
        GetWorldSegment(out Vector2 start, out Vector2 end);
        DrawWireCapsuleSegment(start, end, width, transform.position.z);
    }

    private void GetWorldSegment(out Vector2 start, out Vector2 end)
    {
        Vector3 start3 = transform.TransformPoint(localStart);
        Vector3 end3 = transform.TransformPoint(localEnd);
        start = new Vector2(start3.x, start3.y);
        end = new Vector2(end3.x, end3.y);
    }
}
