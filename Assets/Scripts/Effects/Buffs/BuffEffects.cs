using BaseClasses;
using Effects.Base;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Effects.Buffs
{
    /// <summary>
    /// 코드에서 직접 정의하는 버프류 상태의 ID 대역 (100–199 = 유닛 고유 버프).
    /// 구 StatusEffect(dict) 시스템에서 이식된 상태들이다.
    /// </summary>
    public static class BuffStatusIds
    {
        public const int HolyEnchant = 10;
        public const int FinalBell = 11;
        public const int SomaSpeed = 12;
        public const int Nishakara = 100;
        public const int Bastion = 101;
        public const int BulwarkStr = 102;
        public const int LastStandFormation = 103;
        public const int IronWall = 104;
        public const int AnemoImbue = 106;
        public const int ForestGrace = 107;
        public const int SpellSniper = 108;
        public const int RootingCon = 109;
        public const int RootingInt = 110;
        public const int Scholar = 111;
        public const int Transcendence = 112;
        public const int GuardianWill = 113;
        public const int CipactliSlayer = 114;
    }

    /// <summary>
    /// 자주 쓰는 버프 상태 빌더. 상태 하나 + 효과 하나 조합을 만든다.
    /// </summary>
    public static class BuffStatus
    {
        /// <summary>
        /// 단일 효과 버프 상태 생성.
        /// <paramref name="duration"/>은 <b>턴</b> 수이며 0 이하면 무한(라운드 종료까지)이다.
        /// </summary>
        public static UnitStatus Create(
            int id, string key, string name, Unit caster, Unit owner, BaseEffect effect,
            int duration = -1,
            BaseEnums.StatusStackPolicy stackPolicy = BaseEnums.StatusStackPolicy.Replace,
            BaseEnums.StatusCategory category = BaseEnums.StatusCategory.Positive,
            bool isBeneficial = false,
            string description = "")
        {
            var status = new UnitStatus(new StatusDefinition
            {
                Id = id,
                Key = key,
                Name = name,
                Description = description,
                Category = category,
                StackPolicy = stackPolicy,
                Duration = duration,
                IsBeneficial = isBeneficial,
            }, caster, owner);
            if (effect != null)
            {
                status.AddEffect(effect);
            }
            return status;
        }
    }

    /// <summary>치명타 확률 가산 버프 (홀리 인챈트 등)</summary>
    /// <summary>수치 효과 없이 상태를 화면에 표시하기 위한 표식용 효과.</summary>
    public class MarkerBuffEffect : BaseEffect
    {
        public MarkerBuffEffect() : base(0) { }
    }

    public class CritChanceBuffEffect : BaseEffect
    {
        private readonly float _amount;
        public override bool IsBeneficial => true;

        public CritChanceBuffEffect(float amount) : base(0, amount)
        {
            _amount = amount;
        }

        public override float CritChanceAdditiveModifier(Unit unit) => _amount;
    }

    /// <summary>만종: 시전자 자신에게 DEX +Level×2 (상태 지속 동안)</summary>
    public class FinalBellBuffEffect : BaseEffect
    {
        public FinalBellBuffEffect() : base(0) { }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit == null || unit != Caster || stat != BaseEnums.PrimaryStat.DEX)
            {
                return 0;
            }

            return unit.Level * 2;
        }
    }

    /// <summary>코드 가속 가산 버프 (소마 등)</summary>
    public class CodeAccelerationBuffEffect : BaseEffect
    {
        private readonly float _amount;
        public override bool IsBeneficial => true;

        public CodeAccelerationBuffEffect(float amount) : base(0, amount)
        {
            _amount = amount;
        }

        public override float CodeAccelerationAdditiveModifier(Unit unit) => _amount;
    }

    /// <summary>5대 기본 스탯 고정 가산 버프 (보유자에게 적용)</summary>
    public class PrimaryStatBonusBuffEffect : BaseEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amount;

        public PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat stat, int amount) : base(0, amount)
        {
            _stat = stat;
            _amount = amount;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            return stat == _stat ? _amount : 0;
        }
    }

    /// <summary>니샤카라: 시전자 자신의 INT 일정 비율만큼 CON 가산</summary>
    public class NishakaraBuffEffect : BaseEffect
    {
        private readonly float _intToConRatio;

        public NishakaraBuffEffect(float intToConRatio) : base(0, intToConRatio)
        {
            _intToConRatio = intToConRatio;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit == null || unit != Caster || stat != BaseEnums.PrimaryStat.CON)
            {
                return 0;
            }

            return Mathf.RoundToInt(unit.GetBaseInt() * _intToConRatio);
        }
    }

    /// <summary>성채: 시전자 자신의 보호막 부여량 가산</summary>
    public class ShieldBonusBuffEffect : BaseEffect
    {
        private readonly float _bonus;

        public ShieldBonusBuffEffect(float bonus) : base(0, bonus)
        {
            _bonus = bonus;
        }

        public override float ShieldBonusAdditiveModifier(Unit unit)
        {
            return unit == Caster ? _bonus : 0f;
        }
    }

    /// <summary>숲의 은총: 시전자가 Dendro 원소 보유 시 CON/INT ×1.5</summary>
    public class ForestGraceBuffEffect : BaseEffect
    {
        public ForestGraceBuffEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Caster || !unit.HasCombatElement(BaseEnums.UnitElement.Dendro)) return 1f;
            return stat == BaseEnums.PrimaryStat.CON || stat == BaseEnums.PrimaryStat.INT ? 1.5f : 1f;
        }
    }

    /// <summary>학자: 시전자 자신의 마나 회복 ×1.5</summary>
    public class ScholarBuffEffect : BaseEffect
    {
        public ScholarBuffEffect() : base(0) { }

        public override float ManaRecoveryMultiplierModifier(Unit unit) => unit == Caster ? 1.5f : 1f;
    }

    /// <summary>주문 저격수: 후열 → 후열 공격 시 주는 피해 ×1.5</summary>
    public class SpellSniperBuffEffect : BaseEffect
    {
        public SpellSniperBuffEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Caster || attacker.currentCell == null || target?.currentCell == null || Managers.GridManager.Instance == null)
            {
                return 1f;
            }
            int attackerRear = Managers.GridManager.Instance.GetRearColumn(attacker.IsEnemy);
            int targetRear = Managers.GridManager.Instance.GetRearColumn(target.IsEnemy);
            return attacker.currentCell.xPos == attackerRear && target.currentCell.xPos == targetRear ? 1.5f : 1f;
        }
    }

    /// <summary>수호자의 의지: 아군에 1010/1011이 살아있으면 받는 피해 ×0.7</summary>
    public class GuardianWillBuffEffect : BaseEffect
    {
        public GuardianWillBuffEffect() : base(0) { }

        public override float ReceivingDamageModifier(Unit unit)
        {
            if (unit != Caster || Managers.GridManager.Instance == null) return 1f;
            foreach (Unit ally in global::Target.GetAllAllies(unit))
            {
                if (ally != null && ally.isActive && (ally.ID == 1010 || ally.ID == 1011)) return 0.7f;
            }
            return 1f;
        }
    }

    /// <summary>시팍틀리를 살해한 자: 시전자가 살아있는 동안 Dendro 보유 아군의 5스탯 ×2</summary>
    public class CipactliSlayerBuffEffect : BaseEffect
    {
        public CipactliSlayerBuffEffect() : base(0) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            return Caster != null && Caster.isActive && unit != null && unit.HasCombatElement(BaseEnums.UnitElement.Dendro)
                ? 2f
                : 1f;
        }
    }
}
