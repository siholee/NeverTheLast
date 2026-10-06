using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 수리야 — 일반행동 「이름을 부르는 탄환」(Detail_08 §18.3). 현상수배된 적에게 공격력 80% 단일 #사격을 쏜 뒤
    /// 탄환을 2발까지 채운다(+2가 아니다). 불은 붙이지 않는다. 수배가 살아 있으면 표적을 바꾸지 않는다.
    /// 자기 일반행동은 특성 응답을 부르지 않는다.
    /// </summary>
    public sealed class SuryaNameBullet : BaseNormalCode
    {
        public const float AttackRatio = 0.80f;

        public override int AttackForm => DamageTag.Shot;

        public SuryaNameBullet(NormalCodeContext context) : base(context) => CodeName = "이름을 부르는 탄환";

        private SuryaBeforeSunset Passive => Caster?.ActivePassiveCodes.OfType<SuryaBeforeSunset>().FirstOrDefault();

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        protected override List<Unit> SelectTarget()
        {
            Unit wanted = Passive?.ResolveTarget();
            return wanted != null && wanted.isActive ? new List<Unit> { wanted } : base.SelectTarget();
        }

        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Combat.DirectAttack.HitOne("surya.normal", Caster, target, () => Hit(target, context),
                presentation: impact => Effects.Vfx.VedicTrioFx.Single("surya.normal", Caster, target, impact));
        }

        protected override void OnAttackResolved(Unit target, DamageContext context) => Passive?.Reload();
    }
}
