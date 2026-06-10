using Game.Play.Battle.Collision;
using Game.Play.Battle.Unit;
using UniKit.Framework.Base;
using UnityEngine;

namespace Game.Play.Systems.Battle.Interface
{
    public interface IBattleCollisionSystem : ISystem
    {
        BattleUnitManager UnitManager { get; }
        BattleCollisionManager CollisionManager { get; }
        bool IsBattleInitialized { get; }

        void InitializeBattle(
            int unitCapacity,
            int collisionCapacity,
            Vector2 gridMin,
            int gridWidth,
            int gridHeight,
            float cellSize,
            float largeQueryCellRatio = 0.35f,
            int maxGridLinks = 0);

        void SyncUnitsToCollision();
        void RebuildCollisionGrid();
        void DisposeBattle();
    }
}
