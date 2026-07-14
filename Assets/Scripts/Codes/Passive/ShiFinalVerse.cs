using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using StatusEffects.Effects;
using UnityEngine;

namespace Codes.Passive
{
    public class ShiFinalVerse : PassiveCode
    {
        private int _verseStack;
        private bool _isRegistered;
        private Action<EventContext> _normalAttackHitHandler;

        public ShiFinalVerse(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "시의 종언";
            Caster = context.Caster;
            MaxStage = 3;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            _verseStack = 0;
            if (_isRegistered) return;

            _normalAttackHitHandler = OnNormalAttackHit;
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalAttackHitHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered || _normalAttackHitHandler == null) return;

            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalAttackHitHandler);
            _normalAttackHitHandler = null;
            _isRegistered = false;
        }

        public override bool HasValidTarget()
        {
            return Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive);
        }

        private void OnNormalAttackHit(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            int stage = Mathf.Clamp(CurrentStage, 1, MaxStage);
            int maxStack = stage * 3;
            int stackGain = Caster.HasStatusEffect(FinalBellEffect.StatusIdentifier) ? 3 : 1;
            int previousStack = Mathf.Min(_verseStack, maxStack);
            _verseStack = Mathf.Min(previousStack + stackGain, maxStack);

            if (previousStack < 3 && _verseStack >= 3)
            {
                DealLowestDefenseDamage(20);
            }
            else if (stage >= 2 && previousStack < 6 && _verseStack >= 6)
            {
                DealSplitDamage(50);
            }
            else if (stage >= 3 && previousStack < 9 && _verseStack >= 9)
            {
                DealSplitDamage(75);
            }

            if (_verseStack >= maxStack)
            {
                _verseStack = 0;
            }
        }

        private void DealLowestDefenseDamage(int dexMultiplier)
        {
            Unit target = GetAvailableEnemies()
                .OrderBy(unit => unit.DefCurr)
                .ThenBy(unit => unit.HpCurr)
                .FirstOrDefault();
            if (target == null) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseDex() * dexMultiplier * critMultiplier));
            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.ContactAttack,
                DamageTag.Slash,
            };
            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, tags, isCrit));
        }

        private void DealSplitDamage(int dexMultiplier)
        {
            List<Unit> targets = GetAvailableEnemies();
            if (targets.Count == 0) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int totalDamage = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseDex() * dexMultiplier * critMultiplier));
            int splitDamage = Mathf.Max(1, totalDamage / targets.Count);
            var tags = new List<int>
            {
                DamageTag.AllTarget,
                DamageTag.SplitDamage,
                DamageTag.ContactAttack,
                DamageTag.Slash,
            };

            foreach (Unit target in targets)
            {
                target.TakeDamage(new DamageContext(Caster, splitDamage, BaseEnums.CodeType.Passive, tags, isCrit));
            }
        }

        private List<Unit> GetAvailableEnemies()
        {
            return Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && unit.currentCell != null && unit.currentCell.isOccupied)
                .ToList();
        }
    }
}
