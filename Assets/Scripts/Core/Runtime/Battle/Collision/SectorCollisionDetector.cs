using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SectorCollisionDetector : BattleCollisionDetectorBase
{
    [SerializeField, Min(0f)] private float radius = 2f;
    [SerializeField, Range(0f, 360f)] private float angleDeg = 90f;

    protected override void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results)
    {
        world.QuerySector(Position2D(transform), transform.right, radius, angleDeg, results);
    }

    protected override void DrawShapeGizmos()
    {
        DrawWireSector(Position2D(transform), transform.right, radius, angleDeg, transform.position.z);
    }
}
