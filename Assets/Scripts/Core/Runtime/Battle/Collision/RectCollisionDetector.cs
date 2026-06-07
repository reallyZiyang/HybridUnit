using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RectCollisionDetector : BattleCollisionDetectorBase
{
    [SerializeField] private Vector2 size = new Vector2(2f, 1f);

    protected override void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results)
    {
        world.QueryRect(Position2D(transform), size, transform.eulerAngles.z, results);
    }

    protected override void DrawShapeGizmos()
    {
        DrawWireRect(Position2D(transform), size, transform.eulerAngles.z, transform.position.z);
    }
}
