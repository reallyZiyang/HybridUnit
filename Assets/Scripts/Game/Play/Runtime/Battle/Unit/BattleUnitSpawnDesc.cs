using UnityEngine;
using Game.Play.Battle.Runtime;

namespace Game.Play.Battle.Unit
{
    public struct BattleUnitSpawnDesc
    {
        public int unitCfgId;
        public Vector2 position;
        public float radius;
        public int camp;
        public int state;
        public int layer;
        public int hp;
        public int renderHandle;
        public int skillSlotStart;
        public int skillSlotCount;
        public BattleAttributeValue[] attrs;
    }
}
