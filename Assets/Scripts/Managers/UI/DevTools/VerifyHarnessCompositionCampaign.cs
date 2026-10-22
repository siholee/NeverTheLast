#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Codes.Base;
using Codes.Passive.Reward;
using Combat;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        [Serializable] private sealed class CampaignSpec
        {
            public string key, name;
            public bool currentComparison;
            public int[] ids, xs, ys;
            public List<string> aliases = new();
        }
        [Serializable] private sealed class CampaignCatalog { public List<CampaignSpec> teams = new(); }
        [Serializable] private sealed class CampaignRest { public string team, label; public int attempt, stage, alive, food; public bool free; }
        [Serializable] private sealed class CampaignThemeRecovery
        { public string team, party; public int attempt, stage, theme, food, shortRests; public bool allFull, resourcesReset; }
        private static readonly Dictionary<string, CampaignSpec> CampaignLineups = new();
        [Serializable] private sealed class CampaignBattle
        {
            public string run, team, category, stageKind, enemies, reason, party, damage, formation;
            public int attempt, seed, stage, theme, turns, survivors;
            public float av, cycles, meanLevel;
            public bool won;
        }
        [Serializable] private sealed class CampaignRewardRejection
        { public string team, reward; public int attempt, stage, code, target; }
        [Serializable] private sealed class CampaignRun
        {
            public string id, team, stop;
            public int attempt, seed, reached, cleared, battles, events, firstLoss;
            public bool capped, valid;
            public float wallSeconds;
        }
        private void CampaignWrite(string name, object value)
            => File.AppendAllText(Path.Combine(_requestDirectory, name + ".jsonl"), JsonUtility.ToJson(value) + Environment.NewLine);

        private CampaignCatalog CampaignBuildCatalog()
        {
            CampaignLineups.Clear();
            var data = GameManager.Instance.dataManager.FetchSynergyDataList();
            var catalog = new CampaignCatalog();
            var keys = new Dictionary<string, CampaignSpec>();
            void Add(string alias, string name, IEnumerable<int> members, Dictionary<int, string> rows = null)
            {
                int[] ids = members.Distinct().ToArray();
                if (ids.Length != 5 || ids.Any(id => !data.units.Any(u => u.id == id))) return;
                int[] xs = ids.Select(id => (rows?.GetValueOrDefault(id) ?? data.units.First(u => u.id == id).row) == "Rear" ? -2 : -1).ToArray();
                if (xs.Count(x => x == -1) > 4 || xs.Count(x => x == -2) > 4) return;
                int[] ys = new int[5]; int front = 0, rear = 0; int[] order = { 2, 3, 1, 4 };
                for (int i = 0; i < 5; i++) ys[i] = order[xs[i] == -1 ? front++ : rear++];
                // 호루스의 후계자는 선택 순서를 사용하므로 호루스 편성은 그 순서를 보존한다.
                string key = string.Join(";", ids.Select((id, i) => (id, i)).OrderBy(p => p.id).Select(p => p.id + ":" + xs[p.i])) +
                    (ids.Contains(120) ? "/order=" + string.Join(",", ids.Where(id => id != 120)) : "");
                if (keys.TryGetValue(key, out var existing)) { existing.aliases.Add(alias); return; }
                var spec = new CampaignSpec { key = "campaign_" + (catalog.teams.Count + 1).ToString("D2"), name = name, ids = ids, xs = xs, ys = ys };
                spec.aliases.Add(alias); keys.Add(key, spec); catalog.teams.Add(spec); CampaignLineups.Add(spec.key, spec);
            }
            foreach (var rec in data.recommendations)
                foreach (var pair in new[] { ("best", rec.best), ("basic", rec.basic) })
                    if (pair.Item2?.members != null)
                        Add(rec.mainName + "/" + pair.Item1, data.archetypes.FirstOrDefault(a => a.id == pair.Item2.archetype)?.name ?? rec.axis,
                            new[] { rec.mainId }.Concat(pair.Item2.members), pair.Item2.rows);
            // 사바흐의 디버퍼 교체 비교. 두 전열과 나머지 후열 위치를 고정한다.
            foreach (var support in new[] { 64, 44 })
                Add("점검/사바흐_" + (support == 64 ? "바유" : "히미코"), "사바흐 · 세이 · " + (support == 64 ? "바유" : "히미코"),
                    new[] { 3, 4, support, 1, 167 },
                    new Dictionary<int, string> { [3] = "Rear", [4] = "Front", [support] = "Rear", [1] = "Rear", [167] = "Front" });
            Add("점검/사바흐_바유히미코", "사바흐 · 바유 · 히미코 (세이 제외)", new[] { 3, 4, 64, 44, 167 },
                new Dictionary<int, string> { [3] = "Rear", [4] = "Front", [64] = "Rear", [44] = "Rear", [167] = "Front" });
            foreach (var archetype in data.archetypes)
                Add("archetype/" + archetype.id, archetype.name, archetype.core.Concat(archetype.partners ?? new List<int>()).Distinct().Take(5));
            foreach (var pair in Checkpoint48Lineups)
            {
                Dictionary<int, string> rows = pair.Key == "b48_nicol_solo"
                    ? new() { [7] = "Front", [1] = "Rear", [122] = "Rear", [162] = "Rear", [168] = "Rear" }
                    : pair.Key == "b48_shi_hypercarry"
                        ? new() { [2] = "Rear", [1] = "Front", [25] = "Front", [60] = "Rear", [44] = "Rear" }
                        : pair.Key.StartsWith("b48_lokapala")
                            ? pair.Value.ToDictionary(id => id, id => id is 60 or 66 ? "Front" : "Rear") : null;
                string name = pair.Key switch
                {
                    "b48_nicol_solo" => "잔 반격",
                    "b48_lokapala" => "로카팔라 · 야마",
                    "b48_lokapala_surya" => "로카팔라 · 수리야",
                    "b48_lokapala_kubera" => "로카팔라 · 쿠베라",
                    "b48_lokapala_agni" => "로카팔라 · 아그니",
                    _ => pair.Key
                };
                Add("검증 편성/" + pair.Key, name, pair.Value, rows);
            }
            // 확정 추천과 구분되는 난이도 대조군. 슬롯을 임의 보너스로 보완하지 않는다.
            Add("후보/소환", "소환 생애 순환 후보", new[] { 180, 125, 10, 20, 124 });
            Add("후보/방출융해", "자해 융해 후보", new[] { 81, 62, 120, 83, 82 });
            Add("후보/추공", "일광 추공 후보", new[] { 40, 80, 62, 66, 160 });
            Add("후보/처형", "처형 격파 후보", new[] { 2, 1, 25, 60, 44 },
                new Dictionary<int, string> { [2] = "Rear", [1] = "Front", [25] = "Front", [60] = "Rear", [44] = "Rear" });
            Add("대조/반격_메인부재", "미완성 반격: 메인 딜러 부재", new[] { 83, 122, 162, 163, 160 });
            Add("대조/추공_공급부족", "미완성 일광: 능동 추공 부족", new[] { 40, 80, 120, 83, 82 });
            Add("대조/지딜_치유부재", "미완성 지속피해: 치유 지원 부재", new[] { 61, 45, 123, 20, 83 });
            Add("대조/소환_토트부재", "미완성 소환: 토트 부재", new[] { 180, 125, 10, 121, 60 });
            Add("대조/재보_특수부재", "미완성 재보: 특수 회복 부재", new[] { 220, 65, 200, 83, 60 });
            Add("대조/디버프_공급부족", "미완성 디버프: 공급·치유 부족", new[] { 3, 44, 60, 83, 121 });
            Add("후보/재보", "재보 로카팔라 후보", new[] { 220, 65, 200, 63, 60 }, new Dictionary<int, string> { [60] = "Front" });
            Add("추가/소환_아누비스", "소환 아누비스 수호", new[] { 180, 125, 10, 121, 124 });
            Add("최적/소환", "우치 순수 소환 파티", new[] { 180, 125, 10, 20, 124 },
                new Dictionary<int, string> { [180] = "Rear", [125] = "Rear", [10] = "Rear", [20] = "Front", [124] = "Rear" });
            Add("최적/소환_혼합", "우치 아누비스 혼합 파티", new[] { 10, 180, 121, 20, 124 },
                new Dictionary<int, string> { [10] = "Rear", [180] = "Rear", [121] = "Front", [20] = "Front", [124] = "Rear" });
            Add("최적/소환_아누비스", "아누비스 단독 전열", new[] { 180, 125, 10, 121, 124 },
                new Dictionary<int, string> { [180] = "Rear", [125] = "Rear", [10] = "Rear", [121] = "Front", [124] = "Rear" });
            Add("점검/오리온_세이", "오리온 반격 · 세이 교체", new[] { 1, 22, 122, 162, 160 },
                new Dictionary<int, string> { [1] = "Front", [22] = "Front", [122] = "Rear", [162] = "Rear", [160] = "Rear" });
            Add("점검/잔_세이", "잔 반격 · 세이 교체", new[] { 1, 7, 122, 162, 160 },
                new Dictionary<int, string> { [1] = "Front", [7] = "Front", [122] = "Rear", [162] = "Rear", [160] = "Rear" });
            Add("점검/오리온_세이후열", "오리온 반격 · 세이 후열", new[] { 1, 22, 122, 162, 160 },
                new Dictionary<int, string> { [1] = "Rear", [22] = "Front", [122] = "Rear", [162] = "Rear", [160] = "Rear" });
            Add("점검/잔_세이후열", "잔 반격 · 세이 후열", new[] { 1, 7, 122, 162, 160 },
                new Dictionary<int, string> { [1] = "Rear", [7] = "Front", [122] = "Rear", [162] = "Rear", [160] = "Rear" });
            // 생성 순서로 매긴 기존 키는 보존한다. 제외 편성의 번호는 재사용하지 않는다.
            foreach (var retired in catalog.teams.Where(spec => (spec.ids.Contains(180) && spec.ids.Contains(125) && spec.ids.Contains(121)) ||
                         (Array.IndexOf(spec.ids, 7) is int jean && jean >= 0 && spec.xs[jean] == -1 &&
                          Array.IndexOf(spec.ids, 1) is int sei && sei >= 0 && spec.xs[sei] == -1)).ToList())
            {
                catalog.teams.Remove(retired);
                CampaignLineups.Remove(retired.key);
            }
            // 신규 추천이 생겨도 생성 순번으로 다른 편성을 현행 비교군으로 오인하지 않는다.
            var currentAliases = new HashSet<string> { "최적/소환", "최적/소환_혼합", "검증 편성/b48_dot_active",
                "검증 편성/b48_dot_poison", "검증 편성/b48_dot_mixed", "검증 편성/b48_dot_mixed_amp",
                "검증 편성/b48_lokapala", "검증 편성/b48_lokapala_surya",
                "검증 편성/b48_lokapala_kubera", "검증 편성/b48_lokapala_agni", "후보/처형",
                "검증 편성/b48_nicol_solo", "검증 편성/b48_shi_hypercarry" };
            foreach (var spec in catalog.teams) spec.currentComparison = spec.aliases.Any(currentAliases.Contains);
            File.WriteAllText(Path.Combine(_requestDirectory, "catalog.json"), JsonUtility.ToJson(catalog, true));
            return catalog;
        }

        private static readonly int[] CampaignKuberaCore = { 60, 63, 64, 66 };
        private static readonly int[] CampaignKuberaOffense = { 501, 601, 507, 607, 508, 608 };
        // 공통 로카팔라 4인을 동반한 쿠베라는 후원자 대신 딜러 자리로 검증한다.
        // 다른 재보 편성의 치피 후원자 운영은 바꾸지 않는다.
        private static bool CampaignKuberaMain(Unit unit) => unit != null && unit.ID == 65 &&
            CampaignKuberaCore.All(id => GridManager.Instance.heroList.Any(u => u != null &&
                (u.ID > 0 ? u.ID : u.LastActiveId) == id));

        private static int[] CampaignWishlist(Unit unit)
        {
            if (unit.ID == 3) return new[] { 505, 605, 607, 608, 606, 701 };
            if (unit.ID == 4) return new[] { 502, 503, 603, 604, 605, 606, 635, 739 };
            if (CampaignKuberaMain(unit)) return new[] { 501, 601, 607, 608, 606, 728, 730 };
            if (_campaignSummonRewardStudy && unit.ID == 20) return new[] { 602, 603, 604, 625, 635, 703, 701, 738 };
            if (_campaignSummonRewardStudy && unit.ID == 124) return new[] { 602, 623, 606, 622, 634, 648, 650, 734, 736 };
            if (unit.ID == 164) return new[] { 605, 617, 606, 622, 601, 602, 701 };
            if (unit.ID == 165) return new[] { 601, 605, 607, 608, 606, 615, 617, 622 };
            if (unit.ID == 166) return new[] { 605, 606, 622, 602, 604, 618, 701 };
            if (unit.ID == 167) return new[] { 602, 623, 603, 604, 606, 622, 634, 625 };
            if (unit.ID == 40) return CounterBuild(unit, "balanced");
            if (unit.ID == 162) return new[] { 601, 608, 606, 623, 634, 648, 650 };
            if (unit.ID == 168) return new[] { 605, 606, 601, 607, 608, 712, 701 };
            if (unit.ID == 163) return new[] { 602, 605, 606, 622, 625, 701, 604 };
            if (unit.ID == 7) return CounterBuild(unit, "jean");
            if (unit.ID == 122) return new[] { 601, 607, 608, 612, 615, 606, 629 };
            if (unit.ID == 82) return new[] { 602, 623, 606, 625, 634, 701, 730 };
            if (unit.ID == 160) return new[] { 601, 608, 606, 623, 634, 705, 730 };
            if (unit.ID == 161) return new[] { 606, 608, 622, 602, 604, 730, 728 };
            if (unit.ID is 66 or 200 or 125 or 1) return B48Build(unit, "baseline").Concat(new[] { 635 }).ToArray();
            return B48Build(unit, unit.ID == 121 ? "tank" : "baseline");
        }
        private static bool _campaignInventoryAware;
        // 전투 중 사망·부활과 무관한 런 시작 편성의 개시 능력. 종료 시 해제한다.
        private static bool? _campaignGateRoster;
        private static float CampaignCodeValue(Unit unit, int id)
        {
            if (id <= 0) return 0;
            if (DebuffRewardCode.Ids.Contains(id))
            {
                var role = SynergyCatalog.UnitOf(unit.ID)?.roles ?? new List<string>();
                bool debuffer = role.Any(r => r is "DEF_SHRED" or "CONTROL" or "DOT") || unit.ID is 3 or 4 or 64;
                bool finiteNonDotDebuff = role.Contains("DEF_SHRED") || unit.ID is 3 or 4;
                bool front = unit.IsOnField && unit.currentCell?.xPos == GridManager.Instance.GetFrontColumn(false);
                bool damage = unit.ID is 2 or 3 or 7 or 40 or 61 or 62 or 65 or 67 or 122 or 165;
                return id switch
                {
                    667 => damage ? 73 : 18,
                    840 => damage ? 91 : 25,
                    918 => damage ? 105 : 30,
                    668 => finiteNonDotDebuff ? 75 : 2,
                    769 => debuffer ? (front ? 82 : 48) : 2,
                    841 => debuffer ? (front ? 96 : 62) : 2,
                    770 => front ? 83 : 32,
                    771 => debuffer && (unit.ID is 3 or 4 or 44 or 64 or 164 or 166) ? 83 : debuffer ? 50 : 2,
                    _ => 1
                };
            }
            if (SpecialElementRewardCode.Ids.Contains(id))
            {
                bool hasGate = _campaignGateRoster ?? GridManager.Instance.heroList.Any(Combat.SpecialAction.CanOpen);
                if (id == 837 && !hasGate) return 1;
                bool gate = Combat.SpecialAction.CanOpen(unit);
                bool standalone = unit.ActivePassiveCodesResolved().Any(code => code is Combat.ISpecialActionProvider);
                bool special = Combat.SpecialAction.CanUse(unit) && (!unit.HasUnitTag(Combat.SpecialAction.LokapalaTag) || hasGate || standalone);
                bool specialDamage = special && (unit.ActiveSpecialCode is { AttackForm: > 0 } || standalone);
                bool elemental = SynergyCatalog.UnitOf(unit.ID)?.roles.Contains("ELEM_APPLY") == true || unit.ID is 61 or 63 or 67 or 125;
                bool front = unit.IsOnField && unit.currentCell?.xPos == GridManager.Instance.GetFrontColumn(false);
                return id switch
                {
                    917 or 763 or 764 or 767 or 666 => gate ? (id == 767 ? 109 : 100) : 1,
                    765 => special ? (front ? 108 : 95) : 1,
                    513 => special ? (front ? 56 : 30) : 1,
                    663 => special ? 76 : 1,
                    837 => specialDamage && hasGate && unit.HasSpecialAction && unit.HasUnitTag(Combat.SpecialAction.LokapalaTag) ? (unit.ID == 66 ? 65 : 95) : 1,
                    664 or 839 => specialDamage && unit.ID != 66 ? 95 : specialDamage ? 65 : 1,
                    838 => specialDamage && unit.Element == "Electro" ? 98 : 1,
                    766 or 665 or 768 => elemental ? (id == 665 ? (front ? 72 : 35) : id == 768 ? 90 : unit.ID is 64 or 66 ? 50 : 80) : 1,
                    _ => 1
                };
            }
            if (id == 748) return unit.IsOnField && unit.currentCell?.xPos == GridManager.Instance.GetFrontColumn(false) &&
                GridManager.Instance.heroList.Count(u => u != null && u.isActive && u.IsOnField &&
                    u.currentCell?.xPos == GridManager.Instance.GetFrontColumn(false)) == 1 ? 106 : 1;
            if (SingleAttackRewardCode.Ids.Contains(id)) return SingleRewardCampaignValue(unit, id);
            if (FollowUpRewardCode.Ids.Contains(id)) return FollowUpCampaignValue(unit, id);
            if (_campaignSummonRewardStudy && SummonStudyCodeValue(unit, id) is float summonValue) return summonValue;
            if (_campaignDotRewardStudy && DotStudyCodeValue(unit, id) is float studyValue) return studyValue;
            int[] wishes = CampaignWishlist(unit);
            bool suited = wishes.Any(w => w == id || RewardCodeCatalog.BronzeSealTargets.GetValueOrDefault(id) == w || RewardCodeCatalog.IsUpperOf(id, w) || RewardCodeCatalog.IsUpperOf(w, id));
            float score = 8 + RewardCodeCatalog.TierOf(id) * 5;
            if (suited) score += 35;
            if (CampaignKuberaMain(unit) && CampaignKuberaOffense.Any(w => w == id ||
                RewardCodeCatalog.BronzeSealTargets.GetValueOrDefault(id) == w ||
                RewardCodeCatalog.IsUpperOf(id, w) || RewardCodeCatalog.IsUpperOf(w, id))) score += 18;
            var tags = RewardCodeCatalog.TagsOf(id);
            var roles = SynergyCatalog.UnitOf(unit.ID)?.roles ?? new List<string>();
            if (tags.Contains("Healing") && !roles.Contains("HEAL")) return 2;
            if (tags.Contains("Shielding") && !roles.Contains("SHIELD") && unit.ID != 83) return 2;
            if (tags.Contains("Counter") && unit.ID is not (7 or 22 or 123)) return 2;
            if (tags.Contains("FollowUp") && !CampaignUsesAdditional(unit) && unit.ID != 180) return 2;
            if (id == 634 && !roles.Contains("HEAL")) return 1;
            if (id == 635 && !roles.Contains("SHIELD") && unit.ID != 83) return 1;
            return score;
        }
        private static (Unit target, int replace, float score) CampaignCodeChoice(int id, bool allowNegative = false)
        {
            Unit best = null; int replace = 0; float score = allowNegative ? float.NegativeInfinity : -1;
            foreach (Unit unit in GridManager.Instance.heroList.Where(u => u != null && u.ID > 0))
            {
                if (!unit.CanReceiveRewardCode(id, out int lower, out bool overwrite, out _)) continue;
                float value = CampaignCodeValue(unit, id); int old = 0;
                if (overwrite)
                {
                    old = unit.RewardCodeSlots.Where(x => x > 0).OrderBy(x => CampaignCodeValue(unit, x)).FirstOrDefault();
                    value = 30 + value - CampaignCodeValue(unit, old);
                }
                else if (lower > 0) value = 30 + value - CampaignCodeValue(unit, lower) * .5f;
                else if (_campaignInventoryAware && unit.RewardCodeSlots.All(x => x > 0))
                    value -= unit.RewardCodeSlots.Min(x => CampaignCodeValue(unit, x));
                if (value > score) { score = value; best = unit; replace = old; }
            }
            return (best, replace, score);
        }
        private static Unit CampaignTarget(RewardDef reward)
        {
            if (reward.IsCodeReward) return CampaignCodeChoice(reward.codeId).target;
            return Managers.UI.Screens.RewardScreen.TargetCandidates(reward)
                .OrderBy(u => reward.IsLevelGrant || reward.IsExpGrant ? u.Level : u.HpCurr / (float)Mathf.Max(1, u.HpMax)).FirstOrDefault();
        }
        private static float CampaignRewardScore(RewardDef reward)
        {
            if (reward == null || reward.RequiresTargetSelection && CampaignTarget(reward) == null) return -1;
            var party = GridManager.Instance.heroList.Where(u => u != null && !u.IsEnemy && (u.ID > 0 || u.LastActiveId > 0)).ToList();
            if (reward.IsCodeReward) return CampaignCodeChoice(reward.codeId).score;
            if (reward.IsRevivalReward) return party.Any(u => !u.IsOnField) ? 100 : -1;
            if (reward.IsHealingReward) return party.Any(u => u.IsOnField && u.HpCurr < u.HpMax * .65f) ? 75 : 1;
            if (reward.IsLevelGrant || reward.IsExpGrant) return 44;
            if (reward.itemId == 5002) return GameManager.Instance.inventoryManager.FoodUnits < 40 ? 70 : 14;
            if (reward.shortRestChargeGrant > 0) return GameManager.Instance.ShortRestCharges < 2 ? 30 : 1;
            return 4;
        }
        // 현행 무료 준비 교환만 이용한다. 코드 생성·주입이나 상점/보상 비용 우회는 없다.
        private static void CampaignManageInventory()
        {
            if (!_campaignInventoryAware) return;
            var party = GridManager.Instance.heroList.Where(u => u != null && !u.IsEnemy && (u.ID > 0 || u.LastActiveId > 0)).ToList();
            for (int step = 0; step < 256; step++)
            {
                RewardCodeInventory.Drag best = null; Unit receiver = null; int slot = -1; float gain = .01f;
                foreach (Unit source in party)
                foreach (bool stored in new[] { false, true })
                {
                    var codes = (stored ? source.StoredRewardCodeIds : source.RewardCodeSlots).ToList();
                    for (int index = 0; index < codes.Count; index++)
                    {
                        int id = codes[index]; if (id <= 0) continue;
                        var drag = new RewardCodeInventory.Drag { Owner = source, Stored = stored, Index = index, CodeId = id };
                        foreach (Unit target in party)
                        for (int i = 0; i < Unit.RewardCodeSlotCount; i++)
                        {
                            int old = target.RewardCodeSlots[i];
                            float delta = CampaignCodeValue(target, id) - CampaignCodeValue(target, old);
                            if (!stored) delta += CampaignCodeValue(source, old) - CampaignCodeValue(source, id);
                            if (delta <= gain || !RewardCodeInventory.CanMove(drag, target, false, i)) continue;
                            gain = delta; best = drag; receiver = target; slot = i;
                        }
                    }
                }
                if (best == null || !RewardCodeInventory.TryMove(best, receiver, false, slot)) break;
            }
        }
        private void CampaignPreparation()
        {
            CauseCapture("prep_before");
            var game = GameManager.Instance;
            if (game.RoundManager.IsCurrentRestStage) game.TryLongRest();
            CampaignFailurePreparation();
            CampaignManageInventory();
            game.inventoryManager.SellAllValuables();
            var party = GridManager.Instance.heroList.Where(u => u != null && !u.IsEnemy && (u.ID > 0 || u.LastActiveId > 0)).ToList();
            bool dead = party.Any(u => !u.IsOnField), hurt = party.Any(u => u.IsOnField && u.HpCurr < u.HpMax * .65f);
            if (dead) game.TryLongRest();
            else if (hurt && game.ShortRestCharges > 0) game.TryShortRest();
            else if (hurt) game.TryLongRest();
            var supplyRandom = new System.Random(game.RoundManager.Stage * 7919);
            int supplyCount = (int)typeof(Managers.UI.Screens.ShopScreen).GetField("SupplyOnDisplay", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            var supplies = RewardManager.Instance.BuildShopItems().OrderBy(_ => supplyRandom.Next()).Take(supplyCount);
            var stock = RewardManager.Instance.BuildShopGoods().Concat(supplies)
                .Concat((List<RewardDef>)typeof(Managers.UI.Screens.ShopScreen).GetMethod("BuildCodeStock", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null))
                .OrderByDescending(CampaignRewardScore).ToList();
            foreach (var reward in stock)
            {
                if (game.ShopPurchasesLeft <= 0) break;
                if (CampaignRewardScore(reward) < 35) continue;
                int price = RewardManager.ShopPrice(reward, game.RoundManager.Stage);
                if (price <= 0 || price > game.inventoryManager.Gold) continue;
                Unit target = reward.RequiresTargetSelection ? CampaignTarget(reward) : null;
                int replace = reward.IsCodeReward ? CampaignCodeChoice(reward.codeId).replace : 0;
                // 일회 생존기를 전원에게 반복 구매해 식량/성장을 굶기지 않는다. 자연 보상 수령은 별도다.
                if (_campaignDotRewardStudy && reward.codeId == 638 && (target?.ID != 167 || game.inventoryManager.FoodUnits < GameManager.LongRestFoodCost)) continue;
                if (!game.inventoryManager.TrySpendGold(price)) continue;
                if (!RewardManager.Instance.ApplyRewardEffect(reward, target, replace)) { game.inventoryManager.AddGold(price); continue; }
                game.NotifyShopPurchase();
                CampaignManageInventory();
            }
            // 식량을 이번 준비에서 구매했으면 아직 쓰지 않은 긴 휴식을 다시 시도할 수 있다.
            if (dead) game.TryLongRest();
            else if (hurt && game.ShortRestCharges == 0) game.TryLongRest();
            if (!string.IsNullOrEmpty(_failurePreparationPolicy) && game.CanRest && hurt) game.TryShortRest();
            CampaignManageInventory();
            CauseCapture("prep_after");
        }

        private IEnumerator ScenarioCompositionCampaign(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            _campaignInventoryAware = config.trigger != "legacy_policy";
            _campaignSummonRewardStudy = config.trigger is "summon_rewards_old" or "summon_rewards_new";
            _campaignDotRewardStudy = _campaignSummonRewardStudy || config.trigger is "dot_rewards_old" or "dot_rewards_new" or "failure_branches";
            var hiddenNewCodes = new Dictionary<CodeCatalog.Entry, int>();
            if (config.trigger == "summon_rewards_old")
                foreach (int id in SummonIdeaRewardCode.Ids)
                { var entry = RewardCodeCatalog.Find(id); if (entry != null) { hiddenNewCodes[entry] = entry.tier; entry.tier = 0; } }
            if (config.trigger == "dot_rewards_old")
                foreach (int id in SecretSupportRewardCode.Ids)
                { var entry = RewardCodeCatalog.Find(id); if (entry != null) { hiddenNewCodes[entry] = entry.tier; entry.tier = 0; } }
            var catalog = CampaignBuildCatalog();
            if (config.trigger == "catalog") { OsFinish(true); yield break; }
            int cap = config.stage > 0 ? config.stage : 96;
            var fixedFloors = new HashSet<int>(GameManager.Instance.dataManager.FetchStageThemeDataList().fixedBossStages.Select(x => x.stage));
            int attempts = config.partyLevel > 0 ? config.partyLevel : 5; // 이 시나리오만 반복 수이며 레벨을 주입하지 않는다.
            var specs = string.IsNullOrEmpty(config.lineup) || !CampaignLineups.ContainsKey(config.lineup) ? catalog.teams : new List<CampaignSpec> { CampaignLineups[config.lineup] };
            if (config.lineup == "sabah_overgrowth")
                specs = catalog.teams.Where(spec => spec.aliases.Contains("사바흐/best")).ToList();
            if (config.lineup?.StartsWith("b48_") == true)
            {
                specs = catalog.teams.Where(spec => spec.aliases.Contains("검증 편성/" + config.lineup)).ToList();
                if (specs.Count != 1) throw new InvalidOperationException("검증 편성 별칭을 하나로 찾지 못함: " + config.lineup);
            }
            if (config.trigger is "pilot" or "probe96" or "realtime" or "visible") specs = catalog.teams.Take(1).ToList();
            if (config.trigger?.StartsWith("shard:") == true)
            {
                var parts = config.trigger.Split(':'); int index = int.Parse(parts[1]), count = int.Parse(parts[2]);
                specs = catalog.teams.Where((_, i) => i % count == index).ToList();
            }
            // 피해 분담자의 열만 바꾸는 대조 실험. 실제 추천 데이터와 기본 측정에는 영향을 주지 않는다.
            if (config.trigger == "ebisu_rear")
            {
                foreach (var spec in specs.Where(spec => spec.ids.Contains(163)))
                {
                    int front = 0, rear = 0; int[] order = { 2, 3, 1, 4 };
                    for (int i = 0; i < spec.ids.Length; i++)
                    {
                        if (spec.ids[i] == 163) spec.xs[i] = -2;
                        spec.ys[i] = order[spec.xs[i] == -1 ? front++ : rear++];
                    }
                }
                File.WriteAllText(Path.Combine(_requestDirectory, "catalog.json"), JsonUtility.ToJson(catalog, true));
            }
            float oldCapture = Time.captureDeltaTime; int oldSync = QualitySettings.vSyncCount, oldLimit = Application.targetFrameRate;
            float oldFixed = Time.fixedDeltaTime;
            var hiddenCanvases = new HashSet<Canvas>();
            LogType oldFilter = Debug.unityLogger.filterLogType;
            // 전투 콜백은 그대로 실행하고, 캡처용 고정 프레임 시계로 벽시계 대기만 줄인다.
            Time.captureDeltaTime = config.trigger == "realtime" ? 0 : 1f / 30f;
            // Scripts에 FixedUpdate 전투 판정은 없다. 물리 연출용 반복만 줄이고 모든 행동 콜백은 유지한다.
            Time.fixedDeltaTime = .2f;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Debug.unityLogger.filterLogType = LogType.Warning;
            ActionScheduler.AnyActionStarted -= OnActionStarted; AudioManager.SfxEventRecorded -= OnSfxEvent;
            Effects.Vfx.VfxComposer.SlotPlayed -= OnVfxSlotPlayed;
            VerifyEvents.Raised -= OnVerifyEvent;
            try
            {
                foreach (var spec in specs)
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    int seed = 6101600 + attempt;
                    CauseRunStart(spec.key, attempt);
                    string runKey = spec.key + "/" + attempt;
                    string doneFile = Path.Combine(_requestDirectory, runKey.Replace('/', '_') + ".done");
                    if (File.Exists(doneFile)) continue;
                    UnityEngine.Random.InitState(seed);
                    SaveSystem.ResetDebugStorage();
                    var game = GameManager.Instance;
                    game.enabled = true; DebugMode.SuiteRunning = true; DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false; DebugMode.ForcedThemeId = 0;
                    game.KillCount = 0; game.inventoryManager.Initialize();
                    game.RestorePreparationState(false, GameManager.MaxShortRestCharges, 0);
                    RewardManager.Instance.ResetRewardSelection();
                    yield return BuildFreshRun(spec.key);
                    _campaignGateRoster = GridManager.Instance.heroList.Any(Combat.SpecialAction.CanOpen);
                    // 새 편성 준비 중 이미 추첨된 테마와 사건 캐시를 다음 런으로 넘기지 않는다.
                    // 실제 새 게임과 같은 RoundManager 초기 상태에서 시드 추첨을 시작한다.
                    typeof(GameManager).GetField("_roundManager", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(game, new RoundManager(game.dataManager));
                    UnityEngine.Random.InitState(seed);
                    game.DebugLoadStage(1);
                    Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(5);
                    var result = new CampaignRun { id = Guid.NewGuid().ToString("N"), team = spec.key, attempt = attempt, seed = seed, valid = true };
                    CampaignBattle battle = null;
                    CampaignFailureCheckpoint failureCheckpoint = null;
                    DotDiagnosisRow telemetry = null; DotDiagnosisRecorder studyRecorder = null;
                    Boss48Replay checkpointTraceRow = null; Boss48Recorder checkpointTrace = null;
                    float start = Time.realtimeSinceStartup, changed = start;
                    GameState priorState = game.gameState; int priorStage = -1; bool eventChosen = false;
                    var diagnosedKinds = new HashSet<string>();
                    while (true)
                    {
                        int floor = game.RoundManager.Stage;
                        if (game.gameState != priorState || floor != priorStage)
                        { changed = Time.realtimeSinceStartup; priorState = game.gameState; priorStage = floor; }
                        // 예약 사건이나 출전 거절을 전투로 세지 않고 실제 전투가 시작된 때만 계측한다.
                        if (battle == null && game.gameState == GameState.RoundInProgress)
                        {
                            result.reached = Mathf.Max(result.reached, floor);
                            StageKind kind = game.RoundManager.CurrentCombatKind;
                            bool fixedBoss = game.RoundManager.IsCurrentFixedBossStage;
                            battle = new CampaignBattle { run = result.id, team = spec.key, attempt = attempt, seed = seed, stage = floor,
                                theme = game.RoundManager.CurrentThemeId, stageKind = kind.ToString(),
                                formation = string.Join(";", AliveAllies().Select(u => u.ID + ":" + u.currentCell.xPos + ":" + u.currentCell.yPos)),
                                category = fixedBoss ? "FixedBoss" : kind is StageKind.Elite or StageKind.MidBoss ? "Elite" : kind == StageKind.Boss ? "Boss" : kind == StageKind.Event ? "Event" : "Normal",
                                enemies = string.Join(",", AliveEnemies().Select(u => u.ID)), meanLevel = AliveAllies().Select(u => (float)u.Level).DefaultIfEmpty().Average(),
                                party = string.Join(";", GridManager.Instance.heroList.Where(u => u != null && (u.ID > 0 || u.LastActiveId > 0)).Select(u => (u.ID > 0 ? u.ID : u.LastActiveId) + ":L" + u.Level + ":HP" + u.HpCurr + "/" + u.HpMax + ":C" + string.Join("/", u.RewardCodeSlots))) };
                        }
                        if (battle != null && game.gameState != GameState.RoundInProgress)
                        {
                            battle.av = game.ActionScheduler.CombatActionValueElapsed; battle.cycles = game.ActionScheduler.ReferenceCyclesElapsed;
                            battle.turns = game.ActionScheduler.TurnsTaken;
                            string text = CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text ?? "";
                            battle.won = text.Contains("전투 종료 — 승리"); battle.reason = text;
                            battle.survivors = AliveAllies().Count;
                            battle.damage = string.Join(";", DamageMeter.Current.Select(entry =>
                                (entry.Unit.ID > 0 ? entry.Unit.ID : entry.Unit.LastActiveId) + ":" + entry.Damage));
                            if (studyRecorder != null)
                            {
                                telemetry.cycles = battle.cycles; telemetry.won = battle.won;
                                telemetry.survivors = studyRecorder.EndSurvivors; telemetry.enemyHpFractionEnd = studyRecorder.EndEnemyFraction;
                                telemetry.party = battle.party; telemetry.enemies = battle.enemies;
                                CampaignWrite("telemetry", telemetry); studyRecorder.Dispose(); studyRecorder = null;
                            }
                            if (checkpointTrace != null)
                            {
                                checkpointTrace.Dispose(); checkpointTraceRow.finished = true;
                                checkpointTraceRow.damageMeter = battle.damage;
                                checkpointTraceRow.transcript = $"checkpoint_{result.id}_{battle.stage}_trace.json";
                                File.WriteAllText(Path.Combine(_requestDirectory, checkpointTraceRow.transcript), JsonUtility.ToJson(checkpointTrace.Transcript));
                                CampaignWrite("checkpoint_fights", checkpointTraceRow);
                                checkpointTrace = null; checkpointTraceRow = null;
                            }
                            CampaignWrite("battles", battle); result.battles++;
                            if (!battle.won && failureCheckpoint != null) CampaignSaveFailure(failureCheckpoint, battle, false);
                            if (battle.won) result.cleared = Mathf.Max(result.cleared, battle.stage);
                            else if (result.firstLoss == 0) result.firstLoss = battle.stage;
                            battle = null;
                        }
                        if (game.gameState == GameState.GameOver) { result.stop = "game_over"; break; }
                        if (game.gameState == GameState.Preparation && floor > cap) { result.capped = true; result.stop = "observation_cap"; break; }
                        if (Time.realtimeSinceStartup - changed > 180 || Time.realtimeSinceStartup - start > 3600)
                        { result.valid = false; result.stop = "harness_watchdog"; break; }
                        if (game.gameState == GameState.Preparation)
                        {
                            if (floor>1 && game.RoundManager.StageInRound==1)
                                CampaignWrite("theme_recoveries",new CampaignThemeRecovery { team=spec.key,attempt=attempt,stage=floor,
                                    theme=game.RoundManager.CurrentThemeId,food=game.inventoryManager.FoodUnits,shortRests=game.ShortRestCharges,
                                    allFull=AliveAllies().Count==spec.ids.Length && AliveAllies().All(u=>u.HpCurr==u.HpMax),
                                    resourcesReset=AliveAllies().All(u=>u.ManaCurr==(u.UltimateResourceType==UltimateResourceType.Passive?u.ManaMax:0)),
                                    party=string.Join(";",GridManager.Instance.heroList.Select(u=>$"{(u.ID>0?u.ID:u.LastActiveId)}:{u.HpCurr}/{u.HpMax}:M{u.ManaCurr}")) });
                            if (config.trigger == "failure_branches") failureCheckpoint = CampaignFailureCapture(result.id, spec.key, attempt, seed);
                            CampaignPreparation();
                            if (game.RoundManager.IsCurrentRestStage)
                            {
                                CampaignWrite("rests", new CampaignRest { team = spec.key, attempt = attempt, stage = floor,
                                    label = ProgressionLayout.Label(floor), free = game.RestUsedThisPreparation,
                                    alive = AliveAllies().Count, food = game.inventoryManager.FoodUnits });
                                game.StartRound();
                                yield return null;
                                continue;
                            }
                            if (AliveAllies().Count == 0)
                            {
                                if (failureCheckpoint != null) CampaignSaveFailure(failureCheckpoint, null, true);
                                result.stop = "party_exhausted"; break;
                            }
                            if (config.trigger == "dot_diagnose" && floor >= 17)
                            {
                                StageKind diagnosticKind = game.RoundManager.CurrentCombatKind;
                                string key = floor == 24 ? "FixedBoss" : diagnosticKind == StageKind.Normal ? "Normal" :
                                    diagnosticKind is StageKind.Elite or StageKind.MidBoss ? "Elite" : null;
                                if (key != null && diagnosedKinds.Add(key))
                                {
                                    yield return CampaignDotDiagnostic(spec.key, attempt, seed, key);
                                    changed = Time.realtimeSinceStartup;
                                }
                            }
                            if (_campaignDotRewardStudy)
                            {
                                if (floor == 24) File.WriteAllText(Path.Combine(_requestDirectory, $"snapshot_{spec.key}_{attempt}_24.json"), JsonUtility.ToJson(RunManager.Instance.CaptureDebugSnapshot()));
                                StageKind k = game.RoundManager.CurrentCombatKind;
                                string category = game.RoundManager.IsCurrentFixedBossStage ? "FixedBoss" : k is StageKind.Elite or StageKind.MidBoss ? "Elite" : k == StageKind.Boss ? "Boss" : k == StageKind.Event ? "Event" : "Normal";
                                telemetry = new DotDiagnosisRow { team = spec.key, attempt = attempt, seed = seed, stage = floor, category = category, profile = config.trigger };
                                studyRecorder = new DotDiagnosisRecorder(telemetry);
                            }
                            if (failureCheckpoint != null)
                            {
                                failureCheckpoint.afterPreparation = RunManager.Instance.CaptureDebugSnapshot();
                                failureCheckpoint.battleRandom = UnityEngine.Random.state;
                            }
                            if (game.RoundManager.IsCurrentFixedBossStage)
                            {
                                checkpointTraceRow = new Boss48Replay { origin = "natural", team = spec.key, attempt = attempt,
                                    profile = config.trigger, initialAlive = AliveAllies().Count, telemetry = telemetry };
                                checkpointTrace = new Boss48Recorder(checkpointTraceRow);
                            }
                            CauseBeginFight(); game.StartRound(); studyRecorder?.SealEnemyBudget(); checkpointTrace?.Seal();
                            if (config.trigger != "visible")
                                foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                    if (canvas.enabled) { hiddenCanvases.Add(canvas); canvas.enabled = false; }
                        }
                        else if (game.gameState == GameState.RewardSelection)
                        {
                            var choices = (List<RewardDef>)typeof(RewardManager).GetField("_selection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(RewardManager.Instance);
                            bool claimed = false;
                            // 실제 지급이 거절된 카드는 소비하지 않고 다음 합법 후보를 고른다.
                            foreach (var reward in (choices ?? new List<RewardDef>()).Where(r => RewardManager.Instance.CanClaimReward(r)).OrderByDescending(CampaignRewardScore))
                            {
                                Unit target = reward.RequiresTargetSelection ? CampaignTarget(reward) : null;
                                int replace = reward.IsCodeReward && target != null ? CampaignCodeChoice(reward.codeId).replace : 0;
                                if (reward.IsCodeReward && target == null)
                                {
                                    // 모든 선택지가 낮은 점수여도 실제 수령 가능한 대상은 선택할 수 있다.
                                    var fallback = CampaignCodeChoice(reward.codeId, true);
                                    target = fallback.target; replace = fallback.replace;
                                    CampaignWrite("reward_fallbacks", new CampaignRewardRejection { team = spec.key, attempt = attempt, stage = game.RoundManager.Stage, reward = reward.displayName, code = reward.codeId, target = target?.ID ?? 0 });
                                }
                                if (RewardManager.Instance.TryClaimReward(reward, target, replace)) { claimed = true; break; }
                                CampaignWrite("reward_rejections", new CampaignRewardRejection { team = spec.key, attempt = attempt, stage = game.RoundManager.Stage, reward = reward.displayName, code = reward.codeId, target = target?.ID ?? 0 });
                            }
                            if (!claimed) { result.valid = false; result.stop = "reward_rejected"; break; }
                            // 만사의 구매권도 실제 지불·잔여 카드 제한을 거친다.
                            foreach (var extra in choices.Where(r => RewardManager.Instance.CanClaimReward(r)).OrderByDescending(CampaignRewardScore).ToList())
                            {
                                if (CampaignRewardScore(extra) < 35) continue;
                                var extraTarget = extra.RequiresTargetSelection ? CampaignTarget(extra) : null;
                                RewardManager.Instance.TryClaimReward(extra, extraTarget, extra.IsCodeReward ? CampaignCodeChoice(extra.codeId).replace : 0);
                            }
                            RewardManager.Instance.FinishRewardSelection(); eventChosen = false;
                        }
                        else if (game.gameState == GameState.EventStage)
                        {
                            game.SkipEventDialogue();
                            var ev = (StageEventData)typeof(GameManager).GetField("currentStageEvent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                            if (!eventChosen && ev != null)
                            {
                                var choice = ev.choices?.FirstOrDefault(c => c.battleEnemyId == 0 && c.grantUnitId == 0 && c.goldCostPerStage * floor <= game.inventoryManager.Gold);
                                if (choice != null) game.SelectEventChoice(choice.id);
                                eventChosen = true; result.events++;
                            }
                            game.CompleteEventStage();
                            if (game.gameState != GameState.EventStage) eventChosen = false;
                        }
                        yield return null;
                    }
                    studyRecorder?.Dispose();
                    checkpointTrace?.Dispose();
                    result.wallSeconds = Time.realtimeSinceStartup - start;
                    CampaignWrite("runs", result);
                    if (result.valid) File.WriteAllText(doneFile, JsonUtility.ToJson(result));
                    File.WriteAllText(Path.Combine(_requestDirectory, "progress.json"), JsonUtility.ToJson(result));
                    YvCheck("자연 진행 " + runKey, "런 종료 또는 관찰 상한·측정 성공", JsonUtility.ToJson(result), result.valid && result.battles > 0);
                    game.DebugResetBattle();
                    foreach (Canvas canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
                    hiddenCanvases.Clear();
                }
            }
            finally
            {
                foreach (var entry in hiddenNewCodes) entry.Key.tier = entry.Value;
                _campaignGateRoster = null;
                _campaignDotRewardStudy = false;
                _campaignSummonRewardStudy = false;
                Time.captureDeltaTime = oldCapture; QualitySettings.vSyncCount = oldSync; Application.targetFrameRate = oldLimit;
                Time.fixedDeltaTime = oldFixed;
                foreach (Canvas canvas in hiddenCanvases) if (canvas != null) canvas.enabled = true;
                Debug.unityLogger.filterLogType = oldFilter;
                ActionScheduler.AnyActionStarted += OnActionStarted; AudioManager.SfxEventRecorded += OnSfxEvent;
                Effects.Vfx.VfxComposer.SlotPlayed += OnVfxSlotPlayed; VerifyEvents.Raised += OnVerifyEvent;
                DebugMode.SuiteRunning = true;
            }
            OsFinish(true);
        }
    }
}
#endif
