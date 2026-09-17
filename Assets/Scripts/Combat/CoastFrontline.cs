using System;
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
    /// <summary>해안 전선이 카드 이름표에 띄우는 전투 자원. 라운드가 끝나면 유닛이 스스로 비운다.</summary>
    public static class CoastFrontlineResources
    {
        /// <summary>공허의 갑주가 살아 있으면 1. 이름만 띄운다.</summary>
        public const string Armor = "coast_void_armor";

        /// <summary>대호흡·촉수 폭풍을 준비 중이면 1. 이름만 띄운다.</summary>
        public const string Charge = "coast_charge";

        /// <summary>심연의 집정관의 남은 체력 구간.</summary>
        public const string Phase = "coast_phase";
    }

    /// <summary>해안 전선의 적 ID와 상태 ID. 코드 ID는 <c>CoastFrontlineCodeIds</c>가 든다.</summary>
    public static class CoastFrontlineIds
    {
        public const int AssaultCaptain = 1140;
        public const int SpearheadCaptain = 1141;
        public const int AbyssHunter = 1142;
        public const int AbyssHorror = 2071;
        public const int AbyssArchon = 3080;

        public const int ArmorStatus = 9800;
        public const int ChargeStatus = 9801;
        public const int ArchonStatus = 9802;
        public const int RecoilStatus = 9803;
    }

    /// <summary>
    /// 해안 전선의 공용 규칙. 갑주 장착·해제, 열 단위 대상, 준비 공격 예약을 모았다.
    /// 설계 원본은 <c>Theme_Japan_Coastal_Frontline.md</c>다.
    /// </summary>
    public static class CoastFrontline
    {
        public const float ArmorStrMultiplier = 1.4f;

        /// <summary>갑주 보호막의 기준량. 일반·엘리트는 최대 체력, 집정관은 한 구간 체력에 곱한다.</summary>
        public const float ArmorShieldRatio = 0.20f;

        // ── 갑주 ────────────────────────────────────────────────────

        public static VoidArmorEffect ArmorOf(Unit unit)
            => unit?.GetStatus(CoastFrontlineIds.ArmorStatus)?.Effects
                .Select(effect => effect.EffectObject).OfType<VoidArmorEffect>().FirstOrDefault();

        public static bool IsArmored(Unit unit) => ArmorOf(unit)?.IsArmed == true;

        /// <summary>
        /// 갑주를 두른다. 이미 상태가 있으면 같은 효과를 다시 장착한다(집정관의 구간 교체).
        /// <paramref name="shieldBaseHp"/>의 20%가 기준 보호막이며 받는 쪽 보너스가 얹힌다.
        /// </summary>
        public static void EquipArmor(Unit unit, int shieldBaseHp)
        {
            if (unit == null || !unit.isActive) return;

            VoidArmorEffect armor = ArmorOf(unit);
            if (armor == null)
            {
                armor = new VoidArmorEffect();
                unit.AddStatus(BuffStatus.Create(
                    CoastFrontlineIds.ArmorStatus, "coast_void_armor", "공허의 갑주", unit, unit, armor,
                    stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                    category: BaseEnums.StatusCategory.Neutral,
                    description: $"STR +{(ArmorStrMultiplier - 1f) * 100f:0}%와 보호막을 두릅니다. " +
                                 "빙결·기절·에어본 같은 행동불능에 걸리면 STR 강화와 갑주가 준 남은 보호막이 함께 벗겨집니다. " +
                                 "보호막만 깨서는 STR 강화가 남습니다."));
            }

            armor.Arm(Mathf.Max(1, Mathf.RoundToInt(shieldBaseHp * ArmorShieldRatio)));
        }

        // ── 대상 ────────────────────────────────────────────────────

        /// <summary>상대 진영 전열. 비었으면 남은 상대 전원.</summary>
        public static List<Unit> OpposingFrontOrAll(Unit caster)
            => OpposingColumnOrAll(caster, front: true);

        /// <summary>상대 진영 후열. 비었으면 남은 상대 전원.</summary>
        public static List<Unit> OpposingRearOrAll(Unit caster)
            => OpposingColumnOrAll(caster, front: false);

        private static List<Unit> OpposingColumnOrAll(Unit caster, bool front)
        {
            List<Unit> enemies = CombatTargets.AliveEnemies(caster);
            GridManager grid = GridManager.Instance;
            if (grid == null || caster == null) return enemies;

            int column = front ? grid.GetFrontColumn(!caster.IsEnemy) : grid.GetRearColumn(!caster.IsEnemy);
            List<Unit> row = enemies.Where(unit => unit.currentCell != null && unit.currentCell.xPos == column).ToList();
            return row.Count > 0 ? row : enemies;
        }

        // ── 준비 공격 ────────────────────────────────────────────────

        public static bool IsCharging(Unit unit) => unit != null && unit.HasStatus(CoastFrontlineIds.ChargeStatus);

        /// <summary>
        /// 준비를 건다. 다음 자기 일반행동이 대체행동으로 바뀐다.
        /// 준비 중 실제 행동불능이 되면 예약이 사라지고 자원은 돌아오지 않는다.
        /// </summary>
        public static void BeginCharge(Unit unit, string name, string description)
        {
            if (unit == null || !unit.isActive) return;
            unit.AddStatus(BuffStatus.Create(
                CoastFrontlineIds.ChargeStatus, "coast_charge", name, unit, unit, new ChargeEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                category: BaseEnums.StatusCategory.Neutral,
                description: description + " 준비 중 행동불능이 되면 취소됩니다."));
        }

        /// <summary>준비를 소모한다. 준비 중이 아니었다면 false.</summary>
        public static bool ConsumeCharge(Unit unit)
        {
            if (!IsCharging(unit)) return false;
            unit.RemoveStatusByKey("coast_charge");
            return true;
        }
    }

    /// <summary>
    /// 공허의 갑주. STR 강화와 보호막을 함께 두르고, 실제 행동불능 진입 한 번에 둘 다 벗는다.
    ///
    /// 보호막은 합산 수치라 출처별 장부가 없다. 그래서 <b>갑주 몫을 따로 적어 두고</b>,
    /// 비관통 피해로 보호막이 줄면 갑주 몫부터 깎인 것으로 센다. 해제할 때는 남은 몫만 지운다 —
    /// 다른 출처의 보호막까지 지우면 아군 치유사의 방어막 제거 효과 같은 것과 구분이 사라진다.
    /// </summary>
    public sealed class VoidArmorEffect : BaseEffect
    {
        private Action<EventContext> _controlHandler;
        private Action<EventContext> _beforeDamageHandler;
        private Action<EventContext> _afterDamageHandler;
        private int _ledger;
        private int _shieldBeforeHit;
        private int _damageDepth;

        public VoidArmorEffect() : base(0) { }

        public override bool CountsAsReagentBuff => false;

        public bool IsArmed { get; private set; }

        /// <summary>갑주가 두른 보호막 중 아직 남아 있는 몫.</summary>
        public int ArmorShieldRemaining => Target == null ? 0 : Mathf.Min(_ledger, Target.ShieldCurr);

        public override void OnApply()
        {
            if (Target == null) return;
            _controlHandler = OnControlStarts;
            _beforeDamageHandler = OnBeforeDamageTaken;
            _afterDamageHandler = OnAfterDamageTaken;
            Target.AddListener(BaseEnums.UnitEventType.OnControlStarts, _controlHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _beforeDamageHandler);
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _afterDamageHandler);
            Target.SetCombatResourceMaximum(CoastFrontlineResources.Armor, 1, resetCurrent: true);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnControlStarts, _controlHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnBeforeDamageTaken, _beforeDamageHandler);
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _afterDamageHandler);
        }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => IsArmed && unit == Target && stat == BaseEnums.PrimaryStat.STR ? CoastFrontline.ArmorStrMultiplier : 1f;

        /// <summary>
        /// 장착(또는 재장착). 남아 있던 옛 갑주 보호막은 먼저 걷어 낸다 — 관통으로 남겨 둔 보호막이
        /// 구간마다 누적되지 않게 하기 위해서다. STR 강화는 갱신일 뿐 겹치지 않는다.
        /// <b>이미 행동불능이면 곧바로 벗겨진 것으로 처리한다.</b> 긴 제어 중에 구간을 넘긴 쪽의 이득이다.
        /// </summary>
        public void Arm(int shieldAmount)
        {
            Unit owner = Target;
            if (owner == null || !owner.isActive) return;

            StripLedgerShield();
            IsArmed = true;
            owner.SetCombatResourceMaximum(CoastFrontlineResources.Armor, 1);
            owner.AddCombatResource(CoastFrontlineResources.Armor, 1 - owner.GetCombatResource(CoastFrontlineResources.Armor));

            int before = owner.ShieldCurr;
            owner.AddShield(shieldAmount, owner);
            _ledger = Mathf.Max(0, owner.ShieldCurr - before);
            owner.RefreshAttributes();

            if (owner.isControlled)
            {
                Debug.Log($"[공허의 갑주] {owner.UnitName}은(는) 이미 행동불능이라 새 갑주가 곧바로 벗겨집니다.");
                Break();
            }
        }

        /// <summary>갑주 해제. 제어 분쇄 회차는 건드리지 않는다.</summary>
        public void Break()
        {
            Unit owner = Target;
            if (!IsArmed || owner == null) return;

            IsArmed = false;
            int removed = StripLedgerShield();
            owner.AddCombatResource(CoastFrontlineResources.Armor, -owner.GetCombatResource(CoastFrontlineResources.Armor));
            owner.RefreshAttributes();
            owner.RefreshView();
            Debug.Log($"[공허의 갑주] {owner.UnitName}의 갑주가 벗겨졌습니다 — STR 강화 해제, 보호막 {removed} 제거");
        }

        private int StripLedgerShield()
        {
            int remove = ArmorShieldRemaining;
            _ledger = 0;
            if (remove > 0 && Target != null) Target.RemoveShield(remove);
            return remove;
        }

        private void OnControlStarts(EventContext context)
        {
            if (Target == null || !Target.isActive) return;
            Break();
        }

        // 피해 처리 안에서 강인도 전환 같은 피해가 다시 들어올 수 있다. 가장 바깥 한 번만 잰다.
        private void OnBeforeDamageTaken(EventContext context)
        {
            if (Target == null) return;
            if (_damageDepth++ == 0) _shieldBeforeHit = Target.ShieldCurr;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            if (Target == null) return;
            if (--_damageDepth > 0) return;
            _damageDepth = 0;

            int lost = _shieldBeforeHit - Target.ShieldCurr;
            if (lost > 0) _ledger = Mathf.Max(0, _ledger - lost);
            _ledger = Mathf.Min(_ledger, Target.ShieldCurr);
        }
    }

    /// <summary>준비 표식. 실제 행동불능 진입 시 예약을 지운다. 자원은 돌려주지 않는다.</summary>
    internal sealed class ChargeEffect : BaseEffect
    {
        private Action<EventContext> _controlHandler;

        public ChargeEffect() : base(0) { }

        public override bool CountsAsReagentBuff => false;

        public override void OnApply()
        {
            if (Target == null) return;
            _controlHandler = OnControlStarts;
            Target.AddListener(BaseEnums.UnitEventType.OnControlStarts, _controlHandler);
            Target.SetCombatResourceMaximum(CoastFrontlineResources.Charge, 1, resetCurrent: true);
            Target.AddCombatResource(CoastFrontlineResources.Charge, 1);
        }

        public override void OnRemove()
        {
            if (Target == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnControlStarts, _controlHandler);
            Target.AddCombatResource(CoastFrontlineResources.Charge, -Target.GetCombatResource(CoastFrontlineResources.Charge));
        }

        private void OnControlStarts(EventContext context)
        {
            if (Target == null) return;
            Debug.Log($"[해안] {Target.UnitName}의 준비 공격이 행동불능으로 취소되었습니다.");
            Target.RemoveStatusByKey("coast_charge");
        }
    }
}
