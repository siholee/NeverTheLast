#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Entities;
using Effects.Vfx;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    /// <summary>분신의 실제 생성·공격·넘침·막기·사망과 영혼 수확을 한 연속 1배속 영상으로 남긴다.</summary>
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator V13BSequence(string scenario, Text label)
        {
            Unit anubis = AliveAllies().First(unit => unit.ID == AnubisId);
            Unit uchi = AliveAllies().First(unit => unit.ID == UchiId);
            Unit kubera = AliveAllies().First(unit => unit.ID == KuberaId);
            if (scenario == "v13b_anubis")
            {
                yield return V13Segment(label, "일반 — 심장의 저울", "anubis.heart_scale", () => V13Cast(anubis, 0));
                yield return V13Segment(label, "초월기 — 사자의 서 · 자신만 강화", "anubis.book", () => V13Cast(anubis, 2));
                yield return V13Segment(label, "강화 일반 — 심장의 저울 · 바위 부착", "anubis.heart_scale.empowered", () => V13Cast(anubis, 0));
                yield return V13Segment(label, "특성 — 영혼 +0.25 · 작은 배지 펄스", "anubis.soul.fraction", () => V13BSoul(uchi, 1));
                yield return V13Segment(label, "특성 — 영혼 정수 증가 · 배지 펄스", "anubis.soul", () => V13BSoul(uchi, 3));
            }
            else if (scenario == "v13b_uchi")
            {
                yield return V13Segment(label, "일반 — 분신술 · 세 장 생성과 행동서열", "uchi.clone", () => V13BFillClones(uchi));
                yield return V13Segment(label, "분신 일반 — 분신 일격 · 공격 후 부적 소멸", "uchi.strike", () => V13BStrike(uchi));
                yield return V13BFillClones(uchi);
                yield return V13Segment(label, "넘침 — 가장 오래된 분신의 약한 일격", "uchi.overflow", () => V13BOverflow(uchi));
                yield return V13Segment(label, "특성 — 대신 맞기 · 분신 막기와 소멸", "uchi.guard", () => V13BGuard(uchi));
                yield return V13BFillClones(uchi);
                yield return V13Segment(label, "초월기 — 만천화우 · 분신 동시 부분 전진", "uchi.manchunhwau", () => V13Cast(uchi, 2));
                yield return V13Segment(label, "초월기 — 분신이 없으면 두 장 생성", "uchi.manchunhwau.empty", () => V13Cast(uchi, 2));
            }
            else
            {
                yield return V13Segment(label, "소환 편성 — 우치 분신술 → 쿠베라 보물", "summon.create", () => V13Cast(uchi, 0));
                yield return V13Segment(label, "소환 편성 — 보고 개방 → 분신 추가", "summon.treasury", () => V13Cast(kubera, 2));
                yield return V13Segment(label, "소환 편성 — 분신 일격 → 아누비스 영혼 +0.25", "summon.strike", () => V13BStrike(uchi));
                yield return V13Segment(label, "소환 편성 — 심장의 저울", "summon.heart_scale", () => V13Cast(anubis, 0));
                yield return V13Segment(label, "소환 편성 — 만천화우 → 소멸과 영혼 수확", "summon.manchunhwau", () => V13Cast(uchi, 2));
            }
        }

        private IEnumerator V13BFillClones(Unit owner)
        {
            while (Summons.Of(owner).Count < 3) yield return V13Cast(owner, 0);
            GameManager.Instance.ActionScheduler.BeginRound();
            // 레일은 다음 여섯 칸만 보여 준다. 느린 분신을 검수에서만 앞당겨 실제 초상화 로더를 확인한다.
            GameManager.Instance.ActionScheduler.AdvanceAction(Summons.Of(owner).First(), 1f);
            yield return WaitUnscaled(.7f);
            bool visible = FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Any(image => image.enabled && image.sprite != null && image.sprite.name.Contains("CLONE"));
            RecordMechanic("분신 행동서열 초상화", "CLONE_PORTRAIT 표시", visible.ToString(), visible);
            GameManager.Instance.ActionScheduler.EndRound();
        }

        private IEnumerator V13BStrike(Unit owner)
        {
            Unit clone = Summons.Of(owner).First();
            ActionScheduler scheduler = GameManager.Instance.ActionScheduler;
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            scheduler.BeginRound();
            scheduler.AdvanceAction(clone, 1f);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (clone != null && clone.isActive && Time.realtimeSinceStartup < deadline)
            {
                scheduler.Tick(Time.deltaTime);
                yield return null;
            }
            scheduler.EndRound();
            RecordMechanic("분신 실제 일반행동", "분신 차례의 공격과 소멸", clone == null || !clone.isActive ? "소멸" : "생존",
                clone == null || !clone.isActive);
            yield return WaitUnscaled(.7f);
        }

        private IEnumerator V13BOverflow(Unit owner)
        {
            Unit oldest = Summons.Of(owner).First();
            Summons.Create(owner);
            RecordMechanic("넘침 상한·오래된 분신", "3장·가장 오래된 분신 소멸",
                $"{Summons.Of(owner).Count}장·오래된 분신 {(oldest == null || !oldest.isActive ? "소멸" : "생존")}",
                Summons.Of(owner).Count == 3 && (oldest == null || !oldest.isActive));
            yield return WaitUnscaled(1f);
        }

        private IEnumerator V13BGuard(Unit owner)
        {
            Unit enemy = AliveEnemies().First();
            ActionScheduler scheduler = GameManager.Instance.ActionScheduler;
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            int before = Summons.Of(owner).Count;
            int hp = owner.HpCurr;
            scheduler.BeginRound();
            scheduler.EnqueuePriorityAdditional(enemy, "v13b_guard", "분신 대신 맞기 검수", () =>
            {
                enemy.isCasting = true;
                enemy.StartCoroutine(Attack());
            });
            IEnumerator Attack()
            {
                yield return EnemyNormalFx.OriginalNormal(enemy, owner, () => owner.TakeDamage(
                    new DamageContext(enemy, Mathf.CeilToInt(owner.HpMax * .20f), BaseEnums.CodeType.Normal,
                        new System.Collections.Generic.List<int> { DamageTag.NormalAttack, DamageTag.ContactAttack, DamageTag.Strike })));
                enemy.isCasting = false;
            }
            scheduler.Tick(0f);
            yield return WaitForCasting(enemy, 5f);
            scheduler.Tick(0f);
            scheduler.EndRound();
            RecordMechanic("분신 대신 맞기", "체력 불변·분신 1장 소멸", $"HP {hp}→{owner.HpCurr}·분신 {before}→{Summons.Of(owner).Count}",
                owner.HpCurr == hp && Summons.Of(owner).Count == before - 1);
            yield return WaitUnscaled(.7f);
        }

        private IEnumerator V13BSoul(Unit owner, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Summons.Create(owner)?.Die(null);
                yield return WaitUnscaled(.45f);
            }
        }
    }
}
#endif
