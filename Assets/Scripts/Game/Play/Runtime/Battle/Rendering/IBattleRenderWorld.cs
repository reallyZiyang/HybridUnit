using UnityEngine;
using Game.Play.Battle.Runtime;

namespace Game.Play.Battle.Rendering
{
    public interface IBattleRenderWorld
    {
        int SpawnUnit(string renderKey, Vector2 position);
        int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg);
        int PlayUnitAction(int renderHandle, string actionName);
        int PlayUnitAction(int renderHandle, string actionName, float speed);
        void PlayUnitIdle(int renderHandle);
        void PlayUnitWalk(int renderHandle);
        int PlayUnitHit(int renderHandle);
        void PlayUnitDead(int renderHandle);
        void ShowDamageText(Vector2 worldPosition, long value);
        void ShowHealText(Vector2 worldPosition, long value);
        void SetPaused(bool paused);
        void SetSortingGrid(float gridMinY, float cellSize);
        void SetBattlefieldBoundary(BattlefieldBoundaryConfig config);
        void SetPosition(int renderHandle, Vector2 position);
        void SetRotation(int renderHandle, float angleDeg);
        void SetUnitFlipX(int renderHandle, bool flipX);
        void SetVisible(int renderHandle, bool visible);
        void Despawn(int renderHandle);
        void Tick(float deltaTime);
        void Clear();
    }
}
