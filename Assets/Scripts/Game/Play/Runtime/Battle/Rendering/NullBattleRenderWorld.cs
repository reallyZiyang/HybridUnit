using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public sealed class NullBattleRenderWorld : IBattleRenderWorld
    {
        private int nextHandle = 1;

        public int SpawnUnit(string renderKey, Vector2 position) => nextHandle++;
        public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg) => nextHandle++;
        public int PlayUnitAction(int renderHandle, string actionName) => 0;
        public void PlayUnitIdle(int renderHandle) { }
        public void PlayUnitWalk(int renderHandle) { }
        public int PlayUnitHit(int renderHandle) => DrawMeshUnitRenderer.DefaultHitLockMs;
        public void PlayUnitDead(int renderHandle) { }
        public void ShowDamageText(Vector2 worldPosition, long value) { }
        public void ShowHealText(Vector2 worldPosition, long value) { }
        public void SetPaused(bool paused) { }
        public void SetPosition(int renderHandle, Vector2 position) { }
        public void SetRotation(int renderHandle, float angleDeg) { }
        public void SetUnitFlipX(int renderHandle, bool flipX) { }
        public void SetVisible(int renderHandle, bool visible) { }
        public void Despawn(int renderHandle) { }
        public void Tick(float deltaTime) { }
        public void Clear() { }
    }
}
