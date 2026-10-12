using System;
using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Special;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 나폴레옹 — 일반행동 「포격 지점 지정」(Detail_08 §20.3). 피해를 주지 않는 지휘 행동이다.
    ///
    /// 우선도를 보지 않고 지금 가장 쉽게 쓰러뜨릴 적 하나(현재 체력 · 방어력 · 실제 참여 아군의 추정 일제사격)를 골라
    /// 나폴레옹의 특수행동 「일제 사격」 하나를 그 대상으로 고정해 예약한 뒤, 일반행동을 한 번 해결해 마나 +20을 받는다.
    /// 참여 아군이 없으면 예약 없이 일반행동만 해결한다.
    /// </summary>
    public sealed class NapoleonCommandShot : NormalCode
    {
        /// <summary>연출 · 검증이 읽는 지휘 흐름(나폴레옹, 고정 대상, 특수행동 예약 성공).</summary>
        public static event Action<Unit, Unit, bool> AnyOrderIssued;

        public NapoleonCommandShot(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            CodeName = "포격 지점 지정";
            CastingDelay = 0.10f;
        }

        /// <summary>마지막으로 지정한 대상. 검증용.</summary>
        public Unit LastOrderedTarget { get; private set; }

        // 맞힐 수 있는 적이 하나도 없어도(전원 무적 등) 지휘 행동 자체는 해결해 마나를 받는다. 예약만 건너뛴다.
        public override bool HasValidTarget() => Combat.CombatTargets.AliveEnemies(Caster).Count > 0;

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool castCompleted = true;
            yield return WaitForCast(ok => castCompleted = ok);
            if (!castCompleted)
            {
                StopCode();
                yield break;
            }

            Unit target = NapoleonCommand.PickTarget(Caster);
            LastOrderedTarget = target;
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            if (target != null)
            {
                VfxComposer.PlayScreen("napoleon.normal", Caster, target);
                VfxComposer.PlaySlot("napoleon.normal", "cast", 0, Caster, target,
                    VfxSpace.WorldPoint(Caster), VfxSpace.SortingLayer(Caster));
            }

            // 예약이 먼저, 해결이 나중이다 — 초월기 버프의 일반행동 시계가 이번 지휘의 일제사격까지 덮도록 한다.
            bool queued = target != null && Caster.ActiveSpecialCode is NapoleonGrandVolley volley && volley.Order(target);
            AnyOrderIssued?.Invoke(Caster, target, queued);
            yield return new WaitForSeconds(0.22f);

            if (Caster.isActive) Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster != null) Caster.isCasting = false;
        }
    }
}
