using Game.Play.Battle.Collision;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattleCollisionMathTests
    {
        [Test]
        public void CircleHitsCircle_IncludesTargetRadius()
        {
            Assert.IsTrue(BattleCollisionMath.CircleHitsCircle(Vector2.zero, 1f, new Vector2(1.5f, 0f), 0.5f));
            Assert.IsFalse(BattleCollisionMath.CircleHitsCircle(Vector2.zero, 1f, new Vector2(1.51f, 0f), 0.5f));
        }

        [Test]
        public void RectHitsCircle_UsesDirection()
        {
            Vector2 direction45 = new Vector2(1f, 1f);
            Assert.IsTrue(BattleCollisionMath.RectHitsCircle(Vector2.zero, new Vector2(2f, 1f), direction45, new Vector2(0.7f, 0.7f), 0.05f));
            Assert.IsFalse(BattleCollisionMath.RectHitsCircle(Vector2.zero, new Vector2(2f, 1f), direction45, new Vector2(-1f, 1f), 0.05f));
        }

        [Test]
        public void SectorHitsCircle_RejectsBackAndAllowsEdgeRadius()
        {
            Assert.IsFalse(BattleCollisionMath.SectorHitsCircle(Vector2.zero, Vector2.right, 3f, 90f, Vector2.left, 0.2f));
            Assert.IsTrue(BattleCollisionMath.SectorHitsCircle(Vector2.zero, Vector2.right, 3f, 90f, new Vector2(2f, 2.1f), 0.8f));
        }

        [Test]
        public void CapsuleSegmentHitsCircle_HandlesSweepAndZeroLength()
        {
            Assert.IsTrue(BattleCollisionMath.CapsuleSegmentHitsCircle(Vector2.zero, new Vector2(4f, 0f), 0.5f, new Vector2(2f, 0.49f), 0.25f));
            Assert.IsFalse(BattleCollisionMath.CapsuleSegmentHitsCircle(Vector2.zero, new Vector2(4f, 0f), 0.5f, new Vector2(2f, 0.76f), 0.25f));
            Assert.IsTrue(BattleCollisionMath.CapsuleSegmentHitsCircle(Vector2.one, Vector2.one, 1f, new Vector2(1.75f, 1f), 0.25f));
        }
    }
}
