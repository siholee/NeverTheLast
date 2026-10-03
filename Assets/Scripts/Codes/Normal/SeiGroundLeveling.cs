using System.Collections;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;

namespace Codes.Normal
{
    /// <summary>
    /// 세이 — 일반행동 「땅고르기」.
    /// 전방으로 결정탄을 연발해 55 + 방어력의 50% 피해를 입히고, 맞은 대상에게 바위 원소를 부여한다.
    ///
    /// 세이는 방어력을 계수로 쓰는 드문 유닛이다. 경감은 다른 공격처럼 대상의 방어력이 맡는다.
    /// 카드가 날아가지 않는 원거리 공격이라 카드는 반동만 치고, 여섯 발 가운데 세 번째 주 착탄만 판정한다.
    /// </summary>
    public sealed class SeiGroundLeveling : BaseNormalCode
    {
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
            // 피해 뒤에 붙인다. 증폭 반응이 방금 그 피해를 되짚어 키울 수 있게.
            if (target != null && target.isActive)
                target.GrantCombatElement(BaseEnums.UnitElement.Geo, Unit.CommonElementAuraDuration, Caster);
        }
    }
}
