using UnityEngine;

namespace Game.Play.Battle.Rendering
{
    public sealed class NullBattleRenderWorld : IBattleRenderWorld
    {
        private int nextHandle = 1;

        public int SpawnUnit(string renderKey, Vector2 position) => nextHandle++;
        public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg) => nextHandle++;
        public void PlayAction(int renderHandle, string actionName) { }
        public void SetPosition(int renderHandle, Vector2 position) { }
        public void SetRotation(int renderHandle, float angleDeg) { }
        public void SetVisible(int renderHandle, bool visible) { }
        public void Despawn(int renderHandle) { }
        public void Tick(float deltaTime) { }
        public void Clear() { }
    }
}
