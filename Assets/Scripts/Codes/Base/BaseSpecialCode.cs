using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Base
{
    /// <summary>
    /// 연출 · 피해가 있는 특수행동의 뼈대(로카팔라 묶음 참여자 등). 시전 중 <c>isCasting</c>을 세우고,
    /// 끝나면 <see cref="SpecialCode.StopCode"/>가 묶음 종료를 알린다. 하위 클래스는 <see cref="Perform"/>만 채운다.
    /// </summary>
    public abstract class BaseSpecialCode : SpecialCode
    {
        private bool _finishingNaturally;
        /// <summary>연출이 들어오기 전까지 행동 사이를 읽히게 하는 최소 간격(초).</summary>
        protected const float MinimumBeat = 0.15f;

        protected BaseSpecialCode(SpecialCodeContext context) : base(context)
        {
            CastingDelay = 0.10f;
        }

        public override bool HasValidTarget() => Enemies().Count > 0;

        public override void CastCode()
        {
            if (!HasValidTarget())
            {
                StopCode();
                return;
            }

            Caster.isCasting = true;
            AudioManager.PlayAction(Caster, SfxActionSlot.Special, SfxCuePhase.Cast, CodeName);
            CurrSkillCoroutine = Caster.StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return Perform();
            yield return new WaitForSeconds(MinimumBeat);
            _finishingNaturally = true;
            StopCode();
            _finishingNaturally = false;
        }

        public override void StopCode()
        {
            if (!_finishingNaturally && Caster != null && CurrSkillCoroutine != null)
                Caster.StopCoroutine(CurrSkillCoroutine);
            base.StopCode();
        }

        protected abstract IEnumerator Perform();

        /// <summary>적 전체에게 같은 피해를 준다. 대상 교체 · 선제 개입 · 공지 순서는 공용 라우터를 따른다.</summary>
        protected IEnumerator HitAllEnemies(System.Func<Unit, int> rawDamage, List<int> tags,
            System.Action<Unit, DamageContext> afterHit = null,
            System.Func<IReadOnlyList<Unit>, System.Action, IEnumerator> presentation = null,
            System.Action afterAll = null)
        {
            Combat.AttackTargetResolution routing = Combat.AttackTargetRouter.Prepare(Caster, Enemies(),
                BaseEnums.CodeType.Special, tags, CodeName);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;

            bool resolved = false;
            void Resolve()
            {
                if (resolved) return;
                resolved = true;
                if (!Combat.DirectAttack.CasterCanContinue(Caster, true)) return;
                // 광역 연출도 원래 대상 순회 전체를 한 정점에서 실행해 치명타·상태·이벤트 순서를 보존한다.
                foreach (Unit target in targets.ToList())
                {
                    if (!Combat.DirectAttack.CasterCanContinue(Caster, true)) break;
                    if (target == null || !target.isActive) continue;
                    bool crit = RollCritical(target, out float multiplier);
                    var context = new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(rawDamage(target) * multiplier)),
                        BaseEnums.CodeType.Special, new List<int>(tags), crit);
                    target.TakeDamage(context);
                    if (Combat.DirectAttack.CasterCanContinue(Caster, true)) afterHit?.Invoke(target, context);
                }
                // 대상 순회가 끝난 뒤 한 번만 하는 일(아군 보호막 등). 같은 정점 콜백 안이라 연출이 없어도 한 번 보장된다.
                if (Combat.DirectAttack.CasterCanContinue(Caster, true)) afterAll?.Invoke();
                AudioManager.PlayAction(Caster, SfxActionSlot.Special, SfxCuePhase.Hit, CodeName);
            }
            if (presentation == null) Resolve();
            else yield return presentation(targets, Resolve);
            Resolve();
        }

        protected List<Unit> Enemies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField && !unit.IsUntargetable).ToList();
        }

        protected List<Unit> Allies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList();
        }
    }
}
