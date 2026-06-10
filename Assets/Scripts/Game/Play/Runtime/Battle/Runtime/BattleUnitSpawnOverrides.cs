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

        public BattleUnitSpawnOverrides(
            bool hasCamp = false,
            int camp = 0,
            bool hasRadius = false,
            float radius = 0f,
            bool hasLayer = false,
            int layer = 0,
            string renderKey = null,
            int[] skillIds = null,
            BattleAttributeValue[] attrs = null)
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
        }

        public static BattleUnitSpawnOverrides FromCampOverride(int campOverride)
        {
            return campOverride == 0
                ? default
                : new BattleUnitSpawnOverrides(hasCamp: true, camp: campOverride);
        }
    }
}
