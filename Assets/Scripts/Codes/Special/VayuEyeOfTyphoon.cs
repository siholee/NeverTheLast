using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Entities;

namespace Codes.Special
{
    /// <summary>
    /// 바유 — 특수행동 「태풍의 눈」(Detail_08 §14.5). 로카팔라 묶음의 처음(-100)에 적 전체 공격력 45% 피해를 준 뒤,
    /// 생존한 적 전원에게 <b>일곱 원소 모두</b>(불 · 물 · 풀 · 번개 · 얼음 · 바위 · 바람)의 취약 +25%(대상 2턴)를 건다.
    ///
    /// 묶음 전용 제한 · 참가자 행동 시작 훅 · 선택 확산 · 바람 부착 · 강제 확산은 없다(사용자 최신 단순화, 2026-10-05).
    /// 취약은 묶음이 끝나도 걷지 않고 자기 지속을 다 쓴다. 명중 · 저항은 공통 규칙을 따른다.
    /// </summary>
    public sealed class VayuEyeOfTyphoon : BaseSpecialCode
    {
        public const float AttackRatio = 0.45f;

        public override int AttackForm => DamageTag.Spell;
        public override int OpenOrder => -100;

        public VayuEyeOfTyphoon(SpecialCodeContext context) : base(context) => CodeName = "태풍의 눈";

        protected override IEnumerator Perform()
        {
            yield return HitAllEnemies(_ => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>
                {
                    DamageTag.AllTarget, DamageTag.SpecialAttack, DamageTag.NonContactAttack, DamageTag.Spell,
                },
                presentation: (targets, impact) => Effects.Vfx.LokapalaFx.Area("vayu.typhoon_eye", Caster, targets, impact),
                afterAll: ApplyGale);
        }

        private void ApplyGale()
        {
            foreach (Unit enemy in Enemies().Where(unit => unit != null && unit.isActive))
            VayuVulnerability.ApplyAll(Caster, enemy);
        }
    }
}
