namespace Game.Play.Battle.Collision
{
    public struct BattleCollisionQueryOptions
    {
        public int campMask;
        public int stateMask;
        public int layerMask;
        public int maxHits;
        public bool sortByDistance;
    }
}
