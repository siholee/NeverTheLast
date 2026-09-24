using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 공허의 파도 N(503) — 필드 위 <b>모든</b> 유닛에게 물 원소를 부착한다. 피해는 없다.
    ///
    /// 적과 아군을 가리지 않는다. 스킬라의 혼돈의 소용돌이가 전기를 얹는 순간 감전이 터지도록
    /// 판을 미리 적셔 두는 것이 이 개체의 유일한 일이다. 스킬라가 제어당해도 파도는 계속 친다.
    /// </summary>
    public sealed class VoidWaveNormal : BaseNormalCode
    {
        public VoidWaveNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "공허의 파도";
            Power = 0;
            CastingDelay = 0.3f;
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast) { StopCode(); yield break; }

            foreach (Unit unit in FieldUnits())
                unit.GrantCombatElement(BaseEnums.UnitElement.Hydro, Unit.CommonElementAuraDuration, Caster);

            NotifyActionResolved();
            StopCode();
        }

        /// <summary>진영을 가리지 않고 필드에 선 유닛을 모은다.</summary>
        private static List<Unit> FieldUnits()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return new List<Unit>();

            return grid.heroList.Concat(grid.enemyList)
                .Where(unit => unit != null && unit.isActive && unit.IsOnField)
                .ToList();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>
    /// 스킬라 N(1920) — 여왕의 광선. 적 전체에게 100 + INT×2.5.
    ///
    /// <b>외신 동조</b>(2브레이크)가 열려 있으면 광선 뒤에 추가행동 '모든 것을 먼지로'가
    /// 따라붙는다 — 무작위 단일 대상을 여섯 번 때리므로, 뭉쳐 있든 흩어져 있든 아프다.
    /// </summary>
    public sealed class ScyllaQueensRay : BaseNormalCode
    {
        private const int DustShots = 6;
        private const int DustPower = 50;
        private const float DustIntCoefficient = 1.2f;

        public ScyllaQueensRay(NormalCodeContext context) : base(context)
        {
            CodeName = "여왕의 광선";
            Power = 100;
            PowerStat = BaseEnums.PrimaryStat.INT;
            PowerStatCoefficient = 2.5f;
            CastingDelay = 0.5f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.AllTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override List<Unit> SelectTarget() => GetAvailableEnemies();

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            base.OnAttackResolved(target, context);
            if (Caster == null || !Caster.isActive) return;
            if (!ScyllaCombat.HasOuterAttunement(Caster)) return;

            // 광역기라 대상 수만큼 불린다. 중복 억제 키가 하나라 예약은 한 번만 걸린다.
            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "scylla_all_to_dust", "모든 것을 먼지로", ResolveAllToDust);
        }

        /// <summary>모든 것을 먼지로 — 무작위 단일 대상을 여섯 번 때린다.</summary>
        private void ResolveAllToDust()
        {
            if (Caster == null || !Caster.isActive) return;

            int power = DustPower + Mathf.RoundToInt(Caster.GetBaseInt() * DustIntCoefficient);
            for (int shot = 0; shot < DustShots; shot++)
            {
                List<Unit> enemies = CombatTargets.AliveEnemies(Caster);
                if (enemies.Count == 0) return;

                Unit target = enemies[Random.Range(0, enemies.Count)];
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(power, BaseEnums.PrimaryStat.INT) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Passive,
                    new List<int>
                    {
                        DamageTag.SingleTarget, DamageTag.AdditionalAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
            }
        }
    }
}
