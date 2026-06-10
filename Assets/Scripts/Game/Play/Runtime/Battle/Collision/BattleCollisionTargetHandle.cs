namespace Game.Play.Battle.Collision
{
    public readonly struct BattleCollisionTargetHandle
    {
        public static readonly BattleCollisionTargetHandle Invalid = new(-1, 0);

        public readonly int index;
        public readonly int generation;

        public BattleCollisionTargetHandle(int index, int generation)
        {
            this.index = index;
            this.generation = generation;
        }

        public bool IsValid => index >= 0 && generation > 0;
    }
}
