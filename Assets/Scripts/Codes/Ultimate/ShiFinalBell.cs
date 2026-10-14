using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 시 — 패시브형 초월기 「만종」.
    /// 멸참 3·6·9회째의 피격이 끝난 뒤 각각 단일·전체 분배·강화 전체 분배 피해를 잇는다.
    /// </summary>
    public sealed class ShiFinalBell : UltimateCode
    {
        public const string ResourceId = "shi_final_bell";
        public const int MaximumStacks = 9;
        private bool _registered;

        public override bool IsAutoCast => false;
        public override bool IsPersistent => true;

        public ShiFinalBell(UltimateCodeContext context) : base(context)
        {
            CodeName = "만종";
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            CombatResourceLabels.Register(ResourceId, "만종");
            Caster.SetCombatResourceMaximum(ResourceId, MaximumStacks, true);
            _registered = true;
        }

        public override void StopCode()
        {
            _registered = false;
        }

        /// <summary>멸참 사용을 기록하고 이번 타격 뒤 이어질 만종 단계(없으면 0)를 돌려준다.</summary>
        public int RegisterDestructionSlash()
        {
            if (!DirectAttack.CasterCanContinue(Caster, false) || !_registered) return 0;
            int stack = Caster.AddCombatResource(ResourceId, 1);
            return stack is 3 or 6 or 9 ? stack : 0;
        }

        public IEnumerator ResolveBell(int tier, Unit struckTarget)
        {
            if (!DirectAttack.CasterCanContinue(Caster, false) || tier is not (3 or 6 or 9)) yield break;

            List<Unit> enemies = AliveEnemies();
            Unit directionTarget = struckTarget != null ? struckTarget : enemies.FirstOrDefault();
            Vector3 center = tier == 3
                ? VfxSpace.WorldPoint(directionTarget)
                : FormationCenter(enemies, directionTarget);
            List<Unit> targets = tier == 3
                ? new List<Unit> { struckTarget }.Where(unit => unit != null && unit.isActive).ToList()
                : enemies;
            if (targets.Count == 0) yield break;

            bool crit = RollCritical(out float multiplier);
            float ratio = tier == 3 ? ThreeStackRatio(Caster.Level) : tier == 6 ? 2f : 3f;
            int total = Mathf.Max(1, Mathf.RoundToInt(
                Caster.StatValue(BaseEnums.StatType.Attack) * ratio * multiplier));
            int each = tier == 3 ? total : Mathf.Max(1, Mathf.RoundToInt(total / (float)targets.Count));
            List<int> tags = new()
            {
                tier == 3 ? DamageTag.SingleTarget : DamageTag.AllTarget,
                DamageTag.UltAttack,
                DamageTag.NonContactAttack,
                DamageTag.BonusDamage,
            };
            var contexts = targets.ToDictionary(target => target, _ => new DamageContext(Caster, each,
                BaseEnums.CodeType.Ultimate, new List<int>(tags), crit));
            string key = $"shi.final_bell.{tier}";
            ActionAnnouncer.Announce(Caster, ActionAnnouncementKind.Ultimate, this);
            AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Screen, CodeName);
            VfxComposer.PlayScreen(key, Caster, directionTarget);

            bool sfxPlayed = false;
            yield return VfxComposer.PlayHorizontalSweepHit(key, 0, Caster, directionTarget,
                center, targets, target =>
            {
                if (!DirectAttack.CasterCanContinue(Caster, false) || target == null ||
                    !target.isActive || !contexts.TryGetValue(target, out DamageContext context))
                    return;
                if (!sfxPlayed)
                {
                    sfxPlayed = true;
                    AudioManager.PlayAction(Caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit, CodeName);
                }
                target.TakeDamage(context);
            });

            if (tier == 9 && DirectAttack.CasterCanContinue(Caster, false))
                Caster.TryConsumeCombatResource(ResourceId, MaximumStacks);
        }

        /// <summary>
        /// 구 3중첩 위력 20/31/68을 50 위력=공격력 100% 이관 규칙으로 환산한다.
        /// 밸런스 확정 전까지 문서와 함께 🔸 임시 수치로 유지한다.
        /// </summary>
        private static float ThreeStackRatio(int level)
        {
            int offset = Mathf.Max(0, level - 1);
            float power = 20f + Mathf.Round(-0.031f * offset + 0.00521f * offset * offset);
            return Mathf.Max(1f, power) / 50f;
        }

        private List<Unit> AliveEnemies()
        {
            if (GridManager.Instance == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? GridManager.Instance.heroList : GridManager.Instance.enemyList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField &&
                                      !unit.IsUntargetable).ToList();
        }

        private static Vector3 FormationCenter(IReadOnlyList<Unit> units, Unit fallback)
        {
            if (units == null || units.Count == 0) return VfxSpace.WorldPoint(fallback);
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Unit unit in units)
            {
                if (unit == null) continue;
                sum += VfxSpace.WorldPoint(unit);
                count++;
            }
            return count > 0 ? sum / count : VfxSpace.WorldPoint(fallback);
        }

        public override bool HasValidTarget() => false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DebugSetStack(int value)
        {
            if (Caster == null) return;
            Caster.SetCombatResourceMaximum(ResourceId, MaximumStacks, true);
            Caster.AddCombatResource(ResourceId, Mathf.Clamp(value, 0, MaximumStacks));
            _registered = true;
        }
#endif
    }
}
