using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
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

    /// <summary>
    /// 파티 공용 인벤토리. 유닛별 휴대 · 장착 · 무게는 없다.
    /// 같은 ID가 여러 번 들어가면 그 수만큼 보유한 것으로 센다.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public int rerollTicketCount;
        public int Gold { get; private set; }
        [SerializeField] public List<int> ItemIdsInHand;
        [SerializeField] public IntIntDictionary TokensInHand;

        private readonly List<ValuableHolding> _valuables = new();
        public IReadOnlyList<ValuableHolding> Valuables => _valuables;

        public int ValuableTotalGold => _valuables.Sum(holding => holding?.gold ?? 0);

        /// <summary>공용 인벤토리에 든 식량의 총 휴식 수치.</summary>
        public int FoodUnits => (ItemIdsInHand ?? new List<int>())
            .Select(FindItem)
            .Where(item => item?.IsFood == true)
            .Sum(item => item.foodValue);

        public void Initialize()
        {
            // 리롤 티켓은 이제 보상 선택지를 다시 굴리는 실제 자원이다(운명의 주사위). 시작 지급은 없다.
            rerollTicketCount = 0;
            Gold = 500;
            TokensInHand ??= new IntIntDictionary();
            TokensInHand.Clear();
            foreach (var token in GameManager.Instance.resourceTokenDataList.tokens) TokensInHand[token.id] = 0;

            ItemIdsInHand ??= new List<int>();
            ItemIdsInHand.Clear();
            _valuables.Clear();

            // 새 런은 긴 휴식 한 번을 시험할 수 있는 야영 식량으로 시작한다.
            ItemIdsInHand.Add(5002);
            RefreshPanel();
        }

        public int CountItem(int itemId) => ItemIdsInHand?.Count(id => id == itemId) ?? 0;

        public void AddToken(int tokenId, int amount)
        {
            if (!TokensInHand.TryAdd(tokenId, amount)) TokensInHand[tokenId] += amount;
            RefreshPanel();
        }

        public bool SpendToken(Dictionary<int, int> tokensToSpend)
        {
            if (tokensToSpend.Any(token => !TokensInHand.ContainsKey(token.Key) || TokensInHand[token.Key] < token.Value))
                return false;
            foreach (var token in tokensToSpend) TokensInHand[token.Key] -= token.Value;
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

        private static int CurrentStage => Mathf.Max(1, GameManager.Instance?.RoundManager?.Stage ?? 1);

        public bool AddItem(int itemId)
        {
            ItemData item = FindItem(itemId);
            if (item == null) return false;
            if (item.IsValuable)
            {
                AddValuable(itemId, RewardManager.ValuablePrice(item, CurrentStage));
                return true;
            }

            ItemIdsInHand ??= new List<int>();
            ItemIdsInHand.Add(itemId);
            RefreshPanel();
            return true;
        }

        /// <summary>공용 인벤토리에서 아이템 하나를 꺼내 쓴다(비약 · 코드 도구). 없으면 false.</summary>
        public bool RemoveItem(int itemId)
        {
            if (ItemIdsInHand == null || !ItemIdsInHand.Remove(itemId)) return false;
            RefreshPanel();
            return true;
        }

        public ItemData Find(int itemId) => FindItem(itemId);

        /// <summary>
        /// 식량을 정확히 소모한다. 현재 식량 단위는 10의 배수이므로 긴 휴식 비용도 같은 단위다.
        /// 큰 묶음부터 사용하되 비용을 넘기는 묶음은 건너뛴다.
        /// </summary>
        public bool TryConsumeFood(int amount)
        {
            if (amount <= 0) return true;
            if (FoodUnits < amount || ItemIdsInHand == null) return false;

            var candidates = ItemIdsInHand
                .Select((id, index) => (id, index, item: FindItem(id)))
                .Where(entry => entry.item?.IsFood == true)
                .OrderByDescending(entry => entry.item.foodValue)
                .ToList();

            int remaining = amount;
            var remove = new List<int>();
            foreach (var entry in candidates)
            {
                if (entry.item.foodValue > remaining) continue;
                remaining -= entry.item.foodValue;
                remove.Add(entry.index);
                if (remaining == 0) break;
            }

            if (remaining != 0) return false;
            foreach (int index in remove.OrderByDescending(value => value)) ItemIdsInHand.RemoveAt(index);
            RefreshPanel();
            return true;
        }

        public void AddValuable(int itemId, int gold)
        {
            if (itemId <= 0) return;
            _valuables.Add(new ValuableHolding { itemId = itemId, gold = Mathf.Max(0, gold) });
            RefreshPanel();
        }

        public bool TrySellValuable(int index, out int gold)
        {
            gold = 0;
            if (index < 0 || index >= _valuables.Count) return false;
            gold = _valuables[index].gold;
            _valuables.RemoveAt(index);
            AddGold(gold);
            return true;
        }

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

        public IEnumerable<(ItemData item, int count)> SharedStacks()
        {
            return (ItemIdsInHand ?? new List<int>())
                .GroupBy(id => id)
                .Select(group => (FindItem(group.Key), group.Count()))
                .Where(stack => stack.Item1 != null)
                .OrderBy(stack => stack.Item1.category)
                .ThenBy(stack => stack.Item1.name);
        }

        private static ItemData FindItem(int itemId)
            => GameManager.Instance?.itemDataList?.items?.FirstOrDefault(item => item != null && item.id == itemId);

        public void RefreshPanel() => GameManager.Instance?.uiManager?.UpdateGoldText();
    }
}
