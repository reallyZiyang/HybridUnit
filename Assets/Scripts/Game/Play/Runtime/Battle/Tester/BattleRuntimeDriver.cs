using System;
using Game.Data.Configs;
using Game.Data.Configs.Attr;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.System;
using UnityEngine;

namespace Game.Play.Battle.Tester
{
    public sealed class BattleRuntimeDriver : MonoBehaviour
    {
        private BattleTesterRunResult result;
        private BattleTesterScenario scenario;
        private float elapsedSeconds;
        private bool paused;

        public bool IsRunning => result.battle != null && result.battle.IsInitialized;
        public bool IsPaused => paused;
        public float ElapsedSeconds => elapsedSeconds;

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            if (paused)
            {
                result.battle.OnUpdate(0f);
                return;
            }

            float deltaTime = Time.deltaTime;
            result.battle.OnUpdate(deltaTime);
            elapsedSeconds += deltaTime;
        }

        private void OnDisable()
        {
            StopBattle();
        }

        private void OnDestroy()
        {
            StopBattle();
        }

        public bool StartBattle(Tables tables, BattleTesterScenario targetScenario, IBattleRenderWorld renderWorld)
        {
            return StartBattle(tables, targetScenario, renderWorld, null);
        }

        public bool StartBattle(Tables tables, BattleTesterScenario targetScenario, IBattleRenderWorld renderWorld, BattleSkillEnhancementContext skillEnhancementContext)
        {
            StopBattle();
            if (tables == null || targetScenario == null)
            {
                return false;
            }

            scenario = targetScenario;
            result = BattleTesterScenarioRunner.Start(tables, targetScenario, renderWorld, skillEnhancementContext);
            if (result.battle == null || !result.battle.IsInitialized)
            {
                result = default;
                scenario = null;
                return false;
            }

            elapsedSeconds = 0f;
            paused = !targetScenario.autoStart;
            result.battle.SetPaused(paused);
            return true;
        }

        public void Pause()
        {
            if (!IsRunning)
            {
                return;
            }

            paused = true;
            result.battle.SetPaused(true);
        }

        public void Resume()
        {
            if (!IsRunning)
            {
                return;
            }

            paused = false;
            result.battle.SetPaused(false);
        }

        public void StepTick()
        {
            if (!IsRunning)
            {
                return;
            }

            float step = Mathf.Max(1, scenario != null ? scenario.logicStepMs : 33) / 1000f;
            bool restorePaused = paused;
            result.battle.SetPaused(false);
            result.battle.OnUpdate(step);
            if (restorePaused)
            {
                result.battle.SetPaused(true);
            }

            elapsedSeconds += step;
        }

        public bool CastSkill(int unitIndex, int skillId)
        {
            if (!IsRunning || unitIndex < 0 || unitIndex >= result.units.Length)
            {
                return false;
            }

            return result.battle.CastSkill(result.units[unitIndex], skillId);
        }

        public void StopBattle()
        {
            result.battle?.DisposeBattle();
            result = default;
            scenario = null;
            elapsedSeconds = 0f;
            paused = false;
        }

        public BattleRuntimeDriverSnapshot GetRuntimeSnapshot()
        {
            if (!IsRunning || result.battle.UnitManager == null)
            {
                return new BattleRuntimeDriverSnapshot(false, paused, elapsedSeconds, Array.Empty<BattleRuntimeUnitSnapshot>());
            }

            BattleRuntimeUnitSnapshot[] units = new BattleRuntimeUnitSnapshot[result.units.Length];
            BattleUnitManager unitManager = result.battle.UnitManager;
            for (int i = 0; i < result.units.Length; i++)
            {
                BattleUnitHandle handle = result.units[i];
                BattleTesterUnitEntry source = i < result.sources.Length ? result.sources[i] : null;
                bool valid = unitManager.IsValid(handle);
                units[i] = new BattleRuntimeUnitSnapshot
                {
                    index = i,
                    label = source?.label,
                    handle = $"{handle.index}:{handle.generation}",
                    valid = valid,
                    unitCfgId = valid ? unitManager.GetUnitCfgId(handle) : source?.unitCfgId ?? 0,
                    camp = valid ? unitManager.GetCamp(handle) : source?.camp ?? 0,
                    position = valid ? unitManager.GetPosition(handle) : default,
                    hp = valid ? unitManager.GetHp(handle) : 0,
                    hpMax = valid ? unitManager.GetAttr(handle, AttributeType.HpMax) : 0,
                    atk = valid ? unitManager.GetAttr(handle, AttributeType.Atk) : 0,
                    state = valid ? unitManager.GetState(handle) : 0,
                    elapsedSeconds = elapsedSeconds
                };
            }

            return new BattleRuntimeDriverSnapshot(true, paused, elapsedSeconds, units);
        }
    }

    public readonly struct BattleRuntimeDriverSnapshot
    {
        public readonly bool isRunning;
        public readonly bool isPaused;
        public readonly float elapsedSeconds;
        public readonly BattleRuntimeUnitSnapshot[] units;

        public BattleRuntimeDriverSnapshot(bool isRunning, bool isPaused, float elapsedSeconds, BattleRuntimeUnitSnapshot[] units)
        {
            this.isRunning = isRunning;
            this.isPaused = isPaused;
            this.elapsedSeconds = elapsedSeconds;
            this.units = units ?? Array.Empty<BattleRuntimeUnitSnapshot>();
        }
    }

    [Serializable]
    public struct BattleRuntimeUnitSnapshot
    {
        public int index;
        public string label;
        public string handle;
        public bool valid;
        public int unitCfgId;
        public int camp;
        public Vector2 position;
        public int hp;
        public long hpMax;
        public long atk;
        public int state;
        public float elapsedSeconds;
    }
}
