using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>천공 전선이 카드에 띄우는 전투 자원. 라운드가 끝나면 유닛이 스스로 비운다.</summary>
    public static class SkyFrontlineResources
    {
        /// <summary>남은 무적 횟수. 유효 타격 하나가 하나를 지운다.</summary>
        public const string Ward = "sky_crystal_ward";

        /// <summary>소환체가 발동하기까지 남은 자기 턴.</summary>
        public const string Countdown = "sky_countdown";

        /// <summary>천공의 포식자가 허기 결정을 삼킨 횟수.</summary>
        public const string Satiety = "sky_satiety";
    }

    /// <summary>천공 전선의 적 ID와 상태 ID. 코드 ID는 <c>SkyFrontlineCodeIds</c>가 든다.</summary>
    public static class SkyFrontlineIds
    {
        public const int LanternMoth = 1130;
        public const int Cocoon = 1131;
        public const int ApocalypseCocoon = 1132;
        public const int Butterfly = 1133;
        public const int VoidPredator = 1134;
        public const int HungerCrystal = 1135;
        public const int RuptureScale = 1136;
        public const int ApocalypsePredator = 2060;
        public const int SkyPredator = 3070;

        public const int WardStatus = 9700;
        public const int CushionStatus = 9701;
        public const int CrystalLinkStatus = 9702;
        public const int SatietyStatus = 9703;
        public const int WoundTrackerStatus = 9704;
        public const int CocoonLinkStatus = 9705;
        public const int ButterflyStatus = 9706;
        public const int MothStatus = 9707;
        public const int SkyPredatorStatus = 9708;
        public const int CrystalCoreStatus = 9709;
        public const int PredatorHideStatus = 9710;
        public const int FragileBodyStatus = 9711;
    }

    /// <summary>
    /// 천공 전선의 공용 규칙. 무적 횟수·주변 3×3·소환체 배치처럼 여러 코드가 같은 모양으로
    /// 묻는 것을 모았다. <c>AswanCombat</c>과 같은 역할이다.
    /// </summary>
    public static class SkyFrontline
    {
        /// <summary>소환체 종류별 동시 상한과 합계 상한. 전열 네 칸 안에서 본체를 가리되 전부 막지는 않는다.</summary>
        public const int SummonCapPerKind = 2;
        public const int SummonCapTotal = 3;

        /// <summary>소환체의 체력은 소환 시점 본체 최대 체력의 이 비율로 고정된다.</summary>
        public const float SummonHpRatio = 0.03f;

        public const int CountdownTurns = 3;
        public const int HungerCrystalWard = 10;
        public const int RuptureScaleWard = 8;

        // ── 무적 ────────────────────────────────────────────────────

        public static int WardCharges(Unit unit) => unit?.GetCombatResource(SkyFrontlineResources.Ward) ?? 0;

        /// <summary>
        /// 무적을 <paramref name="charges"/>로 <b>갱신</b>한다. 합산하지 않는다 —
        /// 남은 횟수가 이미 그 이상이면 아무것도 바뀌지 않는다.
        /// </summary>
        public static void RefreshWard(Unit unit, int charges)
        {
            if (unit == null || !unit.isActive || charges <= 0) return;

            EnsureWardStatus(unit);
            int maximum = Mathf.Max(unit.GetCombatResourceMaximum(SkyFrontlineResources.Ward), charges);
            unit.SetCombatResourceMaximum(SkyFrontlineResources.Ward, maximum);
            int current = WardCharges(unit);
            if (current < charges) unit.AddCombatResource(SkyFrontlineResources.Ward, charges - current);
        }

        /// <summary>무적에 횟수를 더한다. 소환 직후 본체의 해금(단단한 결정)이 쓴다.</summary>
        public static void AddWard(Unit unit, int extra)
        {
            if (unit == null || extra <= 0) return;
            RefreshWard(unit, WardCharges(unit) + extra);
        }

        private static void EnsureWardStatus(Unit unit)
        {
            if (unit.HasStatus(SkyFrontlineIds.WardStatus)) return;
            unit.AddStatus(BuffStatus.Create(
                SkyFrontlineIds.WardStatus, "sky_crystal_ward", "결정 무적", unit, unit,
                new CrystalWardEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                category: BaseEnums.StatusCategory.Neutral,
                isBeneficial: false,
                description: "남은 횟수만큼 받는 피해를 한 번씩 통째로 무효화합니다. 지속피해 틱도 한 번으로 셉니다. " +
                             "상태 부여는 타격이 아니라 횟수를 쓰지 않습니다."));
        }

        // ── 대상 질의 ────────────────────────────────────────────────

        /// <summary>같은 진영에서 <paramref name="center"/> 주변 3×3에 선 다른 유닛. 칸 없는 소환수는 빠진다.</summary>
        public static List<Unit> AlliesAround(Unit center)
        {
            if (center?.currentCell == null) return new List<Unit>();
            int x = center.currentCell.xPos;
            int y = center.currentCell.yPos;
            return CombatTargets.AliveAlliesIncludingSelf(center)
                .Where(unit => unit != center && unit.currentCell != null &&
                               Mathf.Abs(unit.currentCell.xPos - x) <= 1 &&
                               Mathf.Abs(unit.currentCell.yPos - y) <= 1)
                .ToList();
        }

        /// <summary><paramref name="center"/>와 같은 진영에서 주변 3×3에 선 공격 가능한 유닛(중심 제외).</summary>
        public static List<Unit> SplashAround(Unit caster, Unit center)
        {
            if (center?.currentCell == null) return new List<Unit>();
            int x = center.currentCell.xPos;
            int y = center.currentCell.yPos;
            return CombatTargets.AliveEnemies(caster)
                .Where(unit => unit != center && unit.currentCell != null &&
                               Mathf.Abs(unit.currentCell.xPos - x) <= 1 &&
                               Mathf.Abs(unit.currentCell.yPos - y) <= 1)
                .ToList();
        }

        /// <summary>체력 비율이 가장 낮은 순. 동률은 현재 체력, 다시 동률이면 칸 순서.</summary>
        public static IEnumerable<Unit> OrderByWound(IEnumerable<Unit> units)
            => units
                .OrderBy(unit => unit.HpMax <= 0 ? 1f : (float)unit.HpCurr / unit.HpMax)
                .ThenBy(unit => unit.HpCurr)
                .ThenBy(unit => unit.currentCell != null ? Mathf.Abs(unit.currentCell.xPos) : 9)
                .ThenBy(unit => unit.currentCell != null ? unit.currentCell.yPos : 9);

        /// <summary>최대 체력 비율 치유. 기준량이라 받는 쪽의 CON 보너스가 얹힌다.</summary>
        public static void HealRatio(Unit source, Unit target, float ratio)
        {
            if (target == null || !target.isActive || ratio <= 0f) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(target.HpMax * ratio));
            target.ModifyHp(target.HpCurr + amount, source);
        }

        /// <summary>최대 체력 비율 보호막. 기준량이다.</summary>
        public static void ShieldRatio(Unit source, Unit target, float ratio)
        {
            if (target == null || !target.isActive || ratio <= 0f) return;
            target.AddShield(Mathf.Max(1, Mathf.RoundToInt(target.HpMax * ratio)), source);
        }

        // ── 소환체 ──────────────────────────────────────────────────

        /// <summary>이 소환체를 떨군 본체. 소환체가 아니면 null이다.</summary>
        public static Unit SummonerOf(Unit crystal)
            => crystal?.GetStatus(SkyFrontlineIds.CrystalLinkStatus)?.Effects
                .Select(effect => effect.EffectObject).OfType<CrystalLinkEffect>()
                .FirstOrDefault()?.Summoner;

        public static bool IsCrystalSummon(Unit unit)
            => unit != null && (unit.ID == SkyFrontlineIds.HungerCrystal || unit.ID == SkyFrontlineIds.RuptureScale);

        public static List<Unit> ActiveCrystals(Unit summoner)
            => CombatTargets.AliveAlliesIncludingSelf(summoner).Where(IsCrystalSummon).ToList();

        /// <summary>
        /// 본체 진영의 전열 빈 칸에 소환체 하나를 세운다. 상한에 걸리거나 칸이 없으면 null.
        ///
        /// 라운드 도중에 들어오므로 OnRoundStart를 직접 보내 패시브를 연다(<c>AswanCombat.SummonWraiths</c>와 같다).
        /// 체력·속도·추가 무적은 본체에 묶여 있어 소환 직후 여기서 덮어쓴다.
        /// </summary>
        public static Unit SpawnCrystal(Unit summoner, int crystalId, int extraWard)
        {
            GridManager grid = GridManager.Instance;
            if (summoner == null || !summoner.isActive || grid == null) return null;

            List<Unit> crystals = ActiveCrystals(summoner);
            if (crystals.Count >= SummonCapTotal) return null;
            if (crystals.Count(unit => unit.ID == crystalId) >= SummonCapPerKind) return null;

            bool isEnemy = summoner.IsEnemy;
            int xPos = grid.GetFrontColumn(isEnemy);
            for (int yPos = grid.yMin; yPos <= grid.yMax; yPos++)
            {
                if (!grid.IsCellAvailable(xPos, yPos)) continue;

                Unit spawned = grid.SpawnUnit(xPos, yPos, isEnemy, crystalId);
                if (spawned == null) continue;

                spawned.AddStatus(BuffStatus.Create(
                    SkyFrontlineIds.CrystalLinkStatus, "sky_crystal_link", "떨어진 결정", summoner, spawned,
                    new CrystalLinkEffect(summoner, Mathf.Max(1, Mathf.RoundToInt(summoner.HpMax * SummonHpRatio))),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    category: BaseEnums.StatusCategory.Neutral,
                    description: $"최대 체력이 소환 시점 {summoner.UnitName}의 {SummonHpRatio * 100f:0.#}%로 고정되고, " +
                                 "행동 속도는 본체를 따라갑니다."));
                // 상태가 붙으며 최대 체력이 바뀌어도 체력 비율은 보존되므로 가득 찬 채로 선다.
                spawned.Invoke(BaseEnums.UnitEventType.OnRoundStart, new EventContext(spawned));
                AddWard(spawned, extraWard);
                Debug.Log($"[천공] {summoner.UnitName}이(가) {spawned.UnitName}을(를) ({xPos},{yPos})에 떨궜습니다.");
                return spawned;
            }

            return null;
        }

        /// <summary>
        /// 살아 있는 소환체 전부의 카운트다운을 하나씩 당긴다. <b>1인 소환체는 건드리지 않는다</b> —
        /// 본체의 궁극기가 곧바로 흡수·파열을 일으키면 대응할 턴이 사라진다.
        /// </summary>
        public static void AccelerateCountdowns(Unit summoner)
        {
            foreach (Unit crystal in ActiveCrystals(summoner))
            {
                if (crystal.GetCombatResource(SkyFrontlineResources.Countdown) > 1)
                    crystal.AddCombatResource(SkyFrontlineResources.Countdown, -1);
            }
        }

        /// <summary>본체가 쓰러지면 남은 소환체는 발동 없이 떠난다. 처치가 아니다.</summary>
        public static void WithdrawCrystals(Unit summoner)
        {
            foreach (Unit crystal in ActiveCrystals(summoner))
            {
                crystal.Withdraw();
            }
        }

        /// <summary>한 프레임 뒤에 실행한다. 라운드 시작 순회 도중에 적 목록을 늘리지 않기 위해서다.</summary>
        public static void RunNextFrame(Unit owner, System.Action action)
        {
            if (owner == null || action == null) return;
            owner.StartCoroutine(NextFrame(action));
        }

        private static IEnumerator NextFrame(System.Action action)
        {
            yield return null;
            action();
        }
    }

    /// <summary>횟수제 무적. 남은 횟수는 전투 자원에 있어 카드가 그대로 읽는다.</summary>
    internal sealed class CrystalWardEffect : BaseEffect
    {
        public override bool CountsAsReagentBuff => false;
        public CrystalWardEffect() : base(0) { }

        public override bool TryNullifyHit(Unit unit, DamageContext context)
        {
            if (unit == null || unit != Target || SkyFrontline.WardCharges(unit) <= 0) return false;
            unit.TryConsumeCombatResource(SkyFrontlineResources.Ward, 1);

            Debug.Log($"[결정 무적] {unit.UnitName}이(가) 타격을 무효화했습니다. 남은 횟수 {SkyFrontline.WardCharges(unit)}");
            return true;
        }
    }

    /// <summary>
    /// 소환체를 본체에 묶는다. 최대 체력은 소환 시점 값으로 굳히고, 행동 속도는 본체를 계속 따라간다.
    /// 레벨별 스탯 표를 따로 두지 않고 본체에 묶어, 무적이 끝난 몸이 모든 레벨에서 한두 타에 쓰러지게 한다.
    /// </summary>
    internal sealed class CrystalLinkEffect : BaseEffect
    {
        private readonly Unit _summoner;
        private readonly int _fixedHp;

        public CrystalLinkEffect(Unit summoner, int fixedHp) : base(0)
        {
            _summoner = summoner;
            _fixedHp = fixedHp;
        }

        public override bool CountsAsReagentBuff => false;

        public Unit Summoner => _summoner;

        public override float MaxHpMultiplierModifier(Unit unit)
        {
            if (unit != Target) return 1f;
            int derived = unit.GetDerivedHp();
            return derived <= 0 ? 1f : (float)_fixedHp / derived;
        }

        public override float ActionSpeedModifier(Unit unit, float calculatedSpeed)
            => unit == Target && _summoner != null && _summoner.isActive && _summoner.ActionSpeedCurr > 0f
                ? _summoner.ActionSpeedCurr
                : calculatedSpeed;
    }
}
