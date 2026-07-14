using BaseClasses;
using Entities;
using Managers;
using StatusEffects.Base;
using UnityEngine;

namespace StatusEffects.Effects
{
    public class PrimaryStatBonusEffect : StatusEffect, ITemporalEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amount;

        public PrimaryStatBonusEffect(Unit grantor, string identifier, BaseEnums.PrimaryStat stat, int amount, float duration = 0f)
            : base(grantor, identifier)
        {
            _stat = stat;
            _amount = amount;
            Duration = duration;
            Stack = 1;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            return stat == _stat ? _amount : 0;
        }

        public void OnUpdate(EventContext context)
        {
            if (Duration <= 0f) return;

            Duration -= context.FloatParam;
            if (Duration <= 0f && context.Grantee != null)
            {
                context.Grantee.RemoveStatusEffect(Identifier);
            }
        }

        public void UpdateDuration(float duration)
        {
            Duration += duration;
        }

        public int IsTriggered(float deltaTime)
        {
            return 0;
        }
    }

    public class ChandraNishakaraEffect : StatusEffect
    {
        private readonly float _intToConRatio;

        public ChandraNishakaraEffect(Unit grantor, float intToConRatio)
            : base(grantor, "ChandraNishakara")
        {
            _intToConRatio = intToConRatio;
            Stack = 1;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit == null || unit != Grantor || stat != BaseEnums.PrimaryStat.CON)
            {
                return 0;
            }

            return Mathf.RoundToInt(unit.GetBaseInt() * _intToConRatio);
        }
    }

    public class ShieldBonusEffect : StatusEffect
    {
        private readonly float _bonus;

        public ShieldBonusEffect(Unit grantor, string identifier, float bonus)
            : base(grantor, identifier)
        {
            _bonus = bonus;
            Stack = 1;
        }

        public override float ShieldBonusAdditiveModifier(Unit unit)
        {
            return unit == Grantor ? _bonus : 0f;
        }
    }

    public class CodeAccelerationEffect : StatusEffect, ITemporalEffect
    {
        private readonly float _amount;

        public override bool IsBeneficial => true;

        public CodeAccelerationEffect(Unit grantor, string identifier, float amount, float duration)
            : base(grantor, identifier)
        {
            _amount = amount;
            Duration = duration;
            Stack = 1;
        }

        public override float CodeAccelerationAdditiveModifier(Unit unit)
        {
            return _amount;
        }

        public void OnUpdate(EventContext context)
        {
            Duration -= context.FloatParam;
            if (Duration <= 0f && context.Grantee != null)
            {
                context.Grantee.RemoveStatusEffect(Identifier);
            }
        }

        public void UpdateDuration(float duration)
        {
            Duration += duration;
        }

        public int IsTriggered(float deltaTime)
        {
            return 0;
        }
    }

    public class LokapalaEffect : StatusEffect
    {
        public const int CodeId = 43;

        public LokapalaEffect(Unit grantor) : base(grantor, "Lokapala")
        {
            Stack = 1;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit == null || unit != Grantor || stat != BaseEnums.PrimaryStat.INT)
            {
                return 0;
            }

            int holders = 0;
            foreach (Unit ally in Target.GetAllAllies(unit))
            {
                if (ally == null || !ally.isActive) continue;
                foreach (var record in ally.LearnedPassiveRecords)
                {
                    if (record != null && record.codeId == CodeId)
                    {
                        holders++;
                        break;
                    }
                }
            }

            return holders * 3;
        }
    }

    public class AnemoImbueEffect : StatusEffect, ITemporalEffect
    {
        public const string StatusPrefix = "AnemoImbue";

        public AnemoImbueEffect(Unit grantor, string identifier, float duration)
            : base(grantor, identifier)
        {
            Duration = duration;
            Stack = 1;
        }

        public void OnUpdate(EventContext context)
        {
            Duration -= context.FloatParam;
            if (Duration <= 0f && context.Grantee != null)
            {
                context.Grantee.RemoveStatusEffect(Identifier);
            }
        }

        public void UpdateDuration(float duration)
        {
            Duration += duration;
        }

        public int IsTriggered(float deltaTime)
        {
            return 0;
        }
    }

    public class ForestGraceEffect : StatusEffect
    {
        public ForestGraceEffect(Unit grantor) : base(grantor, "QuetzalcoatlForestGrace") { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Grantor || !unit.HasCombatElement(BaseEnums.UnitElement.Dendro)) return 1f;
            return stat == BaseEnums.PrimaryStat.CON || stat == BaseEnums.PrimaryStat.INT ? 1.5f : 1f;
        }
    }

    public class ScholarEffect : StatusEffect
    {
        public ScholarEffect(Unit grantor) : base(grantor, "QuetzalcoatlScholar") { }
        public override float ManaRecoveryMultiplierModifier(Unit unit) => unit == Grantor ? 1.5f : 1f;
    }

    public class SpellSniperEffect : StatusEffect
    {
        public SpellSniperEffect(Unit grantor) : base(grantor, "QuetzalcoatlSpellSniper") { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Grantor || attacker.currentCell == null || target?.currentCell == null || GridManager.Instance == null)
            {
                return 1f;
            }
            int attackerRear = GridManager.Instance.GetRearColumn(attacker.IsEnemy);
            int targetRear = GridManager.Instance.GetRearColumn(target.IsEnemy);
            return attacker.currentCell.xPos == attackerRear && target.currentCell.xPos == targetRear ? 1.5f : 1f;
        }
    }

    public class GuardianWillEffect : StatusEffect
    {
        public GuardianWillEffect(Unit grantor) : base(grantor, "QuetzalcoatlGuardianWill") { }

        public override float ReceivingDamageModifier(Unit unit)
        {
            if (unit != Grantor || GridManager.Instance == null) return 1f;
            foreach (Unit ally in Target.GetAllAllies(unit))
            {
                if (ally != null && ally.isActive && (ally.ID == 1010 || ally.ID == 1011)) return 0.7f;
            }
            return 1f;
        }
    }

    public class CipactliSlayerEffect : StatusEffect
    {
        public CipactliSlayerEffect(Unit grantor, string identifier) : base(grantor, identifier) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            return Grantor != null && Grantor.isActive && unit != null && unit.HasCombatElement(BaseEnums.UnitElement.Dendro)
                ? 2f
                : 1f;
        }
    }
}
