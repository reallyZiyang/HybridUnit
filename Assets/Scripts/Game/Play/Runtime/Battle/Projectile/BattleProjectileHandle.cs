namespace Game.Play.Battle.Projectile
{
    public readonly struct BattleProjectileHandle
    {
        public static readonly BattleProjectileHandle Invalid = new(-1, 0);

        public readonly int index;
        public readonly int generation;

        public BattleProjectileHandle(int index, int generation)
        {
            this.index = index;
            this.generation = generation;
        }

        public bool IsValid => index >= 0 && generation > 0;
    }
}
