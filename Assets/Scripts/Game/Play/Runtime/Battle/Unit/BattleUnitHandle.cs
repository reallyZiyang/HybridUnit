namespace Game.Play.Battle.Unit
{
    public readonly struct BattleUnitHandle
    {
        public static readonly BattleUnitHandle Invalid = new(-1, 0);

        public readonly int index;
        public readonly int generation;

        public BattleUnitHandle(int index, int generation)
        {
            this.index = index;
            this.generation = generation;
        }

        public bool IsValid => index >= 0 && generation > 0;
    }
}
