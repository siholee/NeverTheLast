using System;
using System.Collections.Generic;

namespace Managers
{
    [Serializable]
    /// <summary>
    /// 런 중 공용 인벤토리에 들어가는 아이템.
    /// 2026-09-26 리워크로 장비 · 장착 슬롯 · 숙련 · 무게는 모두 사라졌다.
    /// </summary>
    public class ItemData
    {
        public int id;
        public string name;
        /// <summary>Resources/Sprite/Items 기준 아이콘 파일명(확장자 제외).</summary>
        public string icon;
        public string category;
        public int rarity = 1;
        public bool eventOnly;

        /// <summary>긴 휴식에 보태는 식량 수치. 0보다 크면 식량이다.</summary>
        public int foodValue;

        /// <summary>
        /// 상점 기본가. 0이면 상점에 올리지 않는다.
        /// 실제 가격은 <see cref="RewardManager.ShopPrice(int,int)"/>가 스테이지를 곱해 정한다.
        /// </summary>
        public int shopPrice;

        /// <summary>
        /// 귀중품의 기본 판매가. 식량이 아닌 아이템에서 0보다 크면 팔아 치우는 물건이다.
        /// 값은 주울 때의 스테이지로 확정되므로 오래 들고 있어도 값이 오르지 않는다.
        /// </summary>
        public int sellPrice;
        public bool IsFood => foodValue > 0;
        public bool IsValuable => !IsFood && sellPrice > 0;
    }

    [Serializable]
    public class ItemDataList
    {
        public List<ItemData> items;
    }
}
