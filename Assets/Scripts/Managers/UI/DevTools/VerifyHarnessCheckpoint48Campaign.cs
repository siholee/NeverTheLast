#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core;
using Codes.Checkpoints;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static Unit B48RewardTarget(RewardDef reward)
        {
            var candidates = reward.IsCodeReward ? GridManager.Instance.heroList.Where(u => u != null && u.ID > 0 &&
                u.CanReceiveRewardCode(reward.codeId, out int lower, out _, out _) && (u.FreeRewardCodeSlots > 0 || lower > 0)).ToList() :
                Managers.UI.Screens.RewardScreen.TargetCandidates(reward).ToList();
            if (reward.IsCodeReward)
                return candidates.OrderByDescending(u => B48Build(u, "treasury").Contains(reward.codeId) ? 1 : 0)
                    .ThenByDescending(u => u.FreeRewardCodeSlots).FirstOrDefault();
            if (reward.IsRevivalReward) return candidates.FirstOrDefault(u => !u.IsOnField);
            return candidates.OrderBy(u => u.HpCurr / (float)Mathf.Max(1, u.HpMax)).FirstOrDefault();
        }

        private static float B48RewardScore(RewardDef reward)
        {
            if (reward == null || reward.RequiresTargetSelection && B48RewardTarget(reward) == null) return -1;
            var party = GridManager.Instance.heroList.Where(u => u != null && u.ID > 0).ToList();
            if (reward.IsRevivalReward && party.Any(u => !u.IsOnField)) return 100;
            if (reward.IsCodeReward)
            {
                var target = B48RewardTarget(reward);
                if (target == null || !target.CanReceiveRewardCode(reward.codeId, out _, out _, out _)) return -1;
                return (B48Build(target, "treasury").Contains(reward.codeId) ? 60 : 12) + target.FreeRewardCodeSlots;
            }
            if (reward.IsHealingReward && party.Any(u => u.IsOnField && u.HpCurr < u.HpMax * .5f)) return 75;
            if (reward.IsLevelGrant || reward.IsExpGrant) return 40;
            if (reward.itemId == 5002) return 30;
            return 5;
        }

        private IEnumerator ScenarioCheckpoint48Campaign(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return WaitForGameReady(8);
            var game = GameManager.Instance;
            game.life = 20; game.KillCount = 0;
            game.inventoryManager.Initialize();
            game.RestorePreparationState(false, GameManager.MaxShortRestCharges, 0);
            yield return BuildFreshRun(ResolveLineup(config));
            yield return PrepareStage(config, 1, false, false, false, 4);
            game.DebugResetBattle(); DebugMode.ForcedThemeId = 2; game.DebugLoadStage(1); DebugMode.ForcedThemeId = 0;
            Managers.UI.HUD.BattleHud.TrySetSpeedMultiplier(Mathf.Max(1f, config.timeScale));
            UnityEngine.Random.InitState(config.stage > 0 ? config.stage : 4801);
            DebugMode.SuiteRunning = false;
            int prior = 0, battles = 0, events = 0;
            bool switched = false, passed48 = false;
            float deadline = Time.realtimeSinceStartup + 1800;
            while (game.RoundManager.Stage < 49 && game.life > 0 && game.gameState != GameState.GameOver && Time.realtimeSinceStartup < deadline)
            {
                if (game.gameState == GameState.Preparation)
                {
                    int floor = game.RoundManager.Stage;
                    if (floor == 41 && !switched)
                    {
                        game.DebugResetBattle(); DebugMode.ForcedThemeId = 16; game.DebugLoadStage(41);
                        DebugMode.ForcedThemeId = 0; switched = true;
                    }
                    if (prior != floor)
                    {
                        prior = floor;
                        var party = GridManager.Instance.heroList.Where(u => u != null && u.ID > 0).ToList();
                        bool dead = party.Any(u => !u.IsOnField);
                        bool hurt = party.Any(u => u.IsOnField && u.HpCurr < u.HpMax * .65f);
                        if (dead || hurt && game.ShortRestCharges == 0) game.TryLongRest();
                        else if (hurt) game.TryShortRest();
                        var shop = RewardManager.Instance.BuildShopGoods().Concat(RewardManager.Instance.BuildShopItems())
                            .OrderByDescending(B48RewardScore).ToList();
                        foreach (var good in shop)
                        {
                            if (game.ShopPurchasesLeft <= 0) break;
                            if (B48RewardScore(good) < 30) continue;
                            int price = RewardManager.ShopPrice(good, floor);
                            if (price <= 0 || price > game.inventoryManager.Gold) continue;
                            var target = good.RequiresTargetSelection ? B48RewardTarget(good) : null;
                            // 실제 상점과 같은 유효성 확인·지불·효과·구매 횟수 경로.
                            if (!game.inventoryManager.TrySpendGold(price)) continue;
                            if (!RewardManager.Instance.ApplyRewardEffect(good, target)) { game.inventoryManager.AddGold(price); continue; }
                            game.NotifyShopPurchase();
                        }
                        if (floor is 45 or 47 or 48)
                        {
                            int expected = floor == 45 ? 12050 : floor == 47 ? 13060 : Checkpoint48.BossId;
                            RuiCheck("자연 진행 " + floor + "층 배치", expected.ToString(), string.Join(',', AliveEnemies().Select(u => u.ID)),
                                AliveEnemies().Any(u => u.ID == expected));
                            File.AppendAllText(Path.Combine(_requestDirectory, "checkpoint48-campaign.txt"),
                                $"entry={floor},life={game.life},gold={game.inventoryManager.Gold},party={string.Join(',', party.Select(u => $"{u.ID}:L{u.Level}:HP{u.HpCurr}/{u.HpMax}:codes{string.Join('/', u.RewardCodeSlots)}"))}" + System.Environment.NewLine);
                            yield return WaitUnscaled(1.2f); yield return CaptureStill("checkpoint48_natural_entry_" + floor);
                        }
                    }
                    game.StartRound();
                }
                else if (game.gameState == GameState.RoundEnd)
                {
                    var button = ReleaseResultContinue();
                    if (button != null)
                    {
                        bool won = Combat.CombatLog.Current.LastOrDefault(e => e.Text.StartsWith("전투 종료 — ")).Text?.Contains("전투 종료 — 승리") == true;
                        File.AppendAllText(Path.Combine(_requestDirectory, "checkpoint48-campaign.txt"),
                            $"floor={game.RoundManager.Stage},won={won},life={game.life},levels={string.Join(',', AliveAllies().Select(u => $"{u.ID}:{u.Level}"))}" + System.Environment.NewLine);
                        if (game.RoundManager.Stage == 48) passed48 = won;
                        battles++; button.onClick.Invoke(); yield return WaitUnscaled(.12f);
                    }
                }
                else if (game.gameState == GameState.RewardSelection)
                {
                    var choices = (List<RewardDef>)typeof(RewardManager).GetField("_selection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(RewardManager.Instance);
                    var reward = choices.Where(r => B48RewardScore(r) >= 0).OrderByDescending(B48RewardScore).FirstOrDefault();
                    if (reward == null || !RewardManager.Instance.TryClaimReward(reward, reward.RequiresTargetSelection ? B48RewardTarget(reward) : null)) break;
                    RewardManager.Instance.FinishRewardSelection(); yield return WaitUnscaled(.12f);
                }
                else if (game.gameState == GameState.EventStage)
                {
                    events++; game.SkipEventDialogue();
                    var ev = (StageEventData)typeof(GameManager).GetField("currentStageEvent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);
                    if (ev != null)
                    {
                        var choice = ev.choices.FirstOrDefault(c => c.battleEnemyId == 0 && c.grantUnitId == 0);
                        if (choice != null) game.SelectEventChoice(choice.id);
                        game.CompleteEventStage();
                    }
                    yield return WaitUnscaled(.12f);
                }
                yield return null;
            }
            RuiCheck("1→48 자연 성장·실제 보상", "49층 진입·48층 승리·노르드 연작", $"floor={game.RoundManager.Stage},theme={game.RoundManager.CurrentThemeId},battles={battles},events={events},life={game.life}",
                passed48 && game.RoundManager.Stage == 49 && game.RoundManager.CurrentThemeId == 17 && game.life > 0);
            DebugMode.SuiteRunning = true; DebugMode.ForcedThemeId = 0; game.DebugResetBattle(); RuiFinish();
        }
    }
}
#endif
