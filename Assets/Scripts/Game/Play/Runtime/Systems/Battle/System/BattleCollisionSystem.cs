using Game.Play.Base.Attributes;
using Game.Play.Battle.Collision;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.Interface;
using UniKit.Framework.Base;
using UnityEngine;

namespace Game.Play.Systems.Battle.System
{
    [Order(10000)]
    public sealed class BattleCollisionSystem : AbstractSystem, IBattleCollisionSystem
    {
        public BattleUnitManager UnitManager { get; private set; }
        public BattleCollisionManager CollisionManager { get; private set; }
        public bool IsBattleInitialized => UnitManager != null && CollisionManager != null;

        public void InitializeBattle(
            int unitCapacity,
            int collisionCapacity,
            Vector2 gridMin,
            int gridWidth,
            int gridHeight,
            float cellSize,
            float largeQueryCellRatio = 0.35f,
            int maxGridLinks = 0)
        {
            DisposeBattle();

            UnitManager = new BattleUnitManager(unitCapacity);
            CollisionManager = new BattleCollisionManager(
                collisionCapacity,
                gridMin,
                gridWidth,
                gridHeight,
                cellSize,
                largeQueryCellRatio,
                maxGridLinks);
        }

        public void SyncUnitsToCollision()
        {
            UnitManager?.SyncCollisionTargets(CollisionManager);
        }

        public void RebuildCollisionGrid()
        {
            CollisionManager?.RebuildGrid();
        }

        public void DisposeBattle()
        {
            UnitManager = null;
            CollisionManager = null;
        }

        protected override void OnDispose()
        {
            DisposeBattle();
        }
    }
}
