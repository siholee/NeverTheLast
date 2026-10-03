using System;
using System.Collections;
using Entities;
using Entities.View;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 코드가 카드를 움직일 때 부르는 창구. 카드가 없는 경우(대기석 · 이미 쓰러짐)에도
    /// 판정 콜백은 반드시 불려 전투가 멈추지 않는다.
    /// </summary>
    public static class CardMotion
    {
        public static UnitCardView ViewOf(Unit unit) => unit != null && unit.currentCell != null ? unit.currentCell.Card : null;

        /// <summary>
        /// 하스스톤식 근접 공격 — 카드가 날아가 대상 카드에 부딪히고 돌아온다.
        /// 부딪히는 순간 <paramref name="onImpact"/>(충돌 지점, 진행 방향)가 불린다.
        /// </summary>
        public static IEnumerator Charge(Unit attacker, Unit target, Action<Vector3, Vector3> onImpact)
        {
            UnitCardView view = ViewOf(attacker);
            Vector3 lastTarget = VfxSpace.WorldPoint(target);
            if (view == null)
            {
                Vector3 from = VfxSpace.WorldPoint(attacker);
                onImpact?.Invoke(lastTarget, (lastTarget - from).normalized);
                yield break;
            }

            Vector3 TargetCenter()
            {
                if (target != null && target.isActive) lastTarget = VfxSpace.WorldPoint(target);
                return lastTarget;
            }

            yield return view.Charge(TargetCenter, onImpact);
        }

        /// <summary>던지는 반동 — 카드가 대상 쪽으로 짧게 튄다. 투사체를 내는 그 프레임에 부른다.</summary>
        public static void Throw(Unit caster, Unit target, float strength = 1f)
        {
            UnitCardView view = ViewOf(caster);
            if (view == null) return;
            Vector3 direction = VfxSpace.WorldPoint(target) - VfxSpace.WorldPoint(caster);
            view.PlayThrow(direction, strength);
        }

        /// <summary>할퀴기 선두가 지날 때 대상 카드를 진행 방향으로 짧게 밀고 찌그러뜨린다.</summary>
        public static void RakeReaction(Unit target, Vector3 direction, float strength = 0.55f)
        {
            UnitCardView view = ViewOf(target);
            if (view == null) return;
            view.PlayVfxKick(direction, strength);
        }

        /// <summary>지상형 적이 뒤로 물러났다 짧게 전진하고, 전진 정점에서 할퀴기를 시작한 뒤 복귀한다.</summary>
        public static IEnumerator GroundRakeAdvance(Unit attacker, Unit target, float holdTime, Action onAdvanced)
        {
            UnitCardView view = ViewOf(attacker);
            if (view == null)
            {
                onAdvanced?.Invoke();
                yield return new WaitForSeconds(Mathf.Max(0f, holdTime));
                yield break;
            }

            Vector3 direction = VfxSpace.WorldPoint(target) - VfxSpace.WorldPoint(attacker);
            yield return view.GroundRakeAdvance(direction, holdTime, onAdvanced);
        }

        /// <summary>선제 개입 직전 원 공격의 절반 시전만 보여 주고 제자리로 되감는다.</summary>
        public static IEnumerator InterruptionWindup(Unit attacker, Unit target)
        {
            UnitCardView view = ViewOf(attacker);
            if (view == null)
            {
                yield return new WaitForSeconds(0.30f);
                yield break;
            }
            Vector3 direction = VfxSpace.WorldPoint(target) - VfxSpace.WorldPoint(attacker);
            yield return view.InterruptionWindup(direction);
        }

        /// <summary>사바흐의 그림자 이동. 카드가 없으면 판정 위치만 즉시 넘길 수 있게 빈 동작으로 끝난다.</summary>
        public static IEnumerator ShadowApproach(Unit caster, Unit target)
        {
            UnitCardView view = ViewOf(caster);
            if (view == null) yield break;
            Vector3 lastTarget = VfxSpace.WorldPoint(target);
            Vector3 TargetCenter()
            {
                if (target != null && target.isActive) lastTarget = VfxSpace.WorldPoint(target);
                return lastTarget;
            }
            yield return view.ShadowApproach(TargetCenter);
        }

        public static IEnumerator ShadowReturn(Unit caster)
        {
            UnitCardView view = ViewOf(caster);
            if (view == null) yield break;
            yield return view.ShadowReturn();
        }

        public static IEnumerator ShadowVanish(Unit caster)
        {
            UnitCardView view = ViewOf(caster);
            if (view == null) yield break;
            yield return view.ShadowVanish();
        }

        public static IEnumerator ShadowReappear(Unit caster)
        {
            UnitCardView view = ViewOf(caster);
            if (view == null) yield break;
            yield return view.ShadowReappear();
        }

        /// <summary>카드가 들렸다 내려앉는 시전 동작. 궁극기 · 광역 버프가 쓴다.</summary>
        public static IEnumerator Channel(Unit caster, float riseTime = 0.18f, float holdTime = 0.2f, float settleTime = 0.24f)
        {
            UnitCardView view = ViewOf(caster);
            if (view == null) yield break;
            yield return view.Channel(riseTime, holdTime, settleTime);
        }
    }
}
