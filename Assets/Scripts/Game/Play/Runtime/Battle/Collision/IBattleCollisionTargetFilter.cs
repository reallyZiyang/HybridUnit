namespace Game.Play.Battle.Collision
{
    public interface IBattleCollisionTargetFilter
    {
        bool Accept(int targetIndex);
    }
}
