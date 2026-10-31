#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Codes.Base;
using Entities;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static string _baselinePolicy;
        private static CampaignRun _baselineRun;
        private readonly Dictionary<RewardDef, BaselineOffer> _baselineOffers = new();
        private static float BaselineCodeBonus(Unit unit, int id)
        {
            if (id <= 0 || string.IsNullOrEmpty(_baselinePolicy)) return 0;
            return _baselinePolicy switch
            {
                "shi" when unit.ID == 2 && id is 501 or 601 or 507 or 607 or 508 or 608 or 711 or 724 => 22,
                "guard" when unit.ID is 1 or 25 && id is 502 or 602 or 503 or 603 or 504 or 604 or 635 => 22,
                "rotation" when id is 506 or 606 => 22,
                "balanced" or "himiko_front" or "shi" or "guard" or "rotation" => 0,
                _ => throw new InvalidOperationException("알 수 없는 기준 파티 정책: " + _baselinePolicy)
            };
        }
        [Serializable] private sealed class BaselineOffer
        {
            public string run, team, policy, source, name;
            public int attempt, seed, stage, code, item, target, tier, price, gold, goldAfter;
            public float score;
            public bool claimable, affordable, selected;
        }
        private BaselineOffer BaselineRow(string source, RewardDef reward)
        {
            Unit target = CampaignTarget(reward);
            int price = source == "shop" ? RewardManager.ShopPrice(reward, GameManager.Instance.RoundManager.Stage) : 0;
            return new BaselineOffer { run = _baselineRun.id, team = _baselineRun.team, policy = _baselinePolicy,
                attempt = _baselineRun.attempt, seed = _baselineRun.seed, stage = GameManager.Instance.RoundManager.Stage,
                source = source, name = reward.displayName, code = reward.codeId, item = reward.itemId,
                target = target?.ID ?? 0, tier = RewardCodeCatalog.TierOf(reward.codeId), price = price,
                gold = GameManager.Instance.inventoryManager.Gold, score = CampaignRewardScore(reward),
                claimable = source == "shop" ? !reward.RequiresTargetSelection || target != null : RewardManager.Instance.CanClaimReward(reward),
                affordable = price <= GameManager.Instance.inventoryManager.Gold };
        }
        private void BaselineOffers(string source, IEnumerable<RewardDef> rewards)
        {
            if (string.IsNullOrEmpty(_baselinePolicy) || _baselineRun == null || rewards == null) return;
            _baselineOffers.Clear();
            foreach (var reward in rewards) { var row = BaselineRow(source, reward); _baselineOffers[reward] = row; CampaignWrite("reward_offers", row); }
        }
        private void BaselineSelected(string source, RewardDef reward, Unit target, int price)
        {
            if (string.IsNullOrEmpty(_baselinePolicy) || _baselineRun == null) return;
            var row = _baselineOffers.TryGetValue(reward, out var offered) ? offered : BaselineRow(source, reward);
            row.selected = true; row.source = source; row.target = target?.ID ?? 0; row.price = price;
            row.goldAfter = GameManager.Instance.inventoryManager.Gold;
            CampaignWrite("reward_choices", row);
        }
        [Serializable] private sealed class BaselineLoadout
        { public string run, team, policy, active, stored; public int attempt, stage, unit, level, specialAttack, defense; public float speed; }
        private void BaselineInventory()
        {
            if (string.IsNullOrEmpty(_baselinePolicy) || _baselineRun == null) return;
            foreach (Unit unit in GridManager.Instance.heroList.Where(u => u != null && u.ID > 0))
                CampaignWrite("loadouts", new BaselineLoadout { run = _baselineRun.id, team = _baselineRun.team, policy = _baselinePolicy,
                    attempt = _baselineRun.attempt, stage = GameManager.Instance.RoundManager.Stage, unit = unit.ID, level = unit.Level,
                    speed = unit.SpeedCurr, specialAttack = unit.SpAtkCurr, defense = unit.DefCurr,
                    active = string.Join(";", unit.RewardCodeSlots), stored = string.Join(";", unit.StoredRewardCodeIds) });
        }
    }
}
#endif
