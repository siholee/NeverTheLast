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
        public override bool HasValidTarget() => Passive != null && Enemies().Any(x => Passive.Marks(x) > 0);
        protected override IEnumerator Perform()
        {
            var marked = Enemies().Where(x => Passive.Marks(x) > 0).ToList();
            var tags = new List<int> { DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Shot, DamageTag.SingleTarget };
            var routing = AttackTargetRouter.Prepare(Caster, marked, BaseEnums.CodeType.Normal, tags, CodeName);
            yield return routing.ResolvePreAttack();
            if (!routing.CanContinue || !Targeting.Announce(Caster, routing.Targets)) yield break;
            foreach (var target in routing.Targets)
            {
                if (!DirectAttack.CasterCanContinue(Caster, true)) yield break;
                if (target == null || !target.isActive || Passive.Marks(target) < 1) continue;
                bool critical = RollCritical(target, BaseEnums.CodeType.Normal, out float multiplier);
                var context = new DamageContext(Caster, UnityEngine.Mathf.RoundToInt(
                    StatDamage(0, BaseEnums.StatType.Attack, SuryaNameBullet.FinaleRatio) * multiplier),
                    BaseEnums.CodeType.Normal, tags, critical) { DefenseStatMultiplier = SuryaNameBullet.DefenseMultiplier };
                yield return DirectAttack.HitOne("surya.finale", Caster, target, () =>
                {
                    target.TakeDamage(context);
                    if (!context.IsCancelled) { Passive.Consume(target); if (target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster); }
                }, stopOnControl: true, presentation: impact => Effects.Vfx.VedicTrioFx.Single("surya.finale", Caster, target, impact));
            }
        }
    }
}
