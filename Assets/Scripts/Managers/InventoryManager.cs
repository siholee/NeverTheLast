using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers.UI;
using UnityEngine;

namespace Managers
{
    /// <summary>주머니에 든 귀중품 하나. 값은 주운 시점에 확정된다.</summary>
    [Serializable]
    public class ValuableHolding
    {
        public int itemId;
        public int gold;
    }

    public class InventoryManager: MonoBehaviour
    {
        public int rerollTicketCount;
        public int Gold { get; private set; }
        [SerializeField] public List<int> ItemIdsInHand;
        [SerializeField] public IntIntDictionary TokensInHand;

        /// <summary>
        /// 귀중품 주머니. 장비가 아니라 <b>팔아서 골드로 바꾸는 물건</b>이라 유닛이 들지 않는다.
        ///
        /// 값을 주울 때 확정해 함께 넣어 둔다. 판매가가 스테이지에 비례하는데 파는 시점으로
        /// 계산하면 "후반까지 쌓아 두는 쪽이 이득"이 되어 주머니가 저금통이 된다.
        /// </summary>
        private readonly List<ValuableHolding> _valuables = new();

        public IReadOnlyList<ValuableHolding> Valuables => _valuables;

        /// <summary>주머니에 든 귀중품을 전부 팔았을 때 받는 골드.</summary>
        public int ValuableTotalGold
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _valuables.Count; i++) total += _valuables[i].gold;
                return total;
            }
        }


        public void Initialize()
        {
            rerollTicketCount = 0;
            Gold = 500;
            var tokensData = GameManager.Instance.resourceTokenDataList;
            if (TokensInHand == null) TokensInHand = new IntIntDictionary();
            TokensInHand.Clear();
            foreach (var token in tokensData.tokens)
            {
                TokensInHand[token.id] = 999;
            }

            rerollTicketCount = 50;

            if (ItemIdsInHand == null) ItemIdsInHand = new List<int>();
            ItemIdsInHand.Clear();
            _valuables.Clear();

            RefreshPanel();
        }
    
        public void AddToken(int tokenId, int amount)
        {
            if (!TokensInHand.TryAdd(tokenId, amount))
            {
                TokensInHand[tokenId] += amount;
            }

            RefreshPanel();
        }

        public bool SpendToken(Dictionary<int, int> tokensToSpend)
        {
            if (tokensToSpend.Any(token => !TokensInHand.ContainsKey(token.Key) || TokensInHand[token.Key] < token.Value))
            {
                return false; // 토큰이 없거나 부족한 경우
            }
            foreach (var token in tokensToSpend)
            {
                TokensInHand[token.Key] -= token.Value;
            }
            RefreshPanel();
            return true;
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            RefreshPanel();
        }

        public bool TrySpendGold(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (Gold < amount) return false;
            Gold -= amount;
            RefreshPanel();
            return true;
        }

        public void RestoreGold(int amount)
        {
            Gold = Mathf.Max(0, amount);
            RefreshPanel();
        }

        /// <summary>귀중품을 주머니에 넣는다. 값은 지금 스테이지로 확정한다.</summary>
        public void AddValuable(int itemId, int gold)
        {
            if (itemId <= 0) return;
            _valuables.Add(new ValuableHolding { itemId = itemId, gold = Mathf.Max(0, gold) });
            RefreshPanel();
        }

        /// <summary>주머니의 n번째 귀중품을 판다. 값은 넣을 때 확정한 그대로다.</summary>
        public bool TrySellValuable(int index, out int gold)
        {
            gold = 0;
            if (index < 0 || index >= _valuables.Count) return false;

            gold = _valuables[index].gold;
            _valuables.RemoveAt(index);
            AddGold(gold);
            return true;
        }

        /// <summary>주머니를 통째로 판다. 받은 골드를 돌려준다.</summary>
        public int SellAllValuables()
        {
            int total = ValuableTotalGold;
            _valuables.Clear();
            if (total > 0) AddGold(total);
            else RefreshPanel();
            return total;
        }

        public void RestoreValuables(IEnumerable<ValuableHolding> saved)
        {
            _valuables.Clear();
            if (saved == null) return;
            foreach (ValuableHolding holding in saved)
            {
                if (holding == null || holding.itemId <= 0) continue;
                _valuables.Add(new ValuableHolding { itemId = holding.itemId, gold = Mathf.Max(0, holding.gold) });
            }
        }

        public bool AddItem(int itemId, Unit preferredCarrier = null)
        {
            if (itemId <= 0) return false;

            var candidates = new List<Unit>();
            if (preferredCarrier != null) candidates.Add(preferredCarrier);
            if (GridManager.Instance?.heroList != null)
            {
                candidates.AddRange(GridManager.Instance.heroList.Where(hero =>
                    hero != null && hero.isActive && !hero.IsEnemy && hero != preferredCarrier));
            }

            foreach (Unit carrier in candidates)
            {
                if (carrier.TryStoreItem(itemId, out _))
                {
                    RefreshPanel();
                    return true;
                }
            }

            // 영웅 생성 전 지급되는 레거시/사건 아이템만 공유 보관함에 임시 저장한다.
            if (candidates.Count == 0)
            {
                ItemIdsInHand ??= new List<int>();
                ItemIdsInHand.Add(itemId);
                RefreshPanel();
                return true;
            }

            Debug.LogWarning($"[인벤토리] 아이템 데이터가 없거나 휴대 대상을 찾지 못해 아이템 {itemId}을 지급하지 못했습니다.");
            return false;
        }

        /// <summary>
        /// 장비를 다른 캐릭터에게 넘긴다. 발더스 게이트에서 초상화 위로 끌어다 놓는 동작이다.
        ///
        /// 보상 장비가 1번 캐릭터에게만 쌓여 다른 캐릭터가 영영 착용하지 못하던 문제를
        /// 푸는 통로다. 출처는 셋 중 하나다 — 상대의 개인 인벤토리, 상대가 장착 중인 것,
        /// 그리고 영웅 생성 전에 지급돼 공용 보관함에 남아 있는 것.
        /// </summary>
        public bool TryTransferItem(Unit from, Unit to, int itemId, out string reason)
        {
            reason = null;
            if (to == null)
            {
                reason = "받을 유닛이 없습니다.";
                return false;
            }

            bool fromShared = from == null;
            bool wasEquipped = false;

            if (fromShared)
            {
                if (ItemIdsInHand?.Remove(itemId) != true)
                {
                    reason = "공용 보관함에 없는 아이템입니다.";
                    return false;
                }
            }
            else if (from.CarriedItemIds.Contains(itemId))
            {
                from.RemoveCarriedItem(itemId);
            }
            else if (from.IsEquipped(itemId))
            {
                // 넘길 때는 상대의 인벤토리를 거치지 않는다. 거치면 중량이 잠깐 두 번 잡힌다.
                if (!from.TryUnequip(itemId, storeToCarried: false))
                {
                    reason = "장비를 벗기지 못했습니다.";
                    return false;
                }

                wasEquipped = true;
            }
            else
            {
                reason = "이 유닛이 가진 아이템이 아닙니다.";
                return false;
            }

            if (to.TryStoreItem(itemId, out reason))
            {
                RefreshPanel();
                return true;
            }

            // 넘기지 못했다. 원래 자리로 되돌린다.
            if (fromShared)
            {
                ItemIdsInHand ??= new List<int>();
                ItemIdsInHand.Add(itemId);
            }
            else if (wasEquipped)
            {
                from.TryEquipItem(itemId, out _);
            }
            else
            {
                from.TryStoreItem(itemId, out _);
            }

            return false;
        }

        public bool TryEquipStoredItem(Unit unit, int itemId, out string reason)
        {
            reason = null;
            if (unit == null)
            {
                reason = "장착할 유닛이 없습니다.";
                return false;
            }

            bool fromLegacyStorage = ItemIdsInHand?.Contains(itemId) == true;
            if (!unit.CarriedItemIds.Contains(itemId) && !fromLegacyStorage)
            {
                reason = "이 유닛이 휴대 중인 아이템이 아닙니다.";
                return false;
            }

            if (fromLegacyStorage)
            {
                if (!unit.TryStoreItem(itemId, out reason)) return false;
                ItemIdsInHand.Remove(itemId);
            }

            if (!unit.TryEquipCarriedItem(itemId, out reason))
            {
                if (fromLegacyStorage)
                {
                    unit.RemoveCarriedItem(itemId);
                    ItemIdsInHand.Add(itemId);
                }
                return false;
            }

            RefreshPanel();
            return true;
        }

        public void RefreshPanel()
        {
            // 자원 표시는 전투 HUD 상단 스트립이 담당한다.
            GameManager.Instance?.uiManager?.UpdateGoldText();
        }
    }
}
