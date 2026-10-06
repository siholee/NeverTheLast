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

        // ── 전투 비약(category: Tonic) — 준비 화면에서 사용하면 다음 전투 1회 아군 전원에게 걸린다(Detail_17 §6) ──
        /// <summary>퍼센트 스탯(키 = StatType 이름, 값 = 퍼센트 10 = +10%).</summary>
        public Dictionary<string, float> tonicPercentStats;
        /// <summary>깡 스탯(속도 +10, 치명타 확률은 퍼센트 포인트 10 = +10%p).</summary>
        public Dictionary<string, float> tonicFlatStats;
        /// <summary>받는 피해 퍼센트 변화(-10 = 10% 경감).</summary>
        public float tonicDamageTakenPercent;
        /// <summary>전투 시작 마나(최대치 대비 퍼센트). Passive · Stack 자원에는 무효.</summary>
        public float tonicStartManaPercent;
        /// <summary>첫 해로운 상태 1회를 무효화한다(정화의 성수).</summary>
        public bool tonicNegateFirstHarmful;

        // ── 코드 도구(category: CodeTool) ──
        /// <summary>승급의 인장이 올릴 수 있는 최대 티어(청동 2 · 은 3 · 금 4 · 별빛 5). 0이면 인장이 아니다.</summary>
        public int sealMaxTier;
        /// <summary>전수의 두루마리 — 보상 코드 하나를 다른 아군에게 옮긴다.</summary>
        public bool transferScroll;

        /// <summary>보상 풀의 소모품 분류에 오르는가. 식량 · 귀중품은 적 드랍 분류에서 따로 나온다.</summary>
        public bool rewardPool;

        public bool IsFood => foodValue > 0;
        public bool IsValuable => !IsFood && sellPrice > 0;
        public bool IsTonic => string.Equals(category, "Tonic", StringComparison.OrdinalIgnoreCase);
        public bool IsCodeTool => string.Equals(category, "CodeTool", StringComparison.OrdinalIgnoreCase);
        public bool IsSeal => IsCodeTool && sealMaxTier > 0;
    }

    [Serializable]
    public class ItemDataList
    {
        public List<ItemData> items;
    }
}
