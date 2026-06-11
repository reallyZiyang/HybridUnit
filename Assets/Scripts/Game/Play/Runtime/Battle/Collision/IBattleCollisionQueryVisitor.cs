namespace Game.Play.Battle.Collision
{
    public interface IBattleCollisionQueryVisitor
    {
        bool Visit(int targetIndex);
    }
}
