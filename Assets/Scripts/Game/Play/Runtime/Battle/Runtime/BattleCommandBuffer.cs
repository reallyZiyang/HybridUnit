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
        public readonly BattleEffectContext effectContext;

        private BattleCommand(BattleCommandType type, BattleUnitHandle source, BattleUnitHandle target, long value, int id, int durationMs, int stack, Vector2 position, Vector2 direction, bool playHitReaction, BattleEffectContext effectContext)
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
            this.effectContext = effectContext;
        }

        public static BattleCommand Damage(BattleUnitHandle source, BattleUnitHandle target, long value, bool playHitReaction, BattleEffectContext effectContext)
            => new(BattleCommandType.Damage, source, target, value, 0, 0, 0, default, default, playHitReaction, effectContext);

        public static BattleCommand Heal(BattleUnitHandle source, BattleUnitHandle target, long value, BattleEffectContext effectContext)
            => new(BattleCommandType.Heal, source, target, value, 0, 0, 0, default, default, false, effectContext);

        public static BattleCommand AddBuff(BattleUnitHandle source, BattleUnitHandle target, int buffId, int durationMs, int stack, BattleEffectContext effectContext)
            => new(BattleCommandType.AddBuff, source, target, 0, buffId, durationMs, stack, default, default, false, effectContext);

        public static BattleCommand SpawnProjectile(BattleUnitHandle source, BattleUnitHandle target, int projectileId, Vector2 position, Vector2 direction, BattleEffectContext effectContext)
            => new(BattleCommandType.SpawnProjectile, source, target, 0, projectileId, 0, 0, position, direction, false, effectContext);

        public static BattleCommand DespawnUnit(BattleUnitHandle target)
            => new(BattleCommandType.DespawnUnit, BattleUnitHandle.Invalid, target, 0, 0, 0, 0, default, default, false, BattleEffectContext.None);
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
            AddDamage(source, target, value, playHitReaction, BattleEffectContext.None);
        }

        public void AddDamage(BattleUnitHandle source, BattleUnitHandle target, long value, bool playHitReaction, BattleEffectContext effectContext)
        {
            commands.Add(BattleCommand.Damage(source, target, value, playHitReaction, effectContext));
        }

        public void AddHeal(BattleUnitHandle source, BattleUnitHandle target, long value)
        {
            AddHeal(source, target, value, BattleEffectContext.None);
        }

        public void AddHeal(BattleUnitHandle source, BattleUnitHandle target, long value, BattleEffectContext effectContext)
        {
            commands.Add(BattleCommand.Heal(source, target, value, effectContext));
        }

        public void AddBuff(BattleUnitHandle source, BattleUnitHandle target, int buffId, int durationMs, int stack)
        {
            AddBuff(source, target, buffId, durationMs, stack, BattleEffectContext.None);
        }

        public void AddBuff(BattleUnitHandle source, BattleUnitHandle target, int buffId, int durationMs, int stack, BattleEffectContext effectContext)
        {
            commands.Add(BattleCommand.AddBuff(source, target, buffId, durationMs, stack, effectContext));
        }

        public void SpawnProjectile(BattleUnitHandle source, BattleUnitHandle target, int projectileId, Vector2 position, Vector2 direction)
        {
            SpawnProjectile(source, target, projectileId, position, direction, BattleEffectContext.None);
        }

        public void SpawnProjectile(BattleUnitHandle source, BattleUnitHandle target, int projectileId, Vector2 position, Vector2 direction, BattleEffectContext effectContext)
        {
            commands.Add(BattleCommand.SpawnProjectile(source, target, projectileId, position, direction, effectContext));
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
