using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    public class ItemData
    {
        public int id;
        public string name;
        /// <summary>Resources/Sprite/Items 기준 아이콘 파일명(확장자 제외).</summary>
        public string icon;
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
        /// 적만 드는 장비. 보상·상점 어디에도 나오지 않는다.
        /// 초반 적의 무뎌진 무기처럼 적의 성능을 조절하려고 입히는 것이라 플레이어 손에 들어가면 안 된다.
        /// </summary>
        public bool enemyOnly;

        /// <summary>
        /// 캐릭터 연계 장비. 목록의 유닛 중 하나가 파티·대기석·선발 덱에 있을 때만 보상 후보가 된다.
        /// 비어 있으면 조건이 없다. 어느 적이 떨궜든, 공용 드랍이든 같은 조건을 받는다.
        /// </summary>
        public List<int> requiredUnitIds;

        /// <summary>
        /// <see cref="requiredUnitIds"/>의 주인이 현재 덱에 없어도 계정에서 스타팅 해금·육성 완료된
        /// 경우 허용할지 여부. 라이트·니콜 연동 공용 장비가 사용한다.
        /// </summary>
        public bool allowUnlockedRequiredUnits;

        /// <summary>
        /// 현재 덱과 무관하게 목록 중 하나가 계정에서 스타팅 해금 또는 육성 완료되어야 등장하는 장비.
        /// 비어 있으면 계정 해금 조건이 없다.
        /// </summary>
        public List<int> requiredUnlockedUnitIds;
        public bool twoHanded;

        /// <summary>
        /// 상점 매대에 오르는 기본가. 0이면 상점에 올리지 않는다.
        /// 실제 가격은 <see cref="RewardManager.ShopPrice(int,int)"/>가 스테이지를 곱해 정한다.
        /// </summary>
        public int shopPrice;

        /// <summary>
        /// 귀중품의 기본 판매가. 0보다 크면 <b>장비가 아니라 팔아 치우는 물건</b>이다.
        /// 값은 주울 때의 스테이지로 확정되므로 오래 들고 있어도 값이 오르지 않는다.
        /// </summary>
        public int sellPrice;
        public List<BaseClasses.EquipmentStatBonus> statBonuses;
        public List<BaseClasses.EquipmentCodeGrant> codeGrants;

        public bool IsWeapon => string.Equals(slot, "MainHand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slot, "OffHand", StringComparison.OrdinalIgnoreCase);

        /// <summary>귀중품 — 입지 못하고 팔기만 하는 물건.</summary>
        public bool IsValuable => sellPrice > 0;

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
