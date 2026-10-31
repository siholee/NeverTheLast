using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>실제 직접 피해 뒤에만 구매 불티·파편을 보탠다. 피해·상태·행동 콜백은 소유하지 않는다.</summary>
    public static class RosterImpactDetailFx
    {
        private sealed class Stamp
        {
            public int ActionId;
            public BaseEnums.CodeType CodeType;
            public float Until;
        }
        private sealed class Owner
        {
            public readonly ConditionalWeakTable<Unit,Stamp> Targets=new();
        }
        private static readonly ConditionalWeakTable<Unit,Owner> Owners=new();
        private static readonly Queue<float> ActiveUntil=new();
        private const float LifeWindow=1.05f;
        private const int MaxBursts=12;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static int Emitted { get; private set; }
#endif

        public static void Play(Unit target,DamageContext context,float weight)
        {
            Unit caster=context?.Attacker;
            // 검수 중인 구매 화살 세 명은 자신의 분산층을 이미 소유한다.
            if(caster==null || caster.IsEnemy || caster.ID is 164 or 165 or 166 || target==null ||
                !target.isActive || !target.IsOnField || caster.IsEnemy==target.IsEnemy ||
                context.CodeType==BaseEnums.CodeType.Effect || context.IsDamageOverTime ||
                context.IsSharedDamage || context.IsDistributed || context.HasTag(DamageTag.BonusDamage) ||
                context.HasTag(DamageTag.ElementalReaction))return;
            var view=CardMotion.ViewOf(target);
            if(view?.PortraitRenderer==null || view.PortraitRenderer.sprite==null || !view.gameObject.activeInHierarchy)return;
            float now=Time.time;
            while(ActiveUntil.Count>0&&ActiveUntil.Peek()<=now)ActiveUntil.Dequeue();
            if(ActiveUntil.Count>=MaxBursts)return;
            var scheduler=Managers.GameManager.Instance?.ActionScheduler;
            int action=scheduler?.ExecutingUnit!=null?scheduler.CurrentActionId:-1;
            var stamp=Owners.GetOrCreateValue(caster).Targets.GetOrCreateValue(target);
            // 같은 행동·대상의 살아 있는 분산층은 연타마다 중첩하지 않는다. 판정 횟수는 그대로다.
            if(stamp.Until>now && stamp.ActionId==action && stamp.CodeType==context.CodeType)return;
            stamp.ActionId=action;stamp.CodeType=context.CodeType;stamp.Until=now+LifeWindow;
            bool physical=context.HasTag(DamageTag.ContactAttack);
            float size=(physical?.64f:.58f)+Mathf.Clamp01(weight)*.10f;
            float opacity=.66f+Mathf.Clamp01(weight)*.10f;
            PurchasedImpactParticles.Play(target,VfxSpace.AimFrom(caster,target).Direction,size,
                ElementPalette.ColorFor(caster.Element),opacity,centered:caster.ID is 2 or 3 or 22 or 83 or 120 or 220 or 62 or 81 or 7 or 123 or 1 or 60 or 61 or 66,source:caster);
            ActiveUntil.Enqueue(now+LifeWindow);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Emitted++;
#endif
        }
    }
}
