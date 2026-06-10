namespace Game.Play.Battle.Unit
{
    public static class BattleUnitStates
    {
        public const int Alive = 1 << 0;
        public const int Dead = 1 << 1;
        public const int Selectable = 1 << 2;
        public const int Invincible = 1 << 3;
    }
}
