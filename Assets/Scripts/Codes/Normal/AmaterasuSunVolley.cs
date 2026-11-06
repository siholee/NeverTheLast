using System.Collections;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>마나를 모으는 단일 사격. 불 부착을 하지 않아 초월기의 연소·융해 재료를 남긴다.</summary>
    public sealed class AmaterasuSunVolley : BaseNormalCode
    {
        public const float ArrowRatio = 1f;
        public override int AttackForm => DamageTag.Shot;
        public AmaterasuSunVolley(NormalCodeContext context) : base(context) => CodeName = "여명을 겨누는 화살";
        protected override int BaseDamage(Unit target) => StatDamage(0, BaseEnums.StatType.Attack, ArrowRatio);
        protected override IEnumerator Deliver(Unit target)
        {
            if (!DirectAttack.CasterCanContinue(Caster, false) || target == null || !target.isActive ||
                CurrentCommand != null && !CurrentCommand.Active) yield break;
            CardMotion.Throw(Caster, target);
            VfxComposer.PlaySlot("amaterasu.normal", "cast", 0, Caster, target,
                VfxSpace.WorldPoint(Caster), VfxSpace.SortingLayer(Caster));
            DamageContext context = BuildContext(target);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Projectile, CodeName);
            yield return DirectAttack.HitOne("amaterasu.normal", Caster, target, () => Hit(target, context),
                presentation: impact => PurchasedProjectileFx.Fly(Caster,target,.20f,impact));
        }
    }
}
