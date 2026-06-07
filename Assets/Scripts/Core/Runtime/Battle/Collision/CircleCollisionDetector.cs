using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CircleCollisionDetector : BattleCollisionDetectorBase
{
    [SerializeField, Min(0f)] private float radius = 1f;

    protected override void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results)
    {
        world.QueryCircle(Position2D(transform), radius, results);
    }

    protected override void DrawShapeGizmos()
    {
        DrawWireCircle(Position2D(transform), radius, transform.position.z);
    }
}
