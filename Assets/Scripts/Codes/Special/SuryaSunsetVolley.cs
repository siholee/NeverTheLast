using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Combat;

namespace Codes.Special
{
    public sealed class SuryaSunsetVolley : BaseSpecialCode
    {
        public override int AttackForm => DamageTag.Shot;
        public override int OpenOrder => 35;
        public SuryaSunsetVolley(SpecialCodeContext context) : base(context) => CodeName = "석양의 일제사격";
        private SuryaExecution Passive => Caster?.ActivePassiveCodes.OfType<SuryaExecution>().FirstOrDefault();
        public override bool HasValidTarget() => Passive != null && Enemies().Count > 0;
        protected override IEnumerator Perform()
        {
            var targets = Enemies();
            var tags = new List<int> { DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Shot, DamageTag.SingleTarget };
            var routing = AttackTargetRouter.Prepare(Caster, targets, BaseEnums.CodeType.Normal, tags, CodeName);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            foreach (var target in routing.Targets)
            {
                if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
                if (target == null || !target.isActive) continue;
                // 연계 사격은 일반행동의 준비를 빼앗지 않는다. 미표식 적은 조준하고, 이미 표시된 적은 종언탄을 받는다.
                bool finale = Passive.Marks(target) > 0;
                bool critical = RollCritical(target, BaseEnums.CodeType.Normal, out float multiplier);
                var context = new DamageContext(Caster, UnityEngine.Mathf.RoundToInt(
                    StatDamage(0, BaseEnums.StatType.Attack, (finale ? SuryaNameBullet.FinaleRatio : SuryaNameBullet.AttackRatio)) * multiplier),
                    BaseEnums.CodeType.Normal, tags, critical) { DefenseStatMultiplier = finale ? SuryaNameBullet.DefenseMultiplier : 1f, AppliesElement = finale };
                string key = finale ? "surya.finale" : "surya.normal";
                yield return DirectAttack.HitOne(key, Caster, target, () =>
                {
                    target.TakeDamage(context);
                    if (!context.IsCancelled && target.isActive)
                    {
                        if (finale) target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster);
                        else Passive.Mark(target, 1);
                    }
                }, stopOnControl: true, presentation: impact => Effects.Vfx.VedicTrioFx.Single(key, Caster, target, impact));
            }
        }
    }
}
