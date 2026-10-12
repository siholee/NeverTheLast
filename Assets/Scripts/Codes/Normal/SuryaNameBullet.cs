using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;
namespace Codes.Normal
{
    /// <summary>두 번의 일반 사격이 표식을 남기고, 세 번째 사격은 종언탄으로 바뀐다.</summary>
    public sealed class SuryaNameBullet : BaseNormalCode
    {
        public const float AttackRatio = 1.10f;
        public const float FinaleRatio = 3.60f;
        public const float DefenseMultiplier = .75f;
        private bool _finale;
        private SuryaExecution Passive => Caster.ActivePassiveCodes.OfType<SuryaExecution>().FirstOrDefault();
        public override int AttackForm => DamageTag.Shot;
        public SuryaNameBullet(NormalCodeContext context) : base(context) => CodeName = "해가 지는 사격";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack,
            Passive?.Marks(target) >= 2 ? FinaleRatio : AttackRatio);
        protected override IEnumerator Deliver(Unit target)
        {
            _finale = Passive?.Marks(target) >= 2;
            DamageContext context = BuildContext(target);
            if (_finale) context.DefenseStatMultiplier = DefenseMultiplier;
            string key = _finale ? "surya.finale" : "surya.normal";
            yield return Combat.DirectAttack.HitOne(key, Caster, target, () => Hit(target, context, playSfx: !_finale),
                presentation: impact => Effects.Vfx.VedicTrioFx.Single(key, Caster, target, impact));
        }
        // 지휘 대상 추정도 실제 종언탄처럼 방어력을 75%만 인정한다.
        protected override void PrepareEstimateProbe(Unit target, DamageContext probe)
        {
            if (Passive?.Marks(target) >= 2) probe.DefenseStatMultiplier = DefenseMultiplier;
        }
        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (context.IsCancelled || target == null || !target.isActive) return;
            if (_finale) { Passive?.Consume(target); target.GrantCombatElement(BaseEnums.UnitElement.Pyro, source: Caster); }
            else Passive?.Mark(target, (Passive?.Marks(target) ?? 0) + 1);
        }
    }
}
