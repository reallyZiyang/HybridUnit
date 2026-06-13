using UnityEngine;
using Game.Play.Battle.Runtime;

namespace Game.Play.Battle.Rendering
{
    public sealed class GameObjectBattleRenderWorld : IBattleRenderWorld
    {
        private readonly DrawMeshBattleRenderWorld inner = new();

        public int SpawnUnit(string renderKey, Vector2 position)
        {
            return inner.SpawnUnit(renderKey, position);
        }

        public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg)
        {
            return inner.SpawnProjectile(projectileKey, position, angleDeg);
        }

        public int PlayUnitAction(int renderHandle, string actionName)
        {
            return inner.PlayUnitAction(renderHandle, actionName);
        }

        public int PlayUnitAction(int renderHandle, string actionName, float speed)
        {
            return inner.PlayUnitAction(renderHandle, actionName, speed);
        }

        public void PlayUnitIdle(int renderHandle)
        {
            inner.PlayUnitIdle(renderHandle);
        }

        public void PlayUnitWalk(int renderHandle)
        {
            inner.PlayUnitWalk(renderHandle);
        }

        public int PlayUnitHit(int renderHandle)
        {
            return inner.PlayUnitHit(renderHandle);
        }

        public void PlayUnitDead(int renderHandle)
        {
            inner.PlayUnitDead(renderHandle);
        }

        public void ShowDamageText(Vector2 worldPosition, long value)
        {
            inner.ShowDamageText(worldPosition, value);
        }

        public void ShowHealText(Vector2 worldPosition, long value)
        {
            inner.ShowHealText(worldPosition, value);
        }

        public void SetPaused(bool paused)
        {
            inner.SetPaused(paused);
        }

        public void SetSortingGrid(float gridMinY, float cellSize)
        {
            inner.SetSortingGrid(gridMinY, cellSize);
        }

        public void SetBattlefieldBoundary(BattlefieldBoundaryConfig config)
        {
            inner.SetBattlefieldBoundary(config);
        }

        public void SetPosition(int renderHandle, Vector2 position)
        {
            inner.SetPosition(renderHandle, position);
        }

        public void SetRotation(int renderHandle, float angleDeg)
        {
            inner.SetRotation(renderHandle, angleDeg);
        }

        public void SetUnitFlipX(int renderHandle, bool flipX)
        {
            inner.SetUnitFlipX(renderHandle, flipX);
        }

        public void SetVisible(int renderHandle, bool visible)
        {
            inner.SetVisible(renderHandle, visible);
        }

        public void Despawn(int renderHandle)
        {
            inner.Despawn(renderHandle);
        }

        public void Tick(float deltaTime)
        {
            inner.Tick(deltaTime);
        }

        public void Clear()
        {
            inner.Clear();
        }
    }
}
