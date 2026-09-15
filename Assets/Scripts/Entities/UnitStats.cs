using System;
using System.Linq;
using BaseClasses;
using UnityEngine;

namespace Entities
{
    /// <summary>
    /// 유닛의 스탯 모델: 5대 기본 스탯(base/성장/강화)과 파생 스탯 계산(GetBase*/GetDerived*)을 담당한다.
    /// 장비/상태 보정과 성장 레벨은 소유자(Unit)를 통해 조회한다.
    ///
    /// 명시적 공격력/방어력 스탯은 존재하지 않는다. 모든 전투 수치는 5스탯에서 파생된다.
    ///   피해     = 스킬 위력 × 주스탯 × <see cref="SkillPowerScale"/>   (포켓몬식)
    ///   최대체력 = CON × <see cref="HpPerConPoint"/>
    ///   방어력   = STR × <see cref="DefensePerStrPoint"/>              (롤 방식 감쇠)
    /// </summary>
    [Serializable]
    public class UnitStats
    {
        /// <summary>
        /// 스킬 위력 1 × 주스탯 1이 만드는 피해량.
        /// 전투 화력 전체를 조절하는 단일 손잡이다.
        /// </summary>
        public const float SkillPowerScale = 0.2f;

        /// <summary>CON 1점이 만드는 최대 체력.</summary>
        public const int HpPerConPoint = 100;

        /// <summary>STR 1점이 만드는 방어력.</summary>
        public const float DefensePerStrPoint = 1f;

        /// <summary>
        /// 방어력 감쇠 기준값의 1레벨 값. 리그 오브 레전드의 100에 해당한다.
        /// 받는 피해 배율 = 기준값 / (기준값 + 방어력).
        /// </summary>
        public const float ArmorConstantBase = 100f;

        /// <summary>
        /// 레벨 1당 기준값 증가분.
        ///
        /// 롤과 달리 이 게임은 모든 스탯이 레벨에 선형 비례한다. 기준값을 100으로 고정하면
        /// 방어력만 계속 커져 후반에 피해가 무한히 상쇄된다. 기준값을 함께 키워
        /// **감소율이 레벨과 무관하게 일정**하도록 만든다.
        /// </summary>
        public const float ArmorConstantPerLevel = 10f;

        private Unit _owner;

        // 기본 스탯 수치
        [SerializeField] private int strBase;
        [SerializeField] private int strIncrementLvl;
        [SerializeField] private int strIncrementUpgrade;
        [SerializeField] private int dexBase;
        [SerializeField] private int dexIncrementLvl;
        [SerializeField] private int dexIncrementUpgrade;
        [SerializeField] private int conBase;
        [SerializeField] private int conIncrementLvl;
        [SerializeField] private int conIncrementUpgrade;
        [SerializeField] private int intBase;
        [SerializeField] private int intIncrementLvl;
        [SerializeField] private int intIncrementUpgrade;
        [SerializeField] private int lukBase;
        [SerializeField] private int lukIncrementLvl;
        [SerializeField] private int lukIncrementUpgrade;

        // 강화 횟수 (육성/보상)
        [SerializeField] private int strUpgrade;
        [SerializeField] private int dexUpgrade;
        [SerializeField] private int conUpgrade;
        [SerializeField] private int intUpgrade;
        [SerializeField] private int lukUpgrade;

        public int StrBase { get => strBase; set => strBase = value; }
        public int StrIncrementLvl { get => strIncrementLvl; set => strIncrementLvl = value; }
        public int StrIncrementUpgrade { get => strIncrementUpgrade; set => strIncrementUpgrade = value; }
        public int DexBase { get => dexBase; set => dexBase = value; }
        public int DexIncrementLvl { get => dexIncrementLvl; set => dexIncrementLvl = value; }
        public int DexIncrementUpgrade { get => dexIncrementUpgrade; set => dexIncrementUpgrade = value; }
        public int ConBase { get => conBase; set => conBase = value; }
        public int ConIncrementLvl { get => conIncrementLvl; set => conIncrementLvl = value; }
        public int ConIncrementUpgrade { get => conIncrementUpgrade; set => conIncrementUpgrade = value; }
        public int IntBase { get => intBase; set => intBase = value; }
        public int IntIncrementLvl { get => intIncrementLvl; set => intIncrementLvl = value; }
        public int IntIncrementUpgrade { get => intIncrementUpgrade; set => intIncrementUpgrade = value; }
        public int LukBase { get => lukBase; set => lukBase = value; }
        public int LukIncrementLvl { get => lukIncrementLvl; set => lukIncrementLvl = value; }
        public int LukIncrementUpgrade { get => lukIncrementUpgrade; set => lukIncrementUpgrade = value; }
        public int StrUpgrade { get => strUpgrade; set => strUpgrade = value; }
        public int DexUpgrade { get => dexUpgrade; set => dexUpgrade = value; }
        public int ConUpgrade { get => conUpgrade; set => conUpgrade = value; }
        public int IntUpgrade { get => intUpgrade; set => intUpgrade = value; }
        public int LukUpgrade { get => lukUpgrade; set => lukUpgrade = value; }

        public void Initialize(Unit owner)
        {
            _owner = owner;
        }

        /// <summary>YAML 데이터에서 스탯 로드</summary>
        public void Load(
            int dataStrBase, int dataStrIncrementLvl, int dataStrIncrementUpgrade,
            int dataDexBase, int dataDexIncrementLvl, int dataDexIncrementUpgrade,
            int dataConBase, int dataConIncrementLvl, int dataConIncrementUpgrade,
            int dataIntBase, int dataIntIncrementLvl, int dataIntIncrementUpgrade,
            int dataLukBase, int dataLukIncrementLvl, int dataLukIncrementUpgrade)
        {
            bool hasPrimaryStats = dataStrBase != 0 || dataDexBase != 0 || dataConBase != 0 || dataIntBase != 0 || dataLukBase != 0;
            if (!hasPrimaryStats)
            {
                Debug.LogError($"[UnitStats] {_owner?.UnitName}(ID {_owner?.ID}): 5대 기본 스탯이 모두 0입니다. YAML 데이터를 확인하세요.");
            }

            strBase = dataStrBase;
            strIncrementLvl = dataStrIncrementLvl;
            strIncrementUpgrade = dataStrIncrementUpgrade;
            dexBase = dataDexBase;
            dexIncrementLvl = dataDexIncrementLvl;
            dexIncrementUpgrade = dataDexIncrementUpgrade;
            conBase = dataConBase;
            conIncrementLvl = dataConIncrementLvl;
            conIncrementUpgrade = dataConIncrementUpgrade;
            intBase = dataIntBase;
            intIncrementLvl = dataIntIncrementLvl;
            intIncrementUpgrade = dataIntIncrementUpgrade;
            lukBase = dataLukBase;
            lukIncrementLvl = dataLukIncrementLvl;
            lukIncrementUpgrade = dataLukIncrementUpgrade;
        }

        /// <summary>지정한 5스탯의 강화 수치를 증가</summary>
        public void AddUpgrade(BaseEnums.PrimaryStat stat, int amount)
        {
            switch (stat)
            {
                case BaseEnums.PrimaryStat.STR: strUpgrade += amount; break;
                case BaseEnums.PrimaryStat.DEX: dexUpgrade += amount; break;
                case BaseEnums.PrimaryStat.CON: conUpgrade += amount; break;
                case BaseEnums.PrimaryStat.INT: intUpgrade += amount; break;
                case BaseEnums.PrimaryStat.LUK: lukUpgrade += amount; break;
            }
        }

        // ===== 성장/보정 =====

        /// <summary>
        /// 성장 레벨. 아군·적 모두 <c>Level − 1</c>로 통일한다.
        /// 즉 Level 1은 성장분 0이며, 스탯은 항상 <c>base + incrementLvl × 성장레벨</c>이다.
        /// 적의 Level은 현재 스테이지가, 아군의 Level은 누적 EXP가 결정한다.
        /// </summary>
        public int GetStatGrowthLevel()
        {
            return Mathf.Max(0, _owner.Level - 1);
        }

        /// <summary>캐릭터별 트레이닝 보너스. 주/부 스탯과 보너스율은 영웅 데이터가 결정한다.</summary>
        private int ApplyCharacterStatBonus(BaseEnums.PrimaryStat stat, int rawValue)
        {
            return Mathf.Max(0, Mathf.RoundToInt(rawValue * GetCharacterStatMultiplier(stat)));
        }

        internal float GetCharacterStatMultiplier(BaseEnums.PrimaryStat stat)
        {
            float multiplier = 1f;
            if (IsCharacterStatTag(_owner.MainStat, stat))
            {
                multiplier += _owner.MainStatTrainingBonus;
            }
            if (_owner.SubStats.Any(statName => IsCharacterStatTag(statName, stat)))
            {
                multiplier += _owner.SubStatTrainingBonus;
            }
            return multiplier;
        }

        private static bool IsCharacterStatTag(string statName, BaseEnums.PrimaryStat stat)
        {
            return !string.IsNullOrWhiteSpace(statName)
                && Enum.TryParse(statName, true, out BaseEnums.PrimaryStat parsed)
                && parsed == stat;
        }

        // ===== 5대 기본 스탯 최종값 =====

        public int GetBasePrimaryStat(BaseEnums.PrimaryStat stat)
        {
            return stat switch
            {
                BaseEnums.PrimaryStat.STR => GetBaseStr(),
                BaseEnums.PrimaryStat.DEX => GetBaseDex(),
                BaseEnums.PrimaryStat.CON => GetBaseCon(),
                BaseEnums.PrimaryStat.INT => GetBaseInt(),
                BaseEnums.PrimaryStat.LUK => GetBaseLuk(),
                _ => 0,
            };
        }

        public int GetBaseStr()
        {
            return _owner.ApplyEncumbranceStatMultiplier(BaseEnums.PrimaryStat.STR, GetUnburdenedBaseStr());
        }

        /// <summary>휴대 한도 산정용 STR. 중량 페널티만 제외하고 성장·장비·상태 배율은 반영한다.</summary>
        internal int GetUnburdenedBaseStr()
        {
            int raw = strBase + strIncrementLvl * GetStatGrowthLevel() + strIncrementUpgrade * strUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.STR) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.STR)
                + _owner.GetPartyTonicStatBonus(BaseEnums.PrimaryStat.STR);
            return _owner.ApplyStatusPrimaryStatMultipliers(BaseEnums.PrimaryStat.STR, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.STR, raw));
        }

        public int GetBaseDex()
        {
            return _owner.ApplyEncumbranceStatMultiplier(BaseEnums.PrimaryStat.DEX, GetUnburdenedBaseDex());
        }

        internal int GetUnburdenedBaseDex()
        {
            int raw = dexBase + dexIncrementLvl * GetStatGrowthLevel() + dexIncrementUpgrade * dexUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.DEX) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.DEX)
                + _owner.GetPartyTonicStatBonus(BaseEnums.PrimaryStat.DEX);
            return _owner.ApplyStatusPrimaryStatMultipliers(BaseEnums.PrimaryStat.DEX, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.DEX, raw));
        }

        public int GetBaseCon()
        {
            int raw = conBase + conIncrementLvl * GetStatGrowthLevel() + conIncrementUpgrade * conUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.CON) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.CON)
                + _owner.GetPartyTonicStatBonus(BaseEnums.PrimaryStat.CON);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.CON, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.CON, raw));
        }

        public int GetBaseInt()
        {
            int raw = intBase + intIncrementLvl * GetStatGrowthLevel() + intIncrementUpgrade * intUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.INT) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.INT)
                + _owner.GetPartyTonicStatBonus(BaseEnums.PrimaryStat.INT);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.INT, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.INT, raw));
        }

        public int GetBaseLuk()
        {
            int raw = lukBase + lukIncrementLvl * GetStatGrowthLevel() + lukIncrementUpgrade * lukUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.LUK) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.LUK)
                + _owner.GetPartyTonicStatBonus(BaseEnums.PrimaryStat.LUK);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.LUK, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.LUK, raw));
        }

        /// <summary>성장(레벨+강화)으로 얻은 스탯 증가량만 반환</summary>
        public int GetGrowthStatValue(BaseEnums.PrimaryStat stat)
        {
            int growthLevel = GetStatGrowthLevel();
            return stat switch
            {
                BaseEnums.PrimaryStat.STR => strIncrementLvl * growthLevel + strIncrementUpgrade * strUpgrade,
                BaseEnums.PrimaryStat.DEX => dexIncrementLvl * growthLevel + dexIncrementUpgrade * dexUpgrade,
                BaseEnums.PrimaryStat.CON => conIncrementLvl * growthLevel + conIncrementUpgrade * conUpgrade,
                BaseEnums.PrimaryStat.INT => intIncrementLvl * growthLevel + intIncrementUpgrade * intUpgrade,
                BaseEnums.PrimaryStat.LUK => lukIncrementLvl * growthLevel + lukIncrementUpgrade * lukUpgrade,
                _ => 0,
            };
        }

        /// <summary>강화 수치를 제외하고 레벨로만 얻은 성장 스탯을 반환한다.</summary>
        public int GetLevelGrowthStatValue(BaseEnums.PrimaryStat stat)
        {
            int growthLevel = GetStatGrowthLevel();
            return stat switch
            {
                BaseEnums.PrimaryStat.STR => strIncrementLvl * growthLevel,
                BaseEnums.PrimaryStat.DEX => dexIncrementLvl * growthLevel,
                BaseEnums.PrimaryStat.CON => conIncrementLvl * growthLevel,
                BaseEnums.PrimaryStat.INT => intIncrementLvl * growthLevel,
                BaseEnums.PrimaryStat.LUK => lukIncrementLvl * growthLevel,
                _ => 0,
            };
        }

        // ===== 파생 스탯 =====

        public int GetDerivedHp() => Mathf.Max(1, GetBaseCon() * HpPerConPoint);

        /// <summary>방어력. STR에서 파생된다.</summary>
        public int GetDerivedDef()
            => Mathf.Max(0, Mathf.RoundToInt(GetBaseStr() * DefensePerStrPoint));

        /// <summary>이 유닛의 레벨에 대응하는 방어력 감쇠 기준값.</summary>
        public float GetArmorConstant()
            => ArmorConstantBase + ArmorConstantPerLevel * Mathf.Max(0, _owner.Level - 1);

        /// <summary>
        /// 포켓몬식 피해 산출: 스킬이 가진 고정 위력에 시전자의 지정 스탯을 곱한다.
        /// 방어력이 없으므로 이 값이 곧 기본 피해량이며, 이후 치명타·피해 보정만 곱해진다.
        /// </summary>
        public int GetSkillDamage(int skillPower, BaseEnums.PrimaryStat stat)
            => Mathf.Max(1, Mathf.RoundToInt(
                Mathf.Max(0, skillPower) * Mathf.Max(0, GetBasePrimaryStat(stat)) * SkillPowerScale));

        // 최종 명중 확률은 Unit.AttributesUpdate에서 0~100%로 제한한다.
        // 여기서는 학자처럼 초과분을 사용하는 효과를 위해 원시 확률을 보존한다.
        public float GetDerivedCritChance() => Mathf.Max(0f, GetBaseLuk() * 0.01f);

        public float GetDerivedCritDamage() => 1.5f;

        public float GetDerivedCodeAcceleration() => 1f;

        public float GetDerivedActionSpeed() => 1f + Mathf.Max(0, GetBaseDex()) * 0.01f;

        public float GetDerivedEvasionChance() => 0f;

        public float GetDerivedHealingBonus() => Mathf.Clamp(Mathf.Max(0, GetBaseCon() - 10) * 0.01f, 0f, 2f);

        public float GetDerivedShieldBonus() => Mathf.Clamp(Mathf.Max(0, GetBaseCon() - 10) * 0.01f, 0f, 2f);

        public float GetDerivedManaEfficiency() => 1f + Mathf.Max(0, GetBaseInt()) * 0.02f;

        public float GetDerivedCodeActivationChance() => 1f;
    }
}
