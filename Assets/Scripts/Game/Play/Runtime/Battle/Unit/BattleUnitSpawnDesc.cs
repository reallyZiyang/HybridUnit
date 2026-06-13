using UnityEngine;
using Game.Play.Battle.Runtime;
using ConfigBattle = Game.Data.Configs.Battle;

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
        public ConfigBattle.UnitFlag unitFlags;
        public ConfigBattle.UnitRoleFlag roleFlags;
        public int hp;
        public int renderHandle;
        public int skillSlotStart;
        public int skillSlotCount;
        public BattleAttributeValue[] attrs;
        public bool hasPushRadius;
        public float pushRadius;
        public bool hasCanPushOthers;
        public bool canPushOthers;
        public bool hasCanBePushed;
        public bool canBePushed;
    }
}
