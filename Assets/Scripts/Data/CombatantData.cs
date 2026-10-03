using System;
using System.Collections.Generic;
using BaseClasses;

namespace Managers
{
    /// <summary>
    /// 아군(<see cref="UnitData"/>)과 적(<see cref="EnemyData"/>)이 함께 쓰는 전투 정의.
    ///
    /// 스탯은 롤처럼 <b>기초값(Base)</b>과 <b>레벨당 성장치(Growth)</b> 한 쌍이다.
    /// 치명타 두 스탯만 퍼센트 포인트로 적는다(5 = 5%). 적지 않으면 원신과 같은 기본값
    /// (치명타 확률 5%, 치명타 피해 50%, 속도 100)을 쓴다.
    /// </summary>
    [Serializable]
    public class CombatantData
    {
        public int id;
        public string name;
        public string element;
        /// <summary>진영 · 종족처럼 판정에 쓰는 데이터 태그.</summary>
        public List<string> tags;

        public float hpBase;
        public float hpGrowth;
        public float atkBase;
        public float atkGrowth;
        public float spAtkBase;
        public float spAtkGrowth;
        public float defBase;
        public float defGrowth;
        public float spDefBase;
        public float spDefGrowth;
        public float speedBase = 100f;
        public float speedGrowth;
        public float critRateBase = 5f;
        public float critRateGrowth;
        public float critDamageBase = 50f;
        public float critDamageGrowth;

        /// <summary>
        /// 최대 마나. <b>유닛 고유 스탯</b>이라 레벨 · 버프가 관여하지 않는다.
        /// 궁극기 주기를 유닛별로 조이는 손잡이다. 비워 두면 100이다.
        /// </summary>
        public int manaMax;
        /// <summary>
        /// 궁극기 자원 방식. 기본은 Mana이며 Stack은 캐릭터 코드가 명시적으로만 올린다.
        /// 저장 DTO는 두 방식 모두 기존 ultimateResource 정수를 공유한다.
        /// </summary>
        public string ultimateResourceType = "Mana";
        /// <summary>카드 자원 배지와 문서에서 쓰는 고유 자원 이름.</summary>
        public string ultimateResourceName = "마나";
        public Dictionary<string, int> codes;
        public string portrait;
        public string standing;
        /// <summary>원소색과 별개로 초상화에서 고른 전투 연출 대표색(#RRGGBB). 비어 있으면 원소/공허 기본색을 쓴다.</summary>
        public string vfxColor;
        /// <summary>플레이어에게 노출하지 않는 VFX 기본 시전 분류.</summary>
        public string vfxClass;

        public float BaseOf(BaseEnums.StatType stat) => stat switch
        {
            BaseEnums.StatType.MaxHp => hpBase,
            BaseEnums.StatType.Attack => atkBase,
            BaseEnums.StatType.SpecialAttack => spAtkBase,
            BaseEnums.StatType.Defense => defBase,
            BaseEnums.StatType.SpecialDefense => spDefBase,
            BaseEnums.StatType.Speed => speedBase,
            BaseEnums.StatType.CritRate => critRateBase * 0.01f,
            BaseEnums.StatType.CritDamage => critDamageBase * 0.01f,
            _ => 0f,
        };

        public float GrowthOf(BaseEnums.StatType stat) => stat switch
        {
            BaseEnums.StatType.MaxHp => hpGrowth,
            BaseEnums.StatType.Attack => atkGrowth,
            BaseEnums.StatType.SpecialAttack => spAtkGrowth,
            BaseEnums.StatType.Defense => defGrowth,
            BaseEnums.StatType.SpecialDefense => spDefGrowth,
            BaseEnums.StatType.Speed => speedGrowth,
            BaseEnums.StatType.CritRate => critRateGrowth * 0.01f,
            BaseEnums.StatType.CritDamage => critDamageGrowth * 0.01f,
            _ => 0f,
        };

        public int CodeId(string slot) => codes != null && codes.TryGetValue(slot, out int value) ? value : 0;
    }
}
