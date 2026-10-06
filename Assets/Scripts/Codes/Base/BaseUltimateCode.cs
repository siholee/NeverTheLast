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
    /// 연출이 있는 궁극기의 뼈대. 시전 중에는 <c>isCasting</c>을 세워 스케줄러가 다음 행동을 기다리게 하고,
    /// 코루틴이 끝나면 내린다. 하위 클래스는 <see cref="Perform"/>만 채운다.
    /// </summary>
    public abstract class BaseUltimateCode : UltimateCode
    {
        private readonly List<Combat.AttackTargetResolution> _routings = new();
        private bool _stoppedExternally;
        private bool _finishingNaturally;
        private bool _effectApplied;
        /// <summary>공격 중 취소 가드를 쓰는 코드만 실제 효과 발생 근거를 요구한다.</summary>
        protected virtual bool RequiresCompletionEvidence => false;
        protected void MarkEffectApplied() => _effectApplied = true;
        /// <summary>스케줄러 종료와 별개로, 선제 취소 없이 본체 코루틴이 정상 끝났는가.</summary>
        public bool CompletedNormally { get; private set; }
        protected BaseUltimateCode(UltimateCodeContext context) : base(context)
        {
            CastingDelay = 0f;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            CompletedNormally = false;
            _effectApplied = false;
            _stoppedExternally = false;
            _routings.Clear();
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Cast, CodeName);
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return Perform();
            bool normal = !_stoppedExternally && Caster != null && Caster.isActive && Caster.IsOnField &&
                                _routings.All(routing => !routing.CancelledBeforeAttack) &&
                                (!RequiresCompletionEvidence || _effectApplied);
            _finishingNaturally = true;
            StopCode();
            _finishingNaturally = false;
            CompletedNormally = normal;
        }

        /// <summary>궁극기 본체. 피해 · 연출을 여기서 한다.</summary>
        protected abstract IEnumerator Perform();

        public override void StopCode()
        {
            if (!_finishingNaturally)
            {
                _stoppedExternally = true;
                if (Caster != null && CurrSkillCoroutine != null) Caster.StopCoroutine(CurrSkillCoroutine);
            }
            CompletedNormally = false;
            if (Caster != null) Caster.isCasting = false;
        }

        public override bool HasValidTarget() => AliveEnemies().Count > 0;

        protected List<Unit> AliveEnemies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit && unit.IsOnField && !unit.IsUntargetable).ToList();
        }

        protected List<Unit> AliveAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit && unit.IsOnField).ToList();
        }

        /// <summary>우선도가 가장 높은 적 하나(같으면 무작위). 도발 · 전열 규칙을 따른다.</summary>
        protected Unit PickPriorityEnemy()
        {
            List<Unit> enemies = AliveEnemies();
            if (enemies.Count == 0) return null;
            int top = enemies.Max(unit => unit.Priority);
            List<Unit> best = enemies.Where(unit => unit.Priority == top).ToList();
            return best[Random.Range(0, best.Count)];
        }

        /// <summary>치명타를 굴려 피해 정보를 만든다. 경감은 피격자가 한다.</summary>
        protected DamageContext BuildContext(int rawDamage, List<int> tags, Unit target = null)
        {
            bool crit = RollCritical(target, out float multiplier);
            return new DamageContext(Caster, Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier)),
                BaseEnums.CodeType.Ultimate, tags, crit);
        }

        /// <summary>궁극기 대상도 일반행동과 같은 교체 → 선제 개입 → 공지 순서를 거친다.</summary>
        protected Combat.AttackTargetResolution PrepareTargets(IReadOnlyList<Unit> targets, List<int> tags)
        {
            var routing = Combat.AttackTargetRouter.Prepare(Caster, targets,
                BaseEnums.CodeType.Ultimate, tags, CodeName);
            _routings.Add(routing);
            return routing;
        }
    }
}
