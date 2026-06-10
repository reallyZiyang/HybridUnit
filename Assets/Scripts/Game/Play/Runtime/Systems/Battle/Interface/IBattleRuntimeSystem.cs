using Game.Data.Configs;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UniKit.Framework.Base;
using UnityEngine;

namespace Game.Play.Systems.Battle.Interface
{
    public interface IBattleRuntimeSystem : ISystem, IUpdateSystem
    {
        BattleRuntimeData RuntimeData { get; }
        BattleUnitManager UnitManager { get; }
        bool IsInitialized { get; }
        bool IsPaused { get; }

        void InitializeBattle(
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
            int skillSlotsPerUnit = 0);

        BattleUnitHandle SpawnUnit(int unitCfgId, Vector2 position, int campOverride = 0);
        BattleUnitHandle SpawnUnit(int unitCfgId, Vector2 position, BattleUnitSpawnOverrides overrides);
        bool DespawnUnit(BattleUnitHandle unit);
        bool CastSkill(BattleUnitHandle caster, int skillId);
        void SetPaused(bool paused);
        void DisposeBattle();
    }
}
