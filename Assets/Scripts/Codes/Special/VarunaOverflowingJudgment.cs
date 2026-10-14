using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Codes.Passive;

namespace Codes.Special
{
    /// <summary>
    /// 바루나 — 특수행동 「넘치는 판결」(Detail_08 §16.3). 로카팔라 묶음 참여자(순서 -50, 바유 뒤 · 쿠베라 앞)로
    /// 적 전체에 공격력 70%, 생존 필드 아군 전원(자신 포함)에게 바루나 현재 방어력 100% 보호막.
    /// 물 부착이 없으므로 제방(특성) 보호막도 없다.
    /// </summary>
    public sealed class VarunaOverflowingJudgment : BaseSpecialCode
    {
        private const float AttackRatio = 0.70f;
        private const float PartyShieldRatio = 1.00f;

        public override int AttackForm => DamageTag.Spell;
        public override int OpenOrder => -50;

        public VarunaOverflowingJudgment(SpecialCodeContext context) : base(context) => CodeName = "넘치는 판결";

        protected override IEnumerator Perform()
        {
            yield return HitAllEnemies(_ => StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>
                {
                    DamageTag.AllTarget, DamageTag.SpecialAttack, DamageTag.NonContactAttack, DamageTag.Spell,
                },
                afterAll: () =>
                {
                    int amount = VarunaLawLevee.ShieldAmount(Caster, PartyShieldRatio);
                    if (amount <= 0) return;
                    foreach (Entities.Unit ally in Allies()) ally.AddShield(amount, Caster);
                },
                presentation: (targets, impact) => Effects.Vfx.LokapalaFx.Area("varuna.special", Caster, targets, impact));
        }
    }
}
