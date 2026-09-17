using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허의 등불나방 U — 모여드는 등불. 주변 3×3에서 가장 다친 동료 하나(등불의 군집 1712: 둘)를
    /// 최대 체력 2% 치유한다. 남겨진 빛(1713)을 배웠다면 실제로 치유한 대상에게 CON×0.8 보호막을 더한다.
    /// 흔들리는 등불의 지속피해 조건에 걸려 있으면 예약하지 않고 자원을 쥐고 기다린다.
    /// </summary>
    public sealed class SkyGatheringLanterns : SimpleUltimate
    {
        private const float HealRatio = 0.02f;
        private const float ShieldConRatio = 0.8f;

        public SkyGatheringLanterns(UltimateCodeContext context) : base(context, "모여드는 등불", 0.4f) { }

        protected override void Resolve()
        {
            int count = Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.LanternSwarm) ? 2 : 1;
            bool shield = Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.LingeringLight);

            foreach (Unit patient in SkyFrontline.OrderByWound(SkyFrontline.AlliesAround(Caster)).Take(count).ToList())
            {
                int before = patient.HpCurr;
                SkyFrontline.HealRatio(Caster, patient, HealRatio);
                if (shield && patient.HpCurr > before)
                    patient.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ShieldConRatio)), Caster);
            }
        }

        /// <summary>다친 동료가 곁에 없으면 자원을 쥐고 기다린다. 가득 찬 동료를 치유하느라 자원을 버리지 않는다.</summary>
        public override bool HasValidTarget()
            => base.HasValidTarget() &&
               LanternEffect.Of(Caster)?.IsSuppressed != true &&
               SkyFrontline.AlliesAround(Caster).Any(ally => ally.HpCurr < ally.HpMax);
    }

    /// <summary>
    /// 공허의 고치 U — 결정 봉합. 연결 대상에게 그 최대 체력 1%(응축 봉합 1722: 1.25%) 보호막.
    /// 치유·무적 복구·정화는 하지 않는다.
    /// </summary>
    public sealed class SkyCrystalSeal : SimpleUltimate
    {
        public SkyCrystalSeal(UltimateCodeContext context) : base(context, "결정 봉합", 0.4f) { }

        protected override void Resolve()
        {
            Unit link = CocoonCoreEffect.Of(Caster)?.Link;
            float ratio = Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.CondensedSeal) ? 0.0125f : 0.01f;
            SkyFrontline.ShieldRatio(Caster, link, ratio);
        }

        public override bool HasValidTarget() => base.HasValidTarget() && CocoonCoreEffect.Of(Caster)?.Link != null;
    }

    /// <summary>
    /// 중심 하나와 주변 3×3을 따로 치는 궁극기의 공용 뼈대. 중심은 두 번 맞지 않는다.
    /// 피해가 끝난 뒤 살아남은 피해 대상에게 원소를 붙이고, 파생 코드의 후처리를 부른다.
    /// </summary>
    public abstract class SkySplashUltimate : SimpleUltimate
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly BaseEnums.UnitElement _element;
        private readonly bool _contact;

        protected SkySplashUltimate(UltimateCodeContext context, string name, BaseEnums.PrimaryStat stat,
            BaseEnums.UnitElement element, bool contact) : base(context, name, 0.45f)
        {
            _stat = stat;
            _element = element;
            _contact = contact;
        }

        protected abstract int CenterPower { get; }
        protected abstract int SplashPower { get; }

        protected virtual void AfterResolve() { }

        protected override void Resolve()
        {
            Unit center = CombatTargets.PickByPriority(Enemies());
            if (center == null) return;

            List<Unit> splash = SkyFrontline.SplashAround(Caster, center);
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;

            var hit = new List<Unit>();
            Strike(center, CenterPower, crit, isCrit, DamageTag.SingleTarget);
            hit.Add(center);
            foreach (Unit target in splash)
            {
                Strike(target, SplashPower, crit, isCrit, DamageTag.MultiTarget);
                hit.Add(target);
            }

            foreach (Unit target in hit.Where(unit => unit != null && unit.isActive))
            {
                target.GrantCombatElement(_element, Unit.CommonElementAuraDuration, Caster);
            }

            AfterResolve();
        }

        private void Strike(Unit target, int power, float crit, bool isCrit, int scopeTag)
        {
            if (target == null || !target.isActive) return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(power, _stat) * crit));
            var tags = _contact
                ? new List<int> { scopeTag, DamageTag.UltAttack, DamageTag.Physical, DamageTag.ContactAttack }
                : new List<int> { scopeTag, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack };
            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate, tags, isCrit));
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>공허의 나비 U — 화염 비산. 중심과 주변 3×3에 INT 위력 55를 한 번씩, 불 부착.</summary>
    public sealed class SkyFlameScatter : SkySplashUltimate
    {
        public SkyFlameScatter(UltimateCodeContext context)
            : base(context, "화염 비산", BaseEnums.PrimaryStat.INT, BaseEnums.UnitElement.Pyro, contact: false) { }

        protected override int CenterPower => 55;
        protected override int SplashPower => 55;
    }

    /// <summary>
    /// 공허의 포식자 U — 파쇄 급강하. 중심 STR 150(깊은 급강하 1742: 170), 주변 70. 바람 부착.
    /// 피해가 모두 끝난 뒤 무적을 3회로 갱신한다. 시전이 끊기면 여기까지 오지 않으므로 갱신도 없다.
    /// </summary>
    public sealed class SkyShatterDive : SkySplashUltimate
    {
        public SkyShatterDive(UltimateCodeContext context)
            : base(context, "파쇄 급강하", BaseEnums.PrimaryStat.STR, BaseEnums.UnitElement.Anemo, contact: true) { }

        protected override int CenterPower => Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.DeepDive) ? 170 : 150;
        protected override int SplashPower => 70;
        protected override void AfterResolve() => SkyPredatorAftermath.Resolve(Caster);
    }

    /// <summary>종말의 포식자 U — 종말 급강하. 중심 STR 160(깊은 종말 1751: 180), 주변 80. 바람 부착.</summary>
    public sealed class SkyApocalypseDive : SkySplashUltimate
    {
        public SkyApocalypseDive(UltimateCodeContext context)
            : base(context, "종말 급강하", BaseEnums.PrimaryStat.STR, BaseEnums.UnitElement.Anemo, contact: true) { }

        protected override int CenterPower => Caster.HasLearnedPassiveCode(SkyFrontlineCodeIds.DeepApocalypse) ? 180 : 160;
        protected override int SplashPower => 80;
        protected override void AfterResolve() => SkyPredatorAftermath.Resolve(Caster);
    }

    /// <summary>
    /// 천공의 포식자 U — 공허 강하. 상대 전원에게 <c>고정 120 + STR 위력 90</c>, 바람 부착.
    /// 피해 뒤 살아 있는 소환체의 카운트다운을 하나씩 당긴다(1인 소환체는 제외).
    ///
    /// 입장 후 일반행동 2회를 마치기 전에는 예약하지 않는다. 입장 소환 둘을 볼 틈을 주기 위해서다.
    /// </summary>
    public sealed class SkyVoidDescent : SimpleUltimate
    {
        private const int FlatDamage = 120;
        private const int DescentPower = 90;
        private const int ActionsBeforeFirstCast = 2;

        public SkyVoidDescent(UltimateCodeContext context) : base(context, "공허 강하", 0.6f) { }

        protected override void Resolve()
        {
            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                (FlatDamage + Caster.SkillDamage(DescentPower, BaseEnums.PrimaryStat.STR)) * crit));

            List<Unit> targets = Enemies();
            foreach (Unit target in targets)
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack },
                    isCrit));
            }
            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive))
            {
                target.GrantCombatElement(BaseEnums.UnitElement.Anemo, Unit.CommonElementAuraDuration, Caster);
            }

            SkyFrontline.AccelerateCountdowns(Caster);
            SkyPredatorAftermath.Resolve(Caster);
            Debug.Log($"[공허 강하] {Caster.UnitName}이(가) 강하하며 소환체의 카운트다운을 당겼습니다.");
        }

        public override bool HasValidTarget()
            => base.HasValidTarget() && Enemies().Count > 0 &&
               (SkySpawningEffect.Of(Caster)?.NormalActions ?? ActionsBeforeFirstCast) >= ActionsBeforeFirstCast;
    }

    /// <summary>소환체에게 붙이는 빈 궁극기. 자원이 차도 예약되지 않는다.</summary>
    public sealed class SkyInertUltimate : UltimateCode
    {
        public SkyInertUltimate(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "없음";
        }

        public override bool IsAutoCast => false;
        public override bool HasValidTarget() => false;
    }
}
