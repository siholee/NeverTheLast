using System;
using System.Collections;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>잔 전용 조립. 공격은 카드 동작이 시전을, YAML의 한 피격 묶음이 판정을 맡는다.</summary>
    public static class JeanFx
    {
        public static IEnumerator HolyStrike(Unit caster, Unit target, bool counter, Action onImpact)
        {
            string key = counter ? "jean.iron_sanction" : "jean.holy_strike";
            bool completed = false;
            void Impact()
            {
                if(counter)PortraitAccentFx.HeavyContact(target,new Color(.67f,.86f,1f),false);
                onImpact?.Invoke();
            }
            VfxRunner.Run(Complete(VfxComposer.PlaySingleTarget(key, caster, target, Impact,
                counter ? 0.92f : 1f, counter ? 0.34f : 0.46f), () => completed = true));

            // fighter 분류의 짧은 후퇴→부분 전진 자체가 시전이며 별도 폭발을 얹지 않는다.
            yield return CardMotion.GroundRakeAdvance(caster, target, counter ? 0.22f : 0.30f, null);
            float guard = 1.5f;
            while (!completed && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>자기 버프라 받는 연출 없이 원형 칼춤 한 단계의 정점에서 상태를 적용한다.</summary>
        public static IEnumerator TensionRising(Unit caster, Action onPeak)
        {
            const string key = "jean.tension_rising";
            if (caster == null)
            {
                onPeak?.Invoke();
                yield break;
            }

            VfxComposer.PlayScreen(key, caster, caster);
            float elapsed = 0f;
            for (int i = 0; VfxComposer.Slot(key, "cast", i) != null; i++)
            {
                float due = Mathf.Max(0f, VfxComposer.Offset(key, "cast", i));
                if (due > elapsed)
                {
                    yield return new WaitForSeconds(due - elapsed);
                    elapsed = due;
                }
                VfxComposer.PlaySlot(key, "cast", i, caster, caster, VfxSpace.WorldPoint(caster),
                    VfxSpace.SortingLayer(caster));
            }

            if (elapsed < 0.30f) yield return new WaitForSeconds(0.30f - elapsed);
            onPeak?.Invoke();
            yield return new WaitForSeconds(0.34f);
        }

        private static IEnumerator Complete(IEnumerator routine, Action done)
        {
            yield return routine;
            done?.Invoke();
        }
    }
}
