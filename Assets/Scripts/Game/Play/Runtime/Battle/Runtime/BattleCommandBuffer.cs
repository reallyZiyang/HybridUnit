using System.Collections.Generic;
using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Runtime
{
    public enum BattleCommandType
    {
        Damage,
        Heal,
        AddBuff,
        SpawnProjectile,
        DespawnUnit
    }

    public readonly struct BattleCommand
    {
        public readonly BattleCommandType type;
        public readonly BattleUnitHandle source;
        public readonly BattleUnitHandle target;
        public readonly long value;
        public readonly int id;
        public readonly int durationMs;
        public readonly int stack;
        public readonly Vector2 position;
        public readonly Vector2 direction;
        public readonly bool playHitReaction;

        private BattleCommand(BattleCommandType type, BattleUnitHandle source, BattleUnitHandle target, long value, int id, int durationMs, int stack, Vector2 position, Vector2 direction, bool playHitReaction)
        {
            this.type = type;
            this.source = source;
            this.target = target;
            this.value = value;
            this.id = id;
            this.durationMs = durationMs;
            this.stack = stack;
            this.position = position;
            this.direction = direction;
            this.playHitReaction = playHitReaction;
        }

        public static BattleCommand Damage(BattleUnitHandle source, BattleUnitHandle target, long value, bool playHitReaction)
            => new(BattleCommandType.Damage, source, target, value, 0, 0, 0, default, default, playHitReaction);

        public static BattleCommand Heal(BattleUnitHandle source, BattleUnitHandle target, long value)
            => new(BattleCommandType.Heal, source, target, value, 0, 0, 0, default, default, false);

        public static BattleCommand AddBuff(BattleUnitHandle source, BattleUnitHandle target, int buffId, int durationMs, int stack)
            => new(BattleCommandType.AddBuff, source, target, 0, buffId, durationMs, stack, default, default, false);

        public static BattleCommand SpawnProjectile(BattleUnitHandle source, BattleUnitHandle target, int projectileId, Vector2 position, Vector2 direction)
            => new(BattleCommandType.SpawnProjectile, source, target, 0, projectileId, 0, 0, position, direction, false);

        public static BattleCommand DespawnUnit(BattleUnitHandle target)
            => new(BattleCommandType.DespawnUnit, BattleUnitHandle.Invalid, target, 0, 0, 0, 0, default, default, false);
    }

    public sealed class BattleCommandBuffer
    {
        private readonly List<BattleCommand> commands;

        public BattleCommandBuffer(int capacity = 128)
        {
            commands = new List<BattleCommand>(capacity);
        }

        public int Count => commands.Count;

        public BattleCommand this[int index] => commands[index];

        public void AddDamage(BattleUnitHandle source, BattleUnitHandle target, long value, bool playHitReaction)
        {
            commands.Add(BattleCommand.Damage(source, target, value, playHitReaction));
        }

        public void AddHeal(BattleUnitHandle source, BattleUnitHandle target, long value)
        {
            commands.Add(BattleCommand.Heal(source, target, value));
        }

        public void AddBuff(BattleUnitHandle source, BattleUnitHandle target, int buffId, int durationMs, int stack)
        {
            commands.Add(BattleCommand.AddBuff(source, target, buffId, durationMs, stack));
        }

        public void SpawnProjectile(BattleUnitHandle source, BattleUnitHandle target, int projectileId, Vector2 position, Vector2 direction)
        {
            commands.Add(BattleCommand.SpawnProjectile(source, target, projectileId, position, direction));
        }

        public void DespawnUnit(BattleUnitHandle target)
        {
            commands.Add(BattleCommand.DespawnUnit(target));
        }

        public void Clear()
        {
            commands.Clear();
        }
    }
}
