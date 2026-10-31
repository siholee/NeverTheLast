using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 세이 — 일반행동 「땅고르기」.
    /// 전방으로 결정탄을 연발해 55 + 방어력의 50% 피해와 방어력 감소를 준다.
    ///
    /// 세이는 방어력을 계수로 쓰는 드문 유닛이다. 경감은 다른 공격처럼 대상의 방어력이 맡는다.
    /// 카드가 날아가지 않는 원거리 공격이라 카드는 반동만 치고, 여섯 발 가운데 세 번째 주 착탄만 판정한다.
    /// </summary>
    public sealed class SeiGroundLeveling : BaseNormalCode
    {
        public override int AttackForm => DamageTag.Spell;

        private const float FlatDamage = 55f;
        private const float DefenseRatio = 0.5f;

        public SeiGroundLeveling(NormalCodeContext context) : base(context)
        {
            CodeName = "땅고르기";
            CastingDelay = 0.12f;
        }

        protected override int BaseDamage(Unit target)
            => StatDamage(FlatDamage, BaseEnums.StatType.Defense, DefenseRatio);

        /// <summary>
        /// 카드 앞 결정 문장에서 여섯 발을 연속 발사한다. 나머지 다섯 발은 리듬만 만들며,
        /// 세 번째 탄의 착탄 시트 정점 프레임에 기존 피해 콜백을 정확히 한 번 연결한다.
        /// </summary>
        protected override IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            yield return GeoFx.RadiantVolley(Caster, target, () => Hit(target, context),
                context.IsCrit ? 1.2f : 1f);
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (target != null && target.isActive)
            {
                var status = BuffStatus.Create(7003, "sei_ground_shred", "땅고르기", Caster, target,
                    new StatModifierEffect(BaseEnums.StatType.Defense, -.25f, percent: true), duration: 2,
                    category: BaseEnums.StatusCategory.Negative, description: "방어력 -25%. 자신의 일반행동 2회까지.");
                status.NormalActionDuration = true;
                target.AddStatus(status);
            }
        }
    }
}
