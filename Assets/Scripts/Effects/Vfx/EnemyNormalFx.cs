using System;
using System.Collections;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>시험장 적 일반행동과 궁극기의 방향·등급·대표색을 96_vfx 부품 조합에 연결한다.</summary>
    public static class EnemyNormalFx
    {
        /// <summary>일반 등급의 지상 짐승만 짧은 전진 할퀴기를 쓴다. 인간형 선봉과 엘리트는 제외한다.</summary>
        public static bool UsesGroundRake(Unit caster)
            => caster != null && caster.IsEnemy && caster.HasUnitTag("Beast");

        /// <summary>돌격 코드를 쓰는 공허 괴수만 엘리트 크기의 제자리 중타격 조합을 쓴다.</summary>
        public static bool UsesHeavyStrike(Unit caster)
            => caster != null && caster.IsEnemy && caster.HasUnitTag("Void") &&
               (caster.HasUnitTag("Giant") || caster.HasUnitTag("Monstrous"));

        public static IEnumerator HeavyStrike(Unit caster, Unit target, Action onHit)
        {
            yield return MotionStrike("enemy.void_crusher.strike", caster, target,
                WithCardReaction(caster, target, onHit, 0.72f),
                string.Equals(caster?.UnitTier, "elite", StringComparison.OrdinalIgnoreCase) ? 1.18f : 1f,
                0.34f, 0.34f);
        }

        /// <summary>
        /// 경호처럼 최종 대상만 바뀐 시험에서도 원래 공격자의 병과·색·피격을 그대로 사용한다.
        /// 공격 코드와 하네스가 같은 분기표를 공유해 바스테트 연출로 잘못 대체되는 일을 막는다.
        /// </summary>
        public static IEnumerator OriginalNormal(Unit caster, Unit target, Action onHit)
        {
            if (UsesGroundRake(caster))
            {
                yield return GroundRake(caster, target, onHit);
                yield break;
            }

            if (UsesHeavyStrike(caster))
            {
                yield return HeavyStrike(caster, target, onHit);
                yield break;
            }

            if (caster != null && string.Equals(caster.VfxClass, "archer", StringComparison.OrdinalIgnoreCase))
            {
                yield return MirroredVolley(caster, target, onHit);
                yield break;
            }

            yield return VanguardStrike(caster, target, onHit);
        }

        public static IEnumerator GroundRake(Unit caster, Unit target, Action onHit)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            bool rakeCompleted = false;
            IEnumerator Rake()
            {
                AudioManager.PlayAction(caster, SfxActionSlot.Normal, SfxCuePhase.Draw, "돌격");
                yield return VfxComposer.PlaySingleTarget("enemy.void_wolf.charge", caster, target,
                    onHit, 1f, 0.34f);
                rakeCompleted = true;
            }

            // 복귀가 시작될 때 세 줄 그리기는 끝났고, 잔상만 남도록 정점 유지 시간을 잡는다.
            yield return CardMotion.GroundRakeAdvance(caster, target, 0.34f, () => VfxRunner.Run(Rake()));
            float guard = 1.2f;
            while (!rakeCompleted && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        public static IEnumerator MirroredVolley(Unit caster, Unit target, Action onHit)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            bool boss = string.Equals(caster.UnitTier, "boss", StringComparison.OrdinalIgnoreCase);
            if (boss)
                VfxRunner.Run(CardMotion.Channel(caster, 0.12f, 0.18f, 0.22f));
            yield return VfxComposer.PlaySingleTarget(
                boss ? "enemy.void_dragon.shot" : "enemy.void_marksman.shot",
                caster, target, onHit, boss ? 1.34f : 1f, boss ? 0.68f : 0.52f);
        }

        /// <summary>야수 강타는 다른 충격으로 바꾸지 않고 같은 세 갈래 할퀴기의 강화판만 쓴다.</summary>
        public static IEnumerator BeastHeavyClaw(Unit caster, Unit target, Action onHit)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            bool motionDone = false;
            VfxRunner.Run(Complete(CardMotion.GroundRakeAdvance(caster, target, 0.38f, null),
                () => motionDone = true));
            yield return VfxComposer.PlaySingleTarget("enemy.void_beast.smash", caster, target,
                WithCardReaction(caster, target, onHit, 0.78f), 1f, 0.50f);
            float guard = 1.2f;
            while (!motionDone && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>인간형 선봉도 대상까지 이동하지 않고 제자리 반동과 대상 피격으로 돌격을 표현한다.</summary>
        public static IEnumerator VanguardStrike(Unit caster, Unit target, Action onHit)
        {
            yield return MotionStrike("enemy.void_vanguard.charge", caster, target,
                WithCardReaction(caster, target, onHit, 0.38f),
                1f, 0.36f, 0.28f);
        }

        /// <summary>적 궁극기 한 타를 공용 조합표의 시전·피격 슬롯으로 재생한다.</summary>
        public static IEnumerator UltimateSingle(string skillKey, Unit caster, Unit target,
            Action onHit, float recoil = 0.62f, float minimumDuration = 0.42f)
        {
            if (caster == null || target == null)
            {
                onHit?.Invoke();
                yield break;
            }

            float reaction = string.Equals(skillKey, "enemy.ultimate.ground_breaker", StringComparison.Ordinal)
                ? 0.82f : 0.68f;
            yield return MotionStrike(skillKey, caster, target,
                WithCardReaction(caster, target, onHit, reaction), 1f, minimumDuration,
                Mathf.Lerp(0.28f, 0.38f, Mathf.InverseLerp(0.62f, 0.78f, recoil)));
        }

        /// <summary>
        /// 연사·숨결은 한 번만 집속하고 대상별 피격 정점만 시간차로 연다.
        /// 대상 선택과 피해 컨텍스트 생성은 매 간격마다 호출부에 돌려 전투 계약을 보존한다.
        /// </summary>
        public static IEnumerator RepeatedUltimate(string skillKey, Unit caster, int count,
            float interval, Func<Unit> pickTarget, Func<Unit, Action> buildImpact,
            float castLead = 0f, float extraScale = 1f, float castDelay = 0f,
            bool repeatCastPerHit = false)
        {
            Unit first = pickTarget?.Invoke();
            if (caster == null || first == null) yield break;

            VfxComposer.PlayScreen(skillKey, caster, first);
            VfxComposer.PlaySlot(skillKey, "prepare", 0, caster, first,
                VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster), extraScale);
            // 지연된 시전광은 기존 판정까지 남은 lead 안에서만 옮긴다. 전체 lead를 늘리지 않아 피해 시각은 그대로다.
            float clampedCastDelay = Mathf.Clamp(castDelay, 0f, Mathf.Max(0f, castLead));
            if (clampedCastDelay > 0f) yield return new WaitForSeconds(clampedCastDelay);
            if (!repeatCastPerHit)
                VfxComposer.PlaySlot(skillKey, "cast", 0, caster, first,
                    VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster), extraScale);
            // 숨결의 집속을 앞에서 보여 주되 기존 첫 판정 시각은 castLead 뒤로 그대로 둔다.
            float remainingLead = Mathf.Max(0f, castLead - clampedCastDelay);
            if (remainingLead > 0f) yield return new WaitForSeconds(remainingLead);

            int pending = 0;
            for (int i = 0; i < count; i++)
            {
                Unit target = i == 0 ? first : pickTarget?.Invoke();
                if (target == null) break;
                if (repeatCastPerHit)
                {
                    VfxComposer.PlaySlot(skillKey, "cast", 0, caster, target,
                        VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster), extraScale);
                    // BaseUltimateCode가 첫 시전음을 이미 낸다. 두 번째 탄부터만 추가해 총 3회로 맞춘다.
                    if (i > 0)
                        AudioManager.PlayAction(caster, SfxActionSlot.Ultimate, SfxCuePhase.Cast, "연사");
                }
                Action impact = buildImpact?.Invoke(target);
                pending++;
                VfxRunner.Run(Complete(VfxComposer.PlayHitSlot(skillKey, 0, caster, target,
                    impact, extraScale), () => pending--));
                if (i + 1 < count) yield return new WaitForSeconds(interval);
            }

            float guard = 2.5f;
            while (pending > 0 && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        private static IEnumerator Complete(IEnumerator routine, Action onComplete)
        {
            yield return routine;
            onComplete?.Invoke();
        }

        /// <summary>판정 정점은 그대로 두고, 묵직한 접촉 피격만 대상 카드의 짧은 반동을 함께 연다.</summary>
        private static Action WithCardReaction(Unit caster, Unit target, Action onHit, float strength)
        {
            return () =>
            {
                Vector3 direction = VfxSpace.AimFrom(caster, target).Direction;
                CardMotion.RakeReaction(target, direction, strength);
                onHit?.Invoke();
            };
        }

        /// <summary>
        /// 짧은 후퇴→부분 전진이 시전 동작을 대신한다. 피격 조합은 기존 시각에 바로 시작해
        /// 모션을 추가해도 판정 정점이 늦어지지 않게 하고, 복귀가 끝날 때까지 행동을 붙든다.
        /// </summary>
        private static IEnumerator MotionStrike(string skillKey, Unit caster, Unit target, Action onHit,
            float extraScale, float minimumDuration, float holdTime)
        {
            bool motionDone = false;
            VfxRunner.Run(Complete(CardMotion.GroundRakeAdvance(caster, target, holdTime, null),
                () => motionDone = true));
            yield return VfxComposer.PlaySingleTarget(skillKey, caster, target, onHit,
                extraScale, minimumDuration);

            float guard = 1.2f;
            while (!motionDone && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

    }
}
