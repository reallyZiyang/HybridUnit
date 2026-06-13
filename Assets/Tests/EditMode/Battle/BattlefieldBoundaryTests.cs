using Game.Play.Battle.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattlefieldBoundaryTests
    {
        [Test]
        public void Contains_UsesRectangle()
        {
            BattlefieldBoundaryConfig config = new()
            {
                enabled = true,
                rectWidth = 4f,
                rectHeight = 6f,
                rectCenterOffset = new Vector2(1f, -1f)
            };

            Assert.IsTrue(BattlefieldBoundary.Contains(new Vector2(1f, -1f), config));
            Assert.IsTrue(BattlefieldBoundary.Contains(new Vector2(3f, 2f), config));
            Assert.IsFalse(BattlefieldBoundary.Contains(new Vector2(3.1f, 2f), config));
            Assert.IsFalse(BattlefieldBoundary.Contains(new Vector2(1f, -4.1f), config));
        }

        [Test]
        public void Clamp_ClampsToRectangleEdges()
        {
            BattlefieldBoundaryConfig config = new()
            {
                enabled = true,
                rectWidth = 4f,
                rectHeight = 6f,
                rectCenterOffset = new Vector2(1f, -1f)
            };

            Assert.AreEqual(new Vector2(3f, 2f), BattlefieldBoundary.Clamp(new Vector2(5f, 5f), config));
            Assert.AreEqual(new Vector2(-1f, -4f), BattlefieldBoundary.Clamp(new Vector2(-4f, -8f), config));
        }

        [Test]
        public void DisabledOrInvalidRectangle_DoesNotClampOrProduceNaN()
        {
            BattlefieldBoundaryConfig config = new()
            {
                enabled = true,
                rectWidth = 0f,
                rectHeight = float.NaN,
                rectCenterOffset = new Vector2(float.PositiveInfinity, 0f)
            };

            Vector2 point = new(3f, 4f);
            Vector2 clamped = BattlefieldBoundary.Clamp(point, config);

            Assert.IsFalse(BattlefieldBoundary.IsEnabled(config));
            Assert.AreEqual(point, clamped);
            Assert.IsFalse(float.IsNaN(clamped.x));
            Assert.IsFalse(float.IsNaN(clamped.y));
        }
    }
}
