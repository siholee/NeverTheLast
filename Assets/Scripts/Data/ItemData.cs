using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class ItemData
    {
        public int id;
        public string name;
        public string slot;
        public string category;
        public string requiredProficiency;
        public int rarity;
        public int weight = 1;
        /// <summary>
        /// 내구 — 받는 피해에서 **고정으로 깎아내는 양**.
        /// 방어력(비율 감소)과 달리 방어 무시·관통의 영향을 받지 않는다.
        /// 방어구에만 붙이는 것을 원칙으로 한다.
        /// </summary>
        public int durability;
        public bool eventOnly;
        /// <summary>
        /// 테마 전용 보상. 비어 있으면 모든 테마의 보상 풀에 등장한다.
        /// 값은 <c>80_stages.yaml</c>의 <c>stageThemes[].id</c>다(적 themeId가 아니다).
        /// </summary>
        public List<int> themeIds;
        public bool twoHanded;
        public List<BaseClasses.EquipmentStatBonus> statBonuses;
        public List<BaseClasses.EquipmentCodeGrant> codeGrants;

        public bool IsWeapon => string.Equals(slot, "MainHand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slot, "OffHand", StringComparison.OrdinalIgnoreCase);

        public BaseClasses.EquipmentProficiency RequiredProficiency
        {
            get
            {
                if (string.IsNullOrWhiteSpace(requiredProficiency))
                {
                    return BaseClasses.EquipmentProficiency.None;
                }

                return Enum.TryParse(requiredProficiency, true, out BaseClasses.EquipmentProficiency proficiency)
                    ? proficiency
                    : BaseClasses.EquipmentProficiency.None;
            }
        }
    }

    [Serializable]
    public class ItemDataList
    {
        public List<ItemData> items;
    }
}
