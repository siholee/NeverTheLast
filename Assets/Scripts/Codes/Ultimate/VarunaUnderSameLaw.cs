using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 바루나 — 초월기 「동일한 법 아래」(Detail_08 §16.3). 적 전체에 공격력 120%와 물 부착,
    /// 생존 필드 아군 전원(자신 포함)에게 바루나 현재 방어력 100%의 보호막. 보호막은 피해 · 치명타와 무관하게 한 번씩 준다.
    /// 제방(특성)의 자신 35% 보호막은 첫 물 부착 시점에 따로 들어오는 독립된 부여다.
    /// </summary>
    public sealed class VarunaUnderSameLaw : BaseUltimateCode
    {
        private const float AttackRatio = 1.20f;
        public const float PartyShieldRatio = 1.00f;

        public override int AttackForm => DamageTag.Spell;

        public VarunaUnderSameLaw(UltimateCodeContext context) : base(context) => CodeName = "동일한 법 아래";

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, DamageTag.Spell };
            AttackTargetResolution routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Targeting.Announce(Caster, targets)) yield break;

            bool shielded = false;
            yield return Effects.Vfx.LokapalaFx.Area("varuna.ultimate", Caster, targets, () =>
            {
                foreach (Unit target in targets.ToList())
                {
                    if (target == null || !target.isActive) continue;
                    target.TakeDamage(BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), new List<int>(tags), target));
                    if (!target.isActive) continue;
                    target.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: Caster);
                }

                shielded = true;
                ShieldParty();
                AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
            });
            // 연출 도중 대상이 모두 사라져 정점 콜백이 오지 않아도 아군 보호막은 피해와 무관하게 한 번 준다.
            if (!shielded && Caster != null && Caster.isActive) ShieldParty();
            yield return new WaitForSeconds(0.15f);
        }

        /// <summary>생존 필드 아군 전원(자신 포함)에게 현재 방어력 100%. 각자 한 번씩, 공용 보호막 보정을 탄다.</summary>
        private void ShieldParty()
        {
            int amount = VarunaLawLevee.ShieldAmount(Caster, PartyShieldRatio);
            if (amount <= 0) return;
            foreach (Unit ally in AliveAllies()) ally.AddShield(amount, Caster);
        }
    }
}
