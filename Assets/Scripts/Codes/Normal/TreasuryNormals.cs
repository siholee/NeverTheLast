using System.Collections;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;
namespace Codes.Normal
{
    public sealed class MansaRoyalShield : NormalCode
    {
        public MansaRoyalShield(NormalCodeContext context) : base(context) { CodeName = "왕실의 방호"; CodeType = BaseEnums.CodeType.Normal; }
        public override bool HasValidTarget() => PartyTreasury.CanUse(Caster);
        public override void CastCode() { Caster.isCasting = true; CurrSkillCoroutine = Caster.StartCoroutine(Perform()); }
        private IEnumerator Perform()
        {
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            yield return new WaitForSeconds(.20f);
            if (DirectAttack.CasterCanContinue(Caster, true))
            {
                if (PartyTreasury.Spend(Caster, 1))
                    foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)) ally.AddShield(StatDamage(0, BaseEnums.StatType.Defense, .8f), Caster, treasurySpent: true);
                Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            }
            yield return new WaitForSeconds(.20f); StopCode();
        }
        public override void StopCode() { if (Caster != null && CurrSkillCoroutine != null) Caster.StopCoroutine(CurrSkillCoroutine); if (Caster != null) Caster.isCasting = false; base.StopCode(); }
    }
    public sealed class IshtarVenusShot : BaseNormalCode
    {
        public IshtarVenusShot(NormalCodeContext context) : base(context) => CodeName = "금성 사격";
        public override int AttackForm => DamageTag.Shot;
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, .8f);
        protected override void OnAttackResolved(Unit target, DamageContext context)
        { if (target != null && target.isActive) target.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: Caster); }
    }
}
