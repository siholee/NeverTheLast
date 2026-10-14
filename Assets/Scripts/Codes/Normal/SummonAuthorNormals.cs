using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Vfx;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    public sealed class ShakespeareInspiration : NormalCode
    {
        public ShakespeareInspiration(NormalCodeContext context) : base(context) { CodeName = "영감"; CodeType = BaseEnums.CodeType.Normal; }
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;
        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Resolve());
        }
        private IEnumerator Resolve()
        {
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            yield return SummonAuthorsFx.Prayer(Caster, true);
            if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
            Caster.ActivePassiveCodes.OfType<ShakespeareLanguage>().FirstOrDefault()?.Gain(3);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            Caster.isCasting = false;
        }
        public override void StopCode() { base.StopCode(); if (Caster == null) return; if (CurrSkillCoroutine != null) Caster.StopCoroutine(CurrSkillCoroutine); Caster.isCasting = false; }
    }
    public sealed class ThothLifeScript : NormalCode
    {
        public ThothLifeScript(NormalCodeContext context) : base(context) { CodeName = "생명의 문장"; CodeType = BaseEnums.CodeType.Normal; }
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.IsOnField;
        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Resolve());
        }
        private IEnumerator Resolve()
        {
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            yield return SummonAuthorsFx.Prayer(Caster, false);
            if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
            foreach (var ally in SummonSupportCombat.Allies(Caster, true).ToList())
                ally.Heal(Mathf.RoundToInt(Caster.HpMax * .05f), Caster);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            Caster.isCasting = false;
        }
        public override void StopCode() { base.StopCode(); if (Caster == null) return; if (CurrSkillCoroutine != null) Caster.StopCoroutine(CurrSkillCoroutine); Caster.isCasting = false; }
    }
    public sealed class ThothScribeHeal : NormalCode
    {
        public const int CodeId = 502;
        public ThothScribeHeal(NormalCodeContext context) : base(context) { CodeName = "여백의 치유"; CodeType = BaseEnums.CodeType.Normal; }
        public override bool HasValidTarget() => Caster != null && Caster.isActive && Caster.SummonOwner != null && Caster.SummonOwner.isActive;
        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Resolve());
        }
        private IEnumerator Resolve()
        {
            var owner = Caster.SummonOwner;
            yield return SummonAuthorsFx.Prayer(Caster, false);
            if (!HasValidTarget() || Caster.isControlled) { StopCode(); yield break; }
            var target = SummonSupportCombat.Allies(owner, true).OrderBy(x => x.HpCurr / (float)Mathf.Max(1, x.HpMax)).FirstOrDefault();
            target?.Heal(Mathf.RoundToInt(owner.HpMax * .03f), owner);
            AudioManager.PlayCue("thoth.scribe.hit");
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            Caster.isCasting = false;
        }
        public override void StopCode() { base.StopCode(); if (Caster == null) return; if (CurrSkillCoroutine != null) Caster.StopCoroutine(CurrSkillCoroutine); Caster.isCasting = false; }
    }
}
