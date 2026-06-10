using Game.Play.Battle.Collision;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattleCollisionManagerTests
    {
        [Test]
        public void Query_DeduplicatesTargetsAcrossCells()
        {
            BattleCollisionManager manager = CreateManager();
            manager.RegisterTarget(new Vector2(0.95f, 0.95f), 0.4f, 1, 1, 0, 10);
            manager.RebuildGrid();

            BattleCollisionQueryBuffer buffer = new(8);
            int count = manager.Query(new BattleCollisionShape
            {
                type = BattleCollisionShapeType.Circle,
                center = new Vector2(0.95f, 0.95f),
                radius = 0.2f
            }, default, buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void Query_MatchesBruteForceForSmallShape()
        {
            BattleCollisionManager manager = CreateManager();
            for (int i = 0; i < 30; i++)
            {
                float x = -4f + i * 0.3f;
                float y = i % 2 == 0 ? 0.5f : 2f;
                manager.RegisterTarget(new Vector2(x, y), 0.2f, i % 2, 1, 0, i);
            }

            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = Vector2.zero,
                radius = 3f
            };
            BattleCollisionQueryOptions options = new()
            {
                campMask = 1 << 1,
                stateMask = 1,
                layerMask = 1 << 0
            };

            BattleCollisionQueryBuffer gridBuffer = new(64);
            BattleCollisionQueryBuffer bruteBuffer = new(64);
            manager.Query(shape, options, gridBuffer);
            manager.BruteForceQuery(shape, options, bruteBuffer);

            AssertSameTargets(gridBuffer, bruteBuffer);
        }

        [Test]
        public void Query_LargeShapeFallbackMatchesBruteForce()
        {
            BattleCollisionManager manager = CreateManager(gridWidth: 4, gridHeight: 4);
            for (int i = 0; i < 20; i++)
            {
                manager.RegisterTarget(new Vector2(-2f + i * 0.2f, 0f), 0.1f, 0, 1, 0, i);
            }

            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = Vector2.zero,
                radius = 100f
            };

            BattleCollisionQueryBuffer queryBuffer = new(32);
            BattleCollisionQueryBuffer bruteBuffer = new(32);
            manager.Query(shape, default, queryBuffer);
            manager.BruteForceQuery(shape, default, bruteBuffer);

            AssertSameTargets(queryBuffer, bruteBuffer);
        }

        [Test]
        public void Query_DoesNotReturnUnregisteredTarget()
        {
            BattleCollisionManager manager = CreateManager();
            BattleCollisionTargetHandle target = manager.RegisterTarget(Vector2.zero, 0.2f, 0, 1, 0, 1);
            manager.RebuildGrid();
            Assert.IsTrue(manager.UnregisterTarget(target));

            BattleCollisionQueryBuffer buffer = new(8);
            int count = manager.Query(new BattleCollisionShape
            {
                type = BattleCollisionShapeType.Circle,
                center = Vector2.zero,
                radius = 1f
            }, default, buffer);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Query_FiltersAndSortsByDistance()
        {
            BattleCollisionManager manager = CreateManager();
            manager.RegisterTarget(new Vector2(3f, 0f), 0.1f, 1, 1, 0, 1);
            BattleCollisionTargetHandle near = manager.RegisterTarget(new Vector2(1f, 0f), 0.1f, 1, 1, 0, 2);
            manager.RegisterTarget(new Vector2(0.5f, 0f), 0.1f, 0, 1, 0, 3);
            manager.RegisterTarget(new Vector2(0.25f, 0f), 0.1f, 1, 2, 0, 4);

            BattleCollisionQueryBuffer buffer = new(8);
            int count = manager.Query(new BattleCollisionShape
            {
                type = BattleCollisionShapeType.Circle,
                center = Vector2.zero,
                radius = 4f
            }, new BattleCollisionQueryOptions
            {
                campMask = 1 << 1,
                stateMask = 1,
                layerMask = 1 << 0,
                maxHits = 1,
                sortByDistance = true
            }, buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual(near.index, buffer.TargetIndices[0]);
        }

        private static BattleCollisionManager CreateManager(int gridWidth = 10, int gridHeight = 10)
        {
            return new BattleCollisionManager(128, new Vector2(-5f, -5f), gridWidth, gridHeight, 1f);
        }

        private static void AssertSameTargets(BattleCollisionQueryBuffer a, BattleCollisionQueryBuffer b)
        {
            Assert.AreEqual(b.Count, a.Count);
            for (int i = 0; i < a.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Count; j++)
                {
                    if (a.TargetIndices[i] == b.TargetIndices[j])
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, $"Missing target {a.TargetIndices[i]} in brute force result.");
            }
        }
    }
}
