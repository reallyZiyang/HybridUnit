using Game.Data.Configs;
using Game.Play.Adapters;
using Game.Play.Base.Attributes;
using Game.Play.Battle.AI;
using Game.Play.Battle.Buff;
using Game.Play.Battle.Collision;
using Game.Play.Battle.Projectile;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Skill;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.Interface;
using UniKit.Framework.Base;
using UnityEngine;

namespace Game.Play.Systems.Battle.System
{
    [Order(10001)]
    public sealed class BattleRuntimeSystem : AbstractSystem, IBattleRuntimeSystem
    {
        private const int DefaultCommandFlushGuard = 8192;
        private static readonly Vector2 FloatTextOffset = new(0f, 0.8f);

        private IBattleCollisionSystem collisionSystem;
        private BattleCollisionManager localCollisionManager;
        private BattleCommandBuffer commands;
        private BattleEffectExecutor effects;
        private BattleAISystem ai;
        private BattleSkillManager skills;
        private BattleBuffManager buffs;
        private BattleProjectileManager projectiles;
        private IBattleRenderWorld renderWorld;
        private int logicStepMs = 33;
        private float accumulatedMs;

        public BattleRuntimeData RuntimeData { get; private set; }
        public BattleUnitManager UnitManager { get; private set; }
        public bool IsInitialized => RuntimeData != null && UnitManager != null && CollisionManager != null;
        public bool IsPaused { get; private set; }

        private BattleCollisionManager CollisionManager => collisionSystem?.CollisionManager ?? localCollisionManager;

        protected override void OnInitialize()
        {
            if (Context != null)
            {
                try
                {
                    collisionSystem = Context.GetSystem<IBattleCollisionSystem>();
                }
                catch
                {
                    collisionSystem = null;
                }
            }
        }

        public void InitializeBattle(
            Tables tables,
            int unitCapacity,
            int projectileCapacity,
            int buffCapacity,
            Vector2 gridMin,
            int gridWidth,
            int gridHeight,
            float cellSize,
            IBattleRenderWorld renderWorld = null,
            int logicStepMs = 33,
            int skillSlotsPerUnit = 0)
        {
            DisposeBattle();

            Tables safeTables = tables ?? API.Tables;
            RuntimeData = BattleRuntimeData.Build(safeTables);
            this.logicStepMs = Mathf.Max(1, logicStepMs);
            this.renderWorld = renderWorld ?? new DrawMeshBattleRenderWorld();
            accumulatedMs = 0f;
            IsPaused = false;

            if (collisionSystem != null)
            {
                collisionSystem.InitializeBattle(unitCapacity, unitCapacity, gridMin, gridWidth, gridHeight, cellSize);
                UnitManager = collisionSystem.UnitManager;
            }
            else
            {
                UnitManager = new BattleUnitManager(unitCapacity, BattleAttributeRegistry.Count);
                localCollisionManager = new BattleCollisionManager(unitCapacity, gridMin, gridWidth, gridHeight, cellSize);
            }

            commands = new BattleCommandBuffer();
            effects = new BattleEffectExecutor(RuntimeData, UnitManager, commands);
            int slotsPerUnit = Mathf.Max(1, RuntimeData.MaxDefaultSkillCount, skillSlotsPerUnit);
            skills = new BattleSkillManager(RuntimeData, UnitManager, CollisionManager, effects, this.renderWorld, unitCapacity, slotsPerUnit, unitCapacity);
            ai = new BattleAISystem(UnitManager, CollisionManager, skills, this.renderWorld, unitCapacity);
            buffs = new BattleBuffManager(RuntimeData, UnitManager, effects, buffCapacity);
            projectiles = new BattleProjectileManager(RuntimeData, UnitManager, CollisionManager, effects, this.renderWorld, projectileCapacity, unitCapacity);
        }

        public BattleUnitHandle SpawnUnit(int unitCfgId, Vector2 position, int campOverride = 0)
        {
            return SpawnUnit(unitCfgId, position, BattleUnitSpawnOverrides.FromCampOverride(campOverride));
        }

        public BattleUnitHandle SpawnUnit(int unitCfgId, Vector2 position, BattleUnitSpawnOverrides overrides)
        {
            if (!IsInitialized || !RuntimeData.TryGetUnit(unitCfgId, out BattleUnitRuntimeData unitData))
            {
                return BattleUnitHandle.Invalid;
            }

            string renderKey = string.IsNullOrEmpty(overrides.renderKey) ? unitData.renderKey : overrides.renderKey;
            int[] skillIds = ResolveSkillIds(unitData.defaultSkills, overrides.skillIds);
            BattleAttributeValue[] attrs = ResolveAttrs(unitData.attrs, overrides.attrs);
            int renderHandle = renderWorld?.SpawnUnit(renderKey, position) ?? -1;
            BattleUnitSpawnDesc desc = new()
            {
                unitCfgId = unitData.id,
                position = position,
                radius = overrides.hasRadius ? overrides.radius : unitData.radius,
                camp = overrides.hasCamp ? overrides.camp : unitData.camp,
                state = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layer = overrides.hasLayer ? overrides.layer : unitData.layer,
                renderHandle = renderHandle,
                skillSlotCount = skillIds.Length,
                attrs = attrs
            };

            BattleUnitHandle unit = UnitManager.SpawnUnit(desc);
            if (!unit.IsValid)
            {
                renderWorld?.Despawn(renderHandle);
                return BattleUnitHandle.Invalid;
            }

            skills.BindUnitSkills(unit, skillIds);
            UnitManager.SetSkillSlots(unit, skills.GetSlotStart(unit), Mathf.Min(skills.SlotsPerUnit, skillIds.Length));
            UnitManager.RegisterCollisionTarget(unit, CollisionManager);
            return unit;
        }

        public bool DespawnUnit(BattleUnitHandle unit)
        {
            return DespawnUnit(unit, true);
        }

        private bool DespawnUnit(BattleUnitHandle unit, bool despawnRender)
        {
            if (!IsInitialized || !UnitManager.IsValid(unit))
            {
                return false;
            }

            int renderHandle = UnitManager.GetRenderHandle(unit);
            buffs.RemoveUnitBuffs(unit);
            skills.ClearUnitSkills(unit);
            bool despawned = UnitManager.DespawnUnit(unit, CollisionManager);
            if (despawnRender)
            {
                renderWorld?.Despawn(renderHandle);
            }

            return despawned;
        }

        public bool CastSkill(BattleUnitHandle caster, int skillId)
        {
            return IsInitialized && skills.TryCastSkill(caster, skillId);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            renderWorld?.SetPaused(paused);
        }

        public void OnUpdate(float deltaTime)
        {
            if (!IsInitialized)
            {
                return;
            }

            if (IsPaused)
            {
                renderWorld?.Tick(0f);
                return;
            }

            accumulatedMs += Mathf.Max(0f, deltaTime) * 1000f;
            bool tickedLogic = false;
            while (accumulatedMs >= logicStepMs)
            {
                TickLogic(logicStepMs);
                accumulatedMs -= logicStepMs;
                tickedLogic = true;
            }

            float renderAlpha = ResolveRenderAlpha(tickedLogic);
            SyncRenderPositions(renderAlpha);
            renderWorld?.Tick(deltaTime);
        }

        public void DisposeBattle()
        {
            renderWorld?.Clear();
            RuntimeData = null;
            commands = null;
            effects = null;
            ai = null;
            skills = null;
            buffs = null;
            projectiles = null;
            UnitManager = null;
            accumulatedMs = 0f;
            if (collisionSystem != null)
            {
                collisionSystem.DisposeBattle();
            }
            else
            {
                localCollisionManager = null;
            }
        }

        protected override void OnDispose()
        {
            DisposeBattle();
        }

        private void TickLogic(int deltaMs)
        {
            UnitManager.CapturePreviousPositions();
            UnitManager.TickHitLocks(deltaMs);
            UnitManager.SyncCollisionTargets(CollisionManager);
            CollisionManager.RebuildGrid();
            ai.Tick(deltaMs);
            UnitManager.SyncCollisionTargets(CollisionManager);
            CollisionManager.RebuildGrid();
            skills.Tick(deltaMs);
            buffs.Tick(deltaMs);
            projectiles.Tick(deltaMs);
            FlushCommands();
            UnitManager.SyncCollisionTargets(CollisionManager);
        }

        private void FlushCommands()
        {
            int cursor = 0;
            int guard = 0;
            while (cursor < commands.Count && guard++ < DefaultCommandFlushGuard)
            {
                BattleCommand command = commands[cursor++];
                ExecuteCommand(command);
            }

            commands.Clear();
        }

        private void ExecuteCommand(BattleCommand command)
        {
            switch (command.type)
            {
                case BattleCommandType.Damage:
                    int hpBeforeDamage = UnitManager.GetHp(command.target);
                    Vector2 damageTextPosition = UnitManager.GetPosition(command.target) + FloatTextOffset;
                    bool damageApplied = UnitManager.ApplyDamage(command.target, command.value, CollisionManager);
                    int damageDelta = Mathf.Max(0, hpBeforeDamage - UnitManager.GetHp(command.target));
                    if (damageApplied && damageDelta > 0)
                    {
                        renderWorld?.ShowDamageText(damageTextPosition, damageDelta);
                    }

                    if (!UnitManager.IsAlive(command.target))
                    {
                        int renderHandle = UnitManager.GetRenderHandle(command.target);
                        renderWorld?.PlayUnitDead(renderHandle);
                        DespawnUnit(command.target, false);
                    }
                    else if (command.playHitReaction && !UnitManager.HasEndure(command.target))
                    {
                        int hitDurationMs = renderWorld?.PlayUnitHit(UnitManager.GetRenderHandle(command.target)) ?? 0;
                        skills.InterruptUnitCast(command.target);
                        UnitManager.ApplyHitLock(command.target, hitDurationMs);
                    }
                    break;
                case BattleCommandType.Heal:
                    int hpBeforeHeal = UnitManager.GetHp(command.target);
                    Vector2 healTextPosition = UnitManager.GetPosition(command.target) + FloatTextOffset;
                    bool healApplied = UnitManager.ApplyHeal(command.target, command.value);
                    int healDelta = Mathf.Max(0, UnitManager.GetHp(command.target) - hpBeforeHeal);
                    if (healApplied && healDelta > 0)
                    {
                        renderWorld?.ShowHealText(healTextPosition, healDelta);
                    }
                    break;
                case BattleCommandType.AddBuff:
                    buffs.AddBuff(command.source, command.target, command.id, command.durationMs, command.stack);
                    break;
                case BattleCommandType.SpawnProjectile:
                    projectiles.Spawn(command.id, command.source, command.position, command.direction);
                    break;
                case BattleCommandType.DespawnUnit:
                    DespawnUnit(command.target);
                    break;
            }
        }

        private float ResolveRenderAlpha(bool tickedLogic)
        {
            if (logicStepMs <= 0)
            {
                return 1f;
            }

            if (tickedLogic && accumulatedMs <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(accumulatedMs / logicStepMs);
        }

        private void SyncRenderPositions(float alpha)
        {
            for (int i = 0; i < UnitManager.AllocatedCount; i++)
            {
                if (!UnitManager.TryGetHandleByIndex(i, out BattleUnitHandle unit))
                {
                    continue;
                }

                renderWorld?.SetPosition(UnitManager.GetRenderHandle(unit), UnitManager.GetInterpolatedPosition(unit, alpha));
            }

            projectiles?.SyncRenderPositions(alpha);
        }

        private static int[] ResolveSkillIds(int[] defaultSkills, int[] overrideSkills)
        {
            if (overrideSkills != null && overrideSkills.Length > 0)
            {
                return (int[])overrideSkills.Clone();
            }

            return defaultSkills != null ? (int[])defaultSkills.Clone() : global::System.Array.Empty<int>();
        }

        private static BattleAttributeValue[] ResolveAttrs(BattleAttributeValue[] defaultAttrs, BattleAttributeValue[] overrideAttrs)
        {
            int defaultCount = defaultAttrs?.Length ?? 0;
            int overrideCount = overrideAttrs?.Length ?? 0;
            if (overrideCount == 0)
            {
                return defaultAttrs != null ? (BattleAttributeValue[])defaultAttrs.Clone() : global::System.Array.Empty<BattleAttributeValue>();
            }

            BattleAttributeValue[] attrs = new BattleAttributeValue[defaultCount + overrideCount];
            if (defaultCount > 0)
            {
                global::System.Array.Copy(defaultAttrs, attrs, defaultCount);
            }

            global::System.Array.Copy(overrideAttrs, 0, attrs, defaultCount, overrideCount);
            return attrs;
        }
    }
}
