using System;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>강인도는 피해 흡수가 아닌 처치 연계용 자원이다. 실제 사망 이벤트와 경제 보상은 발행하지 않는다.</summary>
    public static class Toughness
    {
        public static event Action<CombatDefeatContext> EnemyDefeated;
        public static event Action<CombatDefeatContext> Broken;

        public static Unit Provider()
            => GridManager.Instance?.heroList.FirstOrDefault(u => u != null && u.isActive && u.IsOnField &&
                u.ActivePassiveCodesResolved().OfType<OrpheusCheatedDeath>().Any(p => p.IsProviding));

        public static void Ensure(Unit enemy)
        {
            if (enemy == null || !enemy.IsEnemy || enemy.IsSummon || !enemy.isActive || !enemy.IsOnField) return;
            Unit source = Provider();
            if (source == null) { enemy.ClearToughness(); return; }
            if (enemy.ToughnessMax <= 0)
                enemy.GrantToughness(string.Equals(enemy.UnitTier, "boss", StringComparison.OrdinalIgnoreCase) ? 150 :
                    string.Equals(enemy.UnitTier, "elite", StringComparison.OrdinalIgnoreCase) ? 90 : 30, source);
        }

        public static bool IsAttack(DamageContext context)
            => context != null && context.Attacker != null && !context.IsDamageOverTime &&
               context.CodeType != BaseEnums.CodeType.Effect && !context.HasTag(DamageTag.ElementalReaction) &&
               !context.HasTag(DamageTag.BonusDamage) && !context.HasTag(DamageTag.PassiveAttack);

        public static float Efficiency(Unit attacker)
            => attacker == null ? 1f : 1f + Mathf.Max(0f, attacker.CombatEffects().Sum(e => e.ToughnessEfficiencyBonus(attacker)));

        public static int Estimate(Unit attacker, float baseReduction)
            => Mathf.Max(0, Mathf.RoundToInt(baseReduction * Efficiency(attacker)));

        public static float BaseReduction(DamageContext context)
        {
            if (context.ToughnessReduction >= 0f) return context.ToughnessReduction;
            bool area = context.HasTag(DamageTag.AllTarget) || context.HasTag(DamageTag.MultiTarget);
            if (context.HasTag(DamageTag.UltAttack)) return area ? 30f : 60f;
            if (context.HasTag(DamageTag.AdditionalAttack) || context.HasTag(DamageTag.CounterAttack) || context.HasTag(DamageTag.SummonAttack))
                return area ? 10f : 15f;
            return area ? 15f : 30f;
        }

        internal static void Publish(Unit target, Unit attacker, bool virtualDefeat)
        {
            var context = new CombatDefeatContext(attacker, target, virtualDefeat);
            if (attacker != null && attacker != target)
            {
                attacker.Invoke(BaseEnums.UnitEventType.OnKill, context);
                attacker.RecoverMana(Unit.ManaPerKill);
            }
            if (target.IsEnemy && !target.IsSummon) EnemyDefeated?.Invoke(context);
            if (virtualDefeat)
            {
                Effects.StatusPopup.ShowToughnessBreak(target);
                Broken?.Invoke(context);
            }
        }
    }
}
