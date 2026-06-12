using Game.Play.Battle.Unit;

namespace Game.Play.Battle.Runtime
{
    public readonly struct BattleUnitSpawnOverrides
    {
        public readonly bool hasCamp;
        public readonly int camp;
        public readonly bool hasRadius;
        public readonly float radius;
        public readonly bool hasLayer;
        public readonly int layer;
        public readonly string renderKey;
        public readonly int[] skillIds;
        public readonly BattleAttributeValue[] attrs;
        public readonly bool hasPushRadius;
        public readonly float pushRadius;
        public readonly bool hasCanPushOthers;
        public readonly bool canPushOthers;
        public readonly bool hasCanBePushed;
        public readonly bool canBePushed;

        public BattleUnitSpawnOverrides(
            bool hasCamp = false,
            int camp = 0,
            bool hasRadius = false,
            float radius = 0f,
            bool hasLayer = false,
            int layer = 0,
            string renderKey = null,
            int[] skillIds = null,
            BattleAttributeValue[] attrs = null,
            bool hasPushRadius = false,
            float pushRadius = 0f,
            bool hasCanPushOthers = false,
            bool canPushOthers = true,
            bool hasCanBePushed = false,
            bool canBePushed = true)
        {
            this.hasCamp = hasCamp;
            this.camp = camp;
            this.hasRadius = hasRadius;
            this.radius = radius;
            this.hasLayer = hasLayer;
            this.layer = layer;
            this.renderKey = renderKey;
            this.skillIds = skillIds;
            this.attrs = attrs;
            this.hasPushRadius = hasPushRadius;
            this.pushRadius = pushRadius;
            this.hasCanPushOthers = hasCanPushOthers;
            this.canPushOthers = canPushOthers;
            this.hasCanBePushed = hasCanBePushed;
            this.canBePushed = canBePushed;
        }

        public static BattleUnitSpawnOverrides FromCampOverride(int campOverride)
        {
            return campOverride == 0
                ? default
                : new BattleUnitSpawnOverrides(hasCamp: true, camp: campOverride);
        }
    }
}
