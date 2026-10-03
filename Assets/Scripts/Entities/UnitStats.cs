using System;
using BaseClasses;
using Managers;
using UnityEngine;

namespace Entities
{
    /// <summary>
    /// 유닛의 스탯 모델 — 여덟 스탯의 기초값 · 성장치와, 레벨을 반영한 <b>기초 스탯</b> 계산.
    ///
    /// 2026-09 개편으로 5스탯 · 훈련 강화 · 주/부 스탯 배율을 모두 걷어냈다. 지금은 롤과 같다.
    ///
    ///   기초 스탯 = 기초값 + 성장치 × (레벨 − 1)
    ///   최종 스탯 = 기초 스탯 × (1 + 퍼센트 보정 합) + 고정 보정 합       (체력 · 공격 · 방어 계열 · 속도)
    ///   최종 스탯 = 기초 스탯 + 고정 보정 합                             (치명타 확률 · 치명타 피해)
    ///
    /// 상태 보정은 <see cref="Unit"/>이 모아 넘긴다. 여기에는 데이터와 레벨만 있다.
    /// 공식의 원본은 <c>Detail_03 §2</c>다.
    /// </summary>
    [Serializable]
    public class UnitStats
    {
        public const int StatCount = 8;

        /// <summary>
        /// 방어 경감 기준값의 1레벨 값. 받는 피해 배율 = 기준값 / (기준값 + 방어력).
        /// 기준값은 <b>공격자</b>의 레벨로 정한다(원신식) — <see cref="ArmorConstant"/>.
        /// </summary>
        public const float ArmorConstantBase = 100f;

        /// <summary>
        /// 레벨 1당 기준값 증가분.
        ///
        /// 레벨에 끝이 없고(무한 모드) 스탯이 선형으로 자라므로, 기준값을 100으로 묶어 두면
        /// 방어력만 계속 커져 경감률이 100%로 수렴한다 — 받는 피해는 멈추는데 체력은 계속 늘어
        /// 고레벨 전투가 끝나지 않는다. 기준값도 방어력 성장과 같은 기울기로 키워
        /// <b>같은 레벨끼리의 경감률이 레벨과 무관하게 일정</b>하도록 만든다.
        /// 표준 성장(방어력 +4~6/Lv)에 맞춰 6을 준다.
        /// </summary>
        public const float ArmorConstantPerLevel = 6f;

        private Unit _owner;

        // 레벨 1의 값과 레벨당 성장치. 인덱스는 BaseEnums.StatType 순서다.
        [SerializeField] private float[] baseValues = new float[StatCount];
        [SerializeField] private float[] growthValues = new float[StatCount];

        public void Initialize(Unit owner)
        {
            _owner = owner;
            EnsureArrays();
        }

        /// <summary>
        /// 프리팹에 저장된 옛 직렬화 데이터(5스탯 시절)에는 이 배열이 없다. 유니티가 빈 배열로 되살리면
        /// 인덱스 접근이 터지므로 길이를 맞춰 다시 만든다.
        /// </summary>
        private void EnsureArrays()
        {
            if (baseValues == null || baseValues.Length != StatCount) baseValues = new float[StatCount];
            if (growthValues == null || growthValues.Length != StatCount) growthValues = new float[StatCount];
        }

        /// <summary>YAML 데이터에서 여덟 스탯의 기초값과 성장치를 읽는다.</summary>
        public void Load(CombatantData data)
        {
            if (data == null) return;
            EnsureArrays();

            for (int i = 0; i < StatCount; i++)
            {
                var stat = (BaseEnums.StatType)i;
                baseValues[i] = data.BaseOf(stat);
                growthValues[i] = data.GrowthOf(stat);
            }

            if (baseValues[(int)BaseEnums.StatType.MaxHp] <= 0f)
                Debug.LogError($"[UnitStats] {_owner?.UnitName}(ID {_owner?.ID}): 최대 체력 기초값이 0입니다. YAML 데이터를 확인하세요.");
        }

        /// <summary>데이터 파일 없이 값을 직접 넣는다. 테스트용 유닛이 쓴다.</summary>
        public void Set(BaseEnums.StatType stat, float baseValue, float growth = 0f)
        {
            EnsureArrays();
            baseValues[(int)stat] = baseValue;
            growthValues[(int)stat] = growth;
        }

        public float BaseValue(BaseEnums.StatType stat)
        {
            EnsureArrays();
            return baseValues[(int)stat];
        }

        public float Growth(BaseEnums.StatType stat)
        {
            EnsureArrays();
            return growthValues[(int)stat];
        }

        /// <summary>
        /// 레벨을 반영한 기초 스탯. 상태 보정 전이다.
        /// Level 1이 성장분 0이다. 적의 Level은 스테이지가, 아군의 Level은 누적 EXP가 정한다.
        /// </summary>
        public float LeveledBase(BaseEnums.StatType stat)
        {
            EnsureArrays();
            int growthLevels = Mathf.Max(0, (_owner != null ? _owner.Level : 1) - 1);
            return baseValues[(int)stat] + growthValues[(int)stat] * growthLevels;
        }

        /// <summary>퍼센트 보정을 받는 스탯인가. 치명타 두 스탯은 이미 비율이라 가산만 받는다.</summary>
        public static bool IsPercentScalable(BaseEnums.StatType stat)
            => stat != BaseEnums.StatType.CritRate && stat != BaseEnums.StatType.CritDamage;

        /// <summary>
        /// 이 레벨의 공격자가 쓰는 방어 경감 기준값.
        /// 방어력 · 특수방어력 경감에 모두 쓴다.
        /// </summary>
        public static float ArmorConstant(int attackerLevel)
            => ArmorConstantBase + ArmorConstantPerLevel * Mathf.Max(0, attackerLevel - 1);

        /// <summary>
        /// 받는 피해 배율 = 기준값 / (기준값 + 방어력). 방어력이 음수면 대칭적으로 피해가 증폭된다.
        /// </summary>
        public static float MitigationMultiplier(float defense, float armorConstant)
        {
            armorConstant = Mathf.Max(1f, armorConstant);
            return defense >= 0f
                ? armorConstant / (armorConstant + defense)
                : 2f - armorConstant / (armorConstant - defense);
        }

        /// <summary>UI 표기용 이름.</summary>
        public static string DisplayName(BaseEnums.StatType stat) => stat switch
        {
            BaseEnums.StatType.MaxHp => "최대 체력",
            BaseEnums.StatType.Attack => "공격력",
            BaseEnums.StatType.SpecialAttack => "특수공격력",
            BaseEnums.StatType.Defense => "방어력",
            BaseEnums.StatType.SpecialDefense => "특수방어력",
            BaseEnums.StatType.Speed => "속도",
            BaseEnums.StatType.CritRate => "치명타 확률",
            BaseEnums.StatType.CritDamage => "치명타 피해",
            _ => stat.ToString(),
        };

        /// <summary>짧은 표기(카드 · 표 머리). 두 글자 안쪽으로 맞춘다.</summary>
        public static string ShortName(BaseEnums.StatType stat) => stat switch
        {
            BaseEnums.StatType.MaxHp => "체력",
            BaseEnums.StatType.Attack => "공격",
            BaseEnums.StatType.SpecialAttack => "특공",
            BaseEnums.StatType.Defense => "방어",
            BaseEnums.StatType.SpecialDefense => "특방",
            BaseEnums.StatType.Speed => "속도",
            BaseEnums.StatType.CritRate => "치확",
            BaseEnums.StatType.CritDamage => "치피",
            _ => stat.ToString(),
        };

        /// <summary>값을 사람이 읽는 형태로. 치명타 두 스탯은 퍼센트로 적는다.</summary>
        public static string Format(BaseEnums.StatType stat, float value)
            => IsPercentScalable(stat) ? Mathf.RoundToInt(value).ToString() : $"{value * 100f:0.#}%";
    }
}
