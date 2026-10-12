using System;
using System.Collections;
using Entities;

namespace Effects.Vfx
{
    /// <summary>바스테트 전용 조립. 카드 동작이 시전을 맡고 YAML은 대상 피격만 맡는다.</summary>
    public static class BastetFx
    {
        public static IEnumerator CatPunch(Unit caster, Unit target, bool additional, Action onImpact)
        {
            // U122는 별도 기술이 아니라 냥냥펀치를 한 번 더 수행하는 추가행동이다.
            // 행동서열 종류만 Additional로 남기고, 시전·피격·박자는 일반행동과 완전히 공유한다.
            const string key = "bastet.cat_punch";
            bool effectStarted = false;
            bool effectCompleted = false;
            void StartImpact()
            {
                if (effectStarted) return;
                effectStarted = true;
                VfxRunner.Run(Complete(VfxComposer.PlaySingleTarget(key, caster, target, onImpact,
                    1f, 0.38f), () => effectCompleted = true));
            }

            // 1×에서 카드 전진이 끝난 뒤 피격 지연을 다시 세면 승인된 시·사바흐 박자보다 길어진다.
            // 피격 타이머를 준비 동작과 함께 열어 전진 정점 직후 대상 충격이 오게 한다.
            StartImpact();
            yield return CardMotion.GroundRakeAdvance(caster, target, 0.30f, null);
            float guard = 1.5f;
            while (!effectCompleted && guard > 0f)
            {
                guard -= UnityEngine.Time.deltaTime;
                yield return null;
            }
        }

        public static IEnumerator GuardIntervention(Unit bastet, Unit guard)
        {
            if (bastet == null || guard == null) yield break;
            // 대신 맞는 주체는 guard이며 바스테트는 보호받는다. 판정·행동 대기 시간은 바꾸지 않는다.
            PortraitAccentFx.GuardLink(guard,bastet);
            Effects.StatusPopup.ShowGuard(guard);
            bool cuePlayed = false;
            yield return CardMotion.GroundRakeAdvance(guard, bastet, 0.18f, () =>
            {
                cuePlayed = true;
                VfxComposer.PlayAffectedSlot("bastet.guard", 0, bastet, guard);
            });
            if (!cuePlayed) VfxComposer.PlayAffectedSlot("bastet.guard", 0, bastet, guard);
        }

        private static IEnumerator Complete(IEnumerator routine, Action done)
        {
            yield return routine;
            done?.Invoke();
        }
    }
}
