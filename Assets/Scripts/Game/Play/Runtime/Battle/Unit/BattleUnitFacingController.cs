using Game.Play.Battle.Rendering;
using UnityEngine;

namespace Game.Play.Battle.Unit
{
    public sealed class BattleUnitFacingController
    {
        private const float FaceEpsilon = 0.001f;

        private readonly BattleUnitManager units;
        private readonly IBattleRenderWorld renderWorld;
        private readonly bool[] flipX;

        public BattleUnitFacingController(BattleUnitManager units, IBattleRenderWorld renderWorld, int unitCapacity)
        {
            this.units = units;
            this.renderWorld = renderWorld;
            flipX = new bool[Mathf.Max(1, unitCapacity)];
        }

        public void ResetUnit(BattleUnitHandle unit)
        {
            if (units == null || !units.IsValid(unit) || unit.index < 0 || unit.index >= flipX.Length)
            {
                return;
            }

            flipX[unit.index] = false;
            renderWorld?.SetUnitFlipX(units.GetRenderHandle(unit), false);
        }

        public void SetUnitFacing(BattleUnitHandle unit, bool faceLeft)
        {
            if (units == null || !units.IsValid(unit) || unit.index < 0 || unit.index >= flipX.Length)
            {
                return;
            }

            if (flipX[unit.index] == faceLeft)
            {
                return;
            }

            flipX[unit.index] = faceLeft;
            renderWorld?.SetUnitFlipX(units.GetRenderHandle(unit), faceLeft);
        }

        public void FaceDirection(BattleUnitHandle unit, Vector2 direction)
        {
            if (direction.x < -FaceEpsilon)
            {
                SetUnitFacing(unit, true);
            }
            else if (direction.x > FaceEpsilon)
            {
                SetUnitFacing(unit, false);
            }
        }

        public void FaceTarget(BattleUnitHandle unit, BattleUnitHandle target)
        {
            if (units == null || !units.IsAlive(unit) || !units.IsAlive(target) || unit.SameAs(target))
            {
                return;
            }

            FaceDirection(unit, units.GetPosition(target) - units.GetPosition(unit));
        }
    }
}
