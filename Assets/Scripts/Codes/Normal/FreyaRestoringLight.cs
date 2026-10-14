using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    public sealed class FreyaRestoringLight : NormalCode
    {
        public const float HealRatio = .08f;
        public FreyaRestoringLight(NormalCodeContext context) : base(context) { CodeName = "돌아오는 봄"; CodeType = BaseEnums.CodeType.Normal; }
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;
        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }
        protected override IEnumerator SkillCoroutine()
        {
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            yield return NordFx.FreyaPrayer(Caster);
            if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
            var grid = GridManager.Instance;
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
                int amount = Mathf.RoundToInt(Caster.HpMax * HealRatio);
            foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == Caster.IsEnemy).ToList())
                ally.Heal(amount, Caster);
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            yield return new WaitForSeconds(.12f);
            StopCode();
        }
        public override void StopCode() { base.StopCode(); if (Caster != null) Caster.isCasting = false; }
    }
}
