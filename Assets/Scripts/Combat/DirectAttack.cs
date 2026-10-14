using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Vfx;
using Entities;

namespace Combat
{
    /// <summary>
    /// "직접 공격"의 한 줄 판정과, 연출 콜백이 오지 않아도 대상마다 판정을 정확히 한 번 여는 피격 도우미.
    ///
    /// 직접 공격 = 지속피해(<c>CodeType.Effect</c>) · 특성 직접 공격(<see cref="DamageTag.PassiveAttack"/>) ·
    /// 원소 반응 · 덧붙는 피해가 아닌 피해. <see cref="AttackTargetRouter"/>의 대상 교체 규칙과 같은 선을 쓴다.
    /// 바유의 원소 취약 · 수리야의 응답 사격(Detail_08 §14 · §18)이 함께 읽는다.
    /// </summary>
    public static class DirectAttack
    {
        public static bool IsDirect(DamageContext context)
            => context != null && context.Attacker != null &&
               context.CodeType != BaseEnums.CodeType.Effect &&
               !context.HasTag(DamageTag.PassiveAttack) &&
               !context.HasTag(DamageTag.ElementalReaction) &&
               !context.HasTag(DamageTag.BonusDamage);

        /// <summary>공격자의 고유 원소(판정 전용 원소). 소환수 피해는 공격자가 소환자로 기록되므로 소환자의 원소다.</summary>
        public static BaseEnums.UnitElement InnateElement(Unit unit)
            => unit != null && Enum.TryParse(unit.Element, true, out BaseEnums.UnitElement element)
                ? element
                : BaseEnums.UnitElement.None;

        /// <summary>
        /// 시전자가 아직 이 행동을 이어 갈 수 있는가. <see cref="Unit.Die"/>는 시전 코루틴을 멈추지 않으므로,
        /// 연출을 기다리는 사이 시전자가 쓰러지거나 전장을 떠나면 남은 판정을 열지 않는다.
        /// 제어는 행동을 시작한 뒤 걸린 경우를 따로 다룬다 — 추공처럼 제어가 곧 취소인 행동은 <paramref name="stopOnControl"/>을 켠다.
        /// </summary>
        public static bool CasterCanContinue(Unit caster, bool stopOnControl)
            => caster != null && caster.isActive && caster.IsOnField && !(stopOnControl && caster.isControlled);

        /// <summary>
        /// 시전자 한 번 시전 → 대상별 피격. 판정은 대상마다 정확히 한 번이다. 연출이 끝났는데 아직 판정이 열리지 않은 대상은
        /// 그 자리에서 마저 처리하고, 늦게 도착한 정점 콜백은 무시한다(8배속 지연 · 정의 없는 키 모두 같은 경로).
        /// 시전자가 도중에 행동을 이어 갈 수 없게 되면 남은 대상은 처리하지 않는다(유령 피해 방지).
        /// </summary>
        public static IEnumerator HitEach(string fxKey, Unit caster, IReadOnlyList<Unit> targets, Action<Unit> hit,
            float spreadSeconds = 0.05f, bool stopOnControl = false,
            Func<IReadOnlyList<Unit>, Action, IEnumerator> presentation = null)
        {
            List<Unit> ordered = targets?.Where(unit => unit != null && unit.isActive).ToList() ?? new List<Unit>();
            var resolved = new HashSet<Unit>();
            void ResolveOnce(Unit target)
            {
                if (target == null || !resolved.Add(target)) return;
                if (!CasterCanContinue(caster, stopOnControl) || !target.isActive) return;
                hit?.Invoke(target);
            }

            if (ordered.Count > 0)
            {
                if (presentation != null)
                    yield return presentation(ordered, () => { foreach (Unit target in ordered) ResolveOnce(target); });
                else
                    yield return VfxComposer.PlayMultiTargetHits(fxKey, caster, ordered, ResolveOnce, spreadSeconds);
            }
            foreach (Unit target in ordered) ResolveOnce(target);
        }

        /// <summary>단일 대상 판정 한 번. <see cref="HitEach"/>와 같은 보장을 준다.</summary>
        public static IEnumerator HitOne(string fxKey, Unit caster, Unit target, Action hit, bool stopOnControl = false,
            Func<Action, IEnumerator> presentation = null)
        {
            bool done = false;
            void ResolveOnce()
            {
                if (done) return;
                done = true;
                if (!CasterCanContinue(caster, stopOnControl)) return;
                hit?.Invoke();
            }

            if (caster != null && target != null)
                yield return presentation != null ? presentation(ResolveOnce)
                    : VfxComposer.PlaySingleTarget(fxKey, caster, target, ResolveOnce);
            ResolveOnce();
        }
    }
}
