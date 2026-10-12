#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using Managers.UI.Screens;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioToughnessAbsence(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return BuildFreshRun("party5");
            yield return PrepareStage(config, 1, false, false, false);
            var game = GameManager.Instance;
            var pool = game.rewardManager.BuildCodePool();
            var stock = (List<RewardDef>)typeof(ShopScreen).GetMethod("BuildCodeStock", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            RuiCheck("공급자 없는 파티 전용 보상 제외", "633·731 제외", string.Join(",", pool.Select(x => x.codeId)), !pool.Any(x => x.codeId == 633 || x.codeId == 731));
            RuiCheck("공급자 없는 파티 전용 상점 제외", "633·731 제외", string.Join(",", stock.Select(x => x.codeId)), !stock.Any(x => x.codeId == 633 || x.codeId == 731));
            RuiCheck("일반 처치 보상은 범용 후보 유지", "717·822·823 있음", "", new[]{717,822,823}.All(id=>pool.Any(x=>x.codeId==id)&&stock.Any(x=>x.codeId==id)));
            game.StartRound();
            yield return WaitUnscaled(.3f);
            RuiCheck("오르페우스 없는 전투 강인도 없음", "전원0", "", AliveEnemies().All(u=>u.ToughnessMax==0));
            yield return CaptureStill("without_toughness");
            RuiFinish();
        }

        private IEnumerator ScenarioToughnessEdges(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return BuildFreshRun("party5_toughness");
            yield return PrepareStage(config, 8, false, false, false);
            var game = GameManager.Instance;
            Unit orpheus = AliveAllies().First(u => u.ID == 25), shi = AliveAllies().First(u => u.ID == 2), anubis = AliveAllies().First(u => u.ID == 121);
            Unit enemy = AliveEnemies().First();
            void Check(string name, bool ok, string actual = "") => RuiCheck(name, "사양 일치", actual, ok);
            var ids = new[] { 633, 731, 717, 822, 823 };
            Check("재분류717 보존·중복 파죽지세 없음", RewardCodeCatalog.TierOf(717) == 4 &&
                RewardCodeCatalog.All().Count(e => e.verbalName == "파죽지세") == 1);
            Check("실제 보상 풀 신규5종", ids.All(id => game.rewardManager.BuildCodePool().Any(r => r.codeId == id)));
            var stockMethod = typeof(ShopScreen).GetMethod("BuildCodeStock", BindingFlags.Static | BindingFlags.NonPublic);
            var stock = (List<RewardDef>)stockMethod.Invoke(null, null);
            Check("실제 상점 신규5종", ids.All(id => stock.Any(r => r.codeId == id)));
            foreach (int id in ids)
                Check("상점 " + id + " 티어 가격", RewardManager.ShopPrice(RewardManager.BuildCodeReward(RewardCodeCatalog.Find(id)), 2) ==
                    RewardManager.TierBasePrice(RewardCodeCatalog.TierOf(id)) * 2);

            game.inventoryManager.RestoreGold(20000);
            game.OpenShopFromPreparation();
            yield return WaitUnscaled(.6f);
            GameObject shopRoot = GameObject.Find("ShopScreen");
            var seenStock = new HashSet<int>();
            for (int page = 0; page < 3; page++)
            {
                for (int i = page*3; i < Mathf.Min(stock.Count,(page+1)*3); i++)
                    if (RuiFind(shopRoot,"CodeGood_"+i)!=null) seenStock.Add(stock[i].codeId);
                RuiReadable("강인도 상점 페이지"+page, shopRoot);
                yield return CaptureStill("toughness_shop_page_"+page);
                if(page<2) { RuiClick(RuiFind(shopRoot,"CodePageNext")); yield return WaitUnscaled(.3f); }
            }
            Check("상점8종 페이지 접근", stock.Count==8 && seenStock.Count==8);
            RuiClick(RuiFind(shopRoot,"CodePagePrevious")); yield return WaitUnscaled(.3f);
            RuiReadable("강인도 상점", shopRoot);
            yield return CaptureStill("toughness_shop_stock");
            int stockIndex = stock.FindIndex(r=>r.codeId==717);
            RuiClick(RuiFind(shopRoot,"CodeGood_"+stockIndex));
            yield return WaitUnscaled(.25f);
            RuiClick(RuiTargetCard(shopRoot,shi));
            yield return WaitUnscaled(.4f);
            Check("신규T4 실제 상점 구매", shi.RewardCodeIds.Contains(717) && game.ShopPurchaseCount==1 && game.inventoryManager.Gold==20000-600*8);
            game.uiManager.HideShopPanel();

            bool resolved = false, once = false, perAlly = false, largeHit = false;
            game.StartRound();
            game.ActionScheduler.EnqueueSpecial(shi, "verify_toughness_budget", "강인도 다단 검증", () =>
            {
                enemy.ClearToughness(); enemy.GrantToughness(150, orpheus); enemy.AddShield(100000, orpheus);
                DamageContext Hit(Unit actor, int raw = 1) => new(actor, raw, CodeType.Normal, new() { DamageTag.NormalAttack, DamageTag.SingleTarget });
                for (int n = 0; n < 3; n++) enemy.TakeDamage(Hit(shi));
                once = enemy.ToughnessCurr == 120;
                enemy.TakeDamage(Hit(anubis));
                perAlly = enemy.ToughnessCurr == 90;
                enemy.TakeDamage(Hit(shi, 10000));
                largeHit = enemy.ToughnessCurr == 90;
                resolved = true;
            });
            float until = Time.realtimeSinceStartup + 10f;
            while (!resolved && Time.realtimeSinceStartup < until) yield return null;
            Check("실제 스케줄러 다단1회 삭감", resolved && once);
            Check("동일 행동 다른 아군 별도 삭감", perAlly);
            Check("다단 추가 큰 피해가 삭감량을 증폭하지 않음", largeHit);
            yield return CaptureStill("toughness_multihit_budget");
            game.DebugResetBattle();

            yield return PrepareStage(config, 8, false, false, false);
            enemy = AliveEnemies().First();
            orpheus = AliveAllies().First(u => u.ID == 25);
            shi = AliveAllies().First(u => u.ID == 2);
            anubis = AliveAllies().First(u => u.ID == 121);
            orpheus.CastPassiveCode();
            int virtualEvents = 0;
            void OnBreak(CombatDefeatContext _) => virtualEvents++;
            Toughness.Broken += OnBreak;
            try
            {
                ((OrpheusCheatedDeath)orpheus.ActivePassiveCodesResolved().First(p => p is OrpheusCheatedDeath)).StopCode();
                Check("공급자 종료는 격파가 아님", enemy.ToughnessMax == 0 && virtualEvents == 0);
                orpheus.CastPassiveCode();
                enemy.ClearToughness(); enemy.Invoke(UnitEventType.OnRoundStart, new EventContext(enemy));
                Check("늦은 적 개전에도 생성", enemy.ToughnessMax > 0);
                orpheus.TakeDamage(new DamageContext(enemy, orpheus.HpMax * 100, CodeType.Normal, new() { DamageTag.NormalAttack }));
                Check("실제 공급자 사망은 강인도 판정 없이 제거", !orpheus.isActive && AliveEnemies().All(u => u.ToughnessMax == 0) && virtualEvents == 0);
            }
            finally { Toughness.Broken -= OnBreak; }
            // 죽은 아군도 편성에는 남으므로 캐릭터 키트 유무로 보상 후보를 판정한다.
            Check("실제 처치가 기본 경제를 보존", game.inventoryManager.Gold >= 500);
            Check("허허실실 장착", shi.TryGrantRewardCode(633)); shi.CastPassiveCode();
            int slot = shi.RewardCodeSlots.ToList().IndexOf(633);
            Check("새 코드 보관 이동", RewardCodeInventory.TryMove(new RewardCodeInventory.Drag { Owner = shi, Index = slot, CodeId = 633 }, shi, true));
            Check("보관 코드 효율 없음", Mathf.Abs(Toughness.Efficiency(shi) - 1f) < .001f);
            int index = shi.StoredRewardCodeIds.ToList().IndexOf(633);
            Check("새 코드 아군 교환", RewardCodeInventory.TryMove(new RewardCodeInventory.Drag { Owner = shi, Stored = true, Index = index, CodeId = 633 }, anubis, false, 0));
            anubis.CastPassiveCode();
            Check("교환 후 새 보유자 효율", Mathf.Abs(Toughness.Efficiency(anubis) - 1.25f) < .001f && Mathf.Abs(Toughness.Efficiency(shi) - 1f) < .001f);
            int gold = game.inventoryManager.Gold;
            var holding = anubis.RewardCodeSlotInstances.First(h => h != null && h.codeId == 633);
            Check("새 코드 판매 정산", RewardCodeInventory.TrySell(anubis, holding.instanceId, out int proceeds) &&
                proceeds > 0 && game.inventoryManager.Gold == gold + proceeds && !anubis.RewardCodeIds.Contains(633));
            Check("판매 후 효율 제거", Mathf.Abs(Toughness.Efficiency(anubis) - 1f) < .001f);
            foreach (int id in ids) shi.TryGrantRewardCode(id);
            var save = new UnitSaveData(); shi.WriteRunState(save); shi.RestoreRunState(save);
            Check("5종 실제 저장복원·717 호환", ids.All(id => shi.RewardCodeIds.Contains(id)));
            yield return CaptureStill("toughness_inventory_roundtrip");
            RuiFinish();
        }
    }
}
#endif
