using System;
using BaseClasses;
using UnityEngine;

namespace Entities
{
    /// <summary>
    /// 유닛의 스탯 모델: 5대 기본 스탯(base/성장/강화) + 명시적 전투 스탯(atk/def)과
    /// 파생 스탯 계산(GetBase*/GetDerived*)을 담당한다.
    /// 장비/상태 보정과 성장 레벨은 소유자(Unit)를 통해 조회한다.
    /// </summary>
    [Serializable]
    public class UnitStats
    {
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

        // 명시적 전투 스탯 (STR 파생이 아님)
        [SerializeField] private int atkBase;
        [SerializeField] private int atkIncrementLvl;
        [SerializeField] private int defBase;
        [SerializeField] private int defIncrementLvl;

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
        public int AtkBase { get => atkBase; set => atkBase = value; }
        public int AtkIncrementLvl { get => atkIncrementLvl; set => atkIncrementLvl = value; }
        public int DefBase { get => defBase; set => defBase = value; }
        public int DefIncrementLvl { get => defIncrementLvl; set => defIncrementLvl = value; }
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
            int dataLukBase, int dataLukIncrementLvl, int dataLukIncrementUpgrade,
            int dataAtkBase, int dataAtkIncrementLvl,
            int dataDefBase, int dataDefIncrementLvl)
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

            atkBase = dataAtkBase;
            atkIncrementLvl = dataAtkIncrementLvl;
            defBase = dataDefBase;
            defIncrementLvl = dataDefIncrementLvl;
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

        /// <summary>성장 레벨: 적 = Level, 아군 = Level - 1</summary>
        public int GetStatGrowthLevel()
        {
            return _owner.IsEnemy ? Mathf.Max(0, _owner.Level) : Mathf.Max(0, _owner.Level - 1);
        }

        /// <summary>캐릭터 주/부 스탯 배율 (주 ×1.2, 부 ×1.1)</summary>
        private int ApplyCharacterStatBonus(BaseEnums.PrimaryStat stat, int rawValue)
        {
            float multiplier = 1f;
            if (IsCharacterStatTag(_owner.MainStat, stat))
            {
                multiplier += 0.2f;
            }
            if (IsCharacterStatTag(_owner.SubStat, stat))
            {
                multiplier += 0.1f;
            }

            return Mathf.Max(0, Mathf.RoundToInt(rawValue * multiplier));
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
            int raw = strBase + strIncrementLvl * GetStatGrowthLevel() + strIncrementUpgrade * strUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.STR) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.STR);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.STR, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.STR, raw));
        }

        public int GetBaseDex()
        {
            int raw = dexBase + dexIncrementLvl * GetStatGrowthLevel() + dexIncrementUpgrade * dexUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.DEX) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.DEX);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.DEX, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.DEX, raw));
        }

        public int GetBaseCon()
        {
            int raw = conBase + conIncrementLvl * GetStatGrowthLevel() + conIncrementUpgrade * conUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.CON) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.CON);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.CON, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.CON, raw));
        }

        public int GetBaseInt()
        {
            int raw = intBase + intIncrementLvl * GetStatGrowthLevel() + intIncrementUpgrade * intUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.INT) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.INT);
            return _owner.ApplyPrimaryStatMultipliers(BaseEnums.PrimaryStat.INT, ApplyCharacterStatBonus(BaseEnums.PrimaryStat.INT, raw));
        }

        public int GetBaseLuk()
        {
            int raw = lukBase + lukIncrementLvl * GetStatGrowthLevel() + lukIncrementUpgrade * lukUpgrade
                + _owner.GetEquipmentStatBonus(BaseEnums.PrimaryStat.LUK) + _owner.GetStatusPrimaryStatBonus(BaseEnums.PrimaryStat.LUK);
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

        // ===== 파생 스탯 =====

        public int GetDerivedHp() => Mathf.Max(1, GetBaseCon() * 1000);

        public int GetDerivedMana() => 100;

        public int GetDerivedAtk() => Mathf.Max(1, atkBase + atkIncrementLvl * GetStatGrowthLevel());

        public int GetDerivedDef() => Mathf.Max(0, defBase + defIncrementLvl * GetStatGrowthLevel());

        public float GetDerivedCritChance() => Mathf.Clamp01(GetBaseLuk() * 0.01f);

        public float GetDerivedCritDamage() => 1.5f;

        public float GetDerivedCodeAcceleration() => 1f;

        public float GetDerivedAttackSpeed() => 1f + Mathf.Max(0, GetBaseDex()) * 0.01f;

        public float GetDerivedEvasionChance() => 0f;

        public float GetDerivedHealingBonus() => Mathf.Clamp(Mathf.Max(0, GetBaseCon() - 10) * 0.01f, 0f, 2f);

        public float GetDerivedShieldBonus() => Mathf.Clamp(Mathf.Max(0, GetBaseCon() - 10) * 0.01f, 0f, 2f);

        public float GetDerivedManaEfficiency() => 1f + Mathf.Max(0, GetBaseInt()) * 0.02f;

        public float GetDerivedCodeActivationChance() => 1f;
    }
}
