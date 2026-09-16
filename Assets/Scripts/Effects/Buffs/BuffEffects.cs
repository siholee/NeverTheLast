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

    /// <summary>수치 효과 없이 상태를 화면에 표시하기 위한 표식용 효과.</summary>
    public class MarkerBuffEffect : BaseEffect
    {
        public override bool CountsAsReagentBuff => false;
        public MarkerBuffEffect() : base(0) { }
    }

    /// <summary>치명타 확률 가산 버프 (홀리 인챈트 등)</summary>
    public class CritChanceBuffEffect : BaseEffect
    {
        private readonly float _amount;
        public override bool IsBeneficial => true;

        public CritChanceBuffEffect(float amount) : base(0, amount)
        {
            _amount = amount;
        }

        public override float CritChanceAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
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

        public override float CodeAccelerationAdditiveModifier(Unit unit) => unit == Target ? _amount : 0f;
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





}
