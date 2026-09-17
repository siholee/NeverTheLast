using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>해안 전선 공격 궁극기의 공용 타격. 치명타는 궁극기 단위로 한 번 굴리고, 피해가 끝난 뒤 원소를 붙인다.</summary>
    public abstract class CoastStrikeUltimate : SimpleUltimate
    {
        protected CoastStrikeUltimate(UltimateCodeContext context, string name) : base(context, name, 0.45f) { }

        protected void Strike(IEnumerable<Unit> targets, int power, BaseEnums.PrimaryStat stat, bool contact,
            bool special, BaseEnums.UnitElement element, int hits = 1)
        {
            List<Unit> list = targets.Where(unit => unit != null && unit.isActive).ToList();
            if (list.Count == 0) return;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(power, stat) * crit));
            int scope = list.Count == 1 ? DamageTag.SingleTarget : DamageTag.MultiTarget;

            for (int hit = 0; hit < hits; hit++)
            {
                foreach (Unit target in list.Where(unit => unit != null && unit.isActive))
                {
                    target.TakeDamage(new DamageContext(
                        Caster, damage, BaseEnums.CodeType.Ultimate,
                        new List<int>
                        {
                            scope, DamageTag.UltAttack,
                            special ? DamageTag.Special : DamageTag.Physical,
                            contact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
                        },
                        isCrit));
                }
            }

            foreach (Unit target in list.Where(unit => unit != null && unit.isActive))
            {
                target.GrantCombatElement(element, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 공허의 돌격대장 U — 조류 집게. 단일 STR 위력 130(조류의 압력 1812: 150), 물 부착.
    /// 쌍집게(1813)를 배우면 같은 대상에게 STR 위력 80을 두 번으로 바뀐다.
    /// </summary>
    public sealed class CoastTidalPincer : CoastStrikeUltimate
    {
        public CoastTidalPincer(UltimateCodeContext context) : base(context, "조류 집게") { }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            if (Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.TwinPincer))
            {
                Strike(new[] { target }, 80, BaseEnums.PrimaryStat.STR, true, false, BaseEnums.UnitElement.Hydro, hits: 2);
                return;
            }

            int power = Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.TidalPincerUp) ? 150 : 130;
            Strike(new[] { target }, power, BaseEnums.PrimaryStat.STR, true, false, BaseEnums.UnitElement.Hydro);
        }
    }

    /// <summary>
    /// 공허의 선봉대장 U — 암초 충돌. 상대 전열 전체에 STR 위력 90(밀려오는 암초 1822: 100), 바위 부착.
    /// 전열이 비면 남은 상대 전원. 되받는 등갑(1823)이면 다음 일반행동 하나를 강화한다.
    /// </summary>
    public sealed class CoastReefCollision : CoastStrikeUltimate
    {
        public CoastReefCollision(UltimateCodeContext context) : base(context, "암초 충돌") { }

        protected override void Resolve()
        {
            int power = Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.ReefSurge) ? 100 : 90;
            Strike(CoastFrontline.OpposingFrontOrAll(Caster), power, BaseEnums.PrimaryStat.STR, true, false,
                BaseEnums.UnitElement.Geo);

            if (!Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.RecoilShell)) return;
            Caster.AddStatus(BuffStatus.Create(
                CoastFrontlineIds.RecoilStatus, "coast_recoil_shell", "되받는 등갑", Caster, Caster, new MarkerBuffEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "다음 등갑 밀치기의 위력이 100이 됩니다."));
        }
    }

    /// <summary>심연의 사냥꾼 U — 혈조 돌진. 상대 후열 전체에 DEX 위력 60(붉은 조류 1833: 70), 특수·비접촉, 불 부착.</summary>
    public sealed class CoastBloodTide : CoastStrikeUltimate
    {
        public CoastBloodTide(UltimateCodeContext context) : base(context, "혈조 돌진") { }

        protected override void Resolve()
        {
            int power = Caster.HasLearnedPassiveCode(CoastFrontlineCodeIds.BloodTideUp) ? 70 : 60;
            Strike(CoastFrontline.OpposingRearOrAll(Caster), power, BaseEnums.PrimaryStat.DEX, false, true,
                BaseEnums.UnitElement.Pyro);
        }
    }

    /// <summary>
    /// 준비형 궁극기의 뼈대. 자원은 준비를 거는 순간 쓰이고 그때는 피해가 없다.
    /// 실제 공격은 다음 자기 일반행동이 대체한다(<c>CoastNormal.TrySubstitute</c>).
    /// </summary>
    public abstract class CoastChargeUltimate : SimpleUltimate
    {
        private readonly string _chargeName;
        private readonly string _chargeDescription;

        protected CoastChargeUltimate(UltimateCodeContext context, string name, string chargeName,
            string chargeDescription) : base(context, name, 0.3f)
        {
            _chargeName = chargeName;
            _chargeDescription = chargeDescription;
        }

        protected override void Resolve()
        {
            CoastFrontline.BeginCharge(Caster, _chargeName, _chargeDescription);
            Debug.Log($"[{CodeName}] {Caster.UnitName}이(가) {_chargeName}을(를) 준비합니다.");
        }

        public override bool HasValidTarget()
            => base.HasValidTarget() && !CoastFrontline.IsCharging(Caster) && Enemies().Count > 0;
    }

    /// <summary>심연의 공포 U — 촉수 폭풍 준비.</summary>
    public sealed class CoastTentacleStorm : CoastChargeUltimate
    {
        public CoastTentacleStorm(UltimateCodeContext context)
            : base(context, "촉수 폭풍", "촉수 폭풍 준비",
                "다음 일반행동이 상대 전원에게 INT 위력 110의 특수·접촉 공격과 바람 부착으로 바뀝니다.") { }
    }

    /// <summary>
    /// 심연의 집정관 U — 해연의 대호흡. 입장 후 일반행동 2회를 마치기 전에는 쓰지 않는다.
    /// 브레이크는 준비를 취소하거나 미루지 않는다. 취소하는 것은 실제 행동불능뿐이다.
    /// </summary>
    public sealed class CoastAbyssalBreath : CoastChargeUltimate
    {
        private const int ActionsBeforeFirstCast = 2;

        public CoastAbyssalBreath(UltimateCodeContext context)
            : base(context, "해연의 대호흡", "대호흡",
                "다음 일반행동이 상대 전원에게 고정 120 + STR 위력 125의 해일로 바뀝니다. " +
                "피해는 해일이 나가는 순간의 STR로 계산하므로 갑주를 벗겨 두면 약해집니다.") { }

        public override bool HasValidTarget()
            => base.HasValidTarget() &&
               (ArchonDominionEffect.Of(Caster)?.NormalActions ?? ActionsBeforeFirstCast) >= ActionsBeforeFirstCast;
    }
}
