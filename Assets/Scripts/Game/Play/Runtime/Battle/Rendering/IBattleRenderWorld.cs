using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public interface IBattleRenderWorld
    {
        int SpawnUnit(string renderKey, Vector2 position);
        int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg);
        void PlayAction(int renderHandle, string actionName);
        void SetPosition(int renderHandle, Vector2 position);
        void SetRotation(int renderHandle, float angleDeg);
        void SetVisible(int renderHandle, bool visible);
        void Despawn(int renderHandle);
        void Tick(float deltaTime);
        void Clear();
    }
}
