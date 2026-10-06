using System.Collections;
using BaseClasses;
using Codes.Base;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 아그니 — 일반행동 「불씨를 다지는 주먹」(Detail_08 §17.3). 우선도 단일 적에게 공격력 75% 접촉 타격.
    /// 불은 붙이지 않는다 — 부착은 특성 추공의 몫이라 일반행동이 증발 · 융해 재료를 낭비하지 않게 한다.
    /// 판돈(초월기)의 지속은 이 일반행동이 실제로 끝날 때마다 하나씩 줄어든다.
    /// </summary>
    public sealed class AgniEmberFist : BaseNormalCode
    {
        public const float AttackRatio = 0.75f;

        public override int AttackForm => DamageTag.Strike;
        protected override bool IsContact => true;

        public AgniEmberFist(NormalCodeContext context) : base(context) => CodeName = "불씨를 다지는 주먹";

        protected override int BaseDamage(Unit target) => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio);

        // 새 직접 공격은 카드 돌진 없이 제자리 시전 + 대상 피격으로 짠다. 짧은 전진 연출은 96_vfx 정의가 맡는다.
        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return Combat.DirectAttack.HitOne("agni.normal", Caster, target, () => Hit(target, context),
                presentation: impact => Effects.Vfx.VedicTrioFx.Single("agni.normal", Caster, target, impact));
        }
    }
}
