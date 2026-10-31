#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BaseClasses.BaseEnums;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioRunFailure(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            var spec=CampaignBuildCatalog().teams.Single(x=>x.aliases.Contains("검증 편성/b48_lokapala_surya"));
            var game=GameManager.Instance;var run=RunManager.Instance;
            void Check(string name,bool ok,string actual="")=>YvCheck("전멸 종료 "+name,"생명력 없음·첫 전멸 종료·저장/진행 차단",actual,ok);
            foreach(int stage in new[]{17,34})
            {
                yield return BuildFreshRun(spec.key);game.DebugLoadStage(stage);
                DebugMode.AllyInvincible=false;DebugMode.EnemyInvincible=true;DebugMode.SuiteRunning=true;
                game.enabled=true;
                // v16 JSON의 남은 생명은 무시하되 나머지 저장을 회복/재추첨 없이 이관한다.
                Unit savedHero=AliveAllies().First();savedHero.ModifyHp(savedHero.HpMax/2);int savedHp=savedHero.HpCurr;
                var snapshot=run.CaptureDebugSnapshot();string old=JsonUtility.ToJson(snapshot).Replace("\"version\":17","\"version\":16,\"life\":1");
                SaveSystem.SaveRun(snapshot);
                typeof(SaveSystem).GetMethod("WriteString",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null,new object[]{"NTL_RunSave",old});
                var loaded=SaveSystem.LoadRun();
                Check("v16 호환·HP/재화/좌표 보존 "+stage,loaded.version==RunSaveData.CurrentVersion &&
                    loaded.currentStage==stage && loaded.gold==snapshot.gold &&
                    JsonUtility.ToJson(loaded.heroUnits[0])==JsonUtility.ToJson(snapshot.heroUnits[0]) && !JsonUtility.ToJson(loaded).Contains("\"life\""));
                run.RestoreDebugSnapshot(loaded);
                Check("이어하기 무료 회복 없음 "+stage,AliveAllies().First().HpCurr==savedHp);
                game.StartRound();Unit foe=AliveEnemies().First();
                var allies=AliveAllies().ToList();
                void Kill(Unit unit)
                {
                    foreach(var passive in unit.ActivePassiveCodes.ToList())passive.StopCode();
                    foreach(var status in unit.GetAllStatuses())unit.RemoveStatusByKey(status.Key);
                    unit.RemoveShield(unit.ShieldCurr);
                    for(int hit=0;hit<4 && unit.isActive;hit++)
                        unit.TakeDamage(new DamageContext(foe,10000000,CodeType.Effect,new(){DamageTag.TrueDamage},false));
                }
                Kill(allies[0]);yield return WaitUnscaled(.1f);
                Check("개별 전투불능은 계속 진행 "+stage,game.gameState==GameState.RoundInProgress && run.RunActive);
                foreach(Unit ally in allies.Skip(1))Kill(ally);
                float deadline=Time.realtimeSinceStartup+10f;
                while(game.gameState!=GameState.GameOver && Time.realtimeSinceStartup<deadline)yield return null;
                Check("첫 전멸 즉시 런 종료 "+stage,game.gameState==GameState.GameOver && !run.RunActive && !SaveSystem.HasSave() && !run.CanSaveNow);
                int ended=game.RoundManager.Stage;run.AdvanceToNextStage();run.SaveCurrentRun();game.RecoverPartyForThemeTransition();game.StartRound();
                Check("패배 뒤 다음층·휴식·저장·재출격 불가 "+stage,game.gameState==GameState.GameOver && game.RoundManager.Stage==ended &&
                    !game.TryLongRest() && !SaveSystem.HasSave() && AliveAllies().Count==0);
            }
            yield return BuildFreshRun(spec.key);game.DebugLoadStage(17);game.StartRound();game.enabled=false;
            foreach(Unit ally in AliveAllies().ToList())ally.Die(null);
            GridManager.Instance.ClearActiveEnemies();game.EndRoundByEnemyDefeat();game.enabled=true;
            Check("양측 전멸도 아군 패배",game.gameState==GameState.GameOver && !run.RunActive && !SaveSystem.HasSave());
            Check("런 생명력 데이터 제거",typeof(GameManager).GetField("life")==null && typeof(RunSaveData).GetField("life")==null &&
                GameObject.Find("LifeIcon")==null && GameObject.Find("Life")==null);
            game.DebugResetBattle();DebugMode.EnemyInvincible=false;OsFinish(true);
        }

        private IEnumerator ScenarioCheckpointProgression(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, v => enabled = v);
            var game = GameManager.Instance; var run = RunManager.Instance;
            void Check(string label, bool ok, string actual = "") => YvCheck("고정보스 추가 진행 " + label, "8칸 보존·4테마 주기·무료 휴식", actual, ok);
            DebugMode.ForcedThemeId = 2;
            run.StartRun(false);
            bool mapping = true;
            for (int stage = 1; stage <= 1020; stage++)
                mapping &= ProgressionLayout.StageOf(ProgressionLayout.ThemeOf(stage), ProgressionLayout.SlotOf(stage)) == stage;
            Check("1020개 누적 위치 왕복", mapping);
            foreach (int theme in new[] { 3, 4, 5, 8, 12 })
            {
                for (int slot = 1; slot <= 8; slot++)
                {
                    game.DebugLoadStage(ProgressionLayout.StageOf(theme, slot));
                    Check($"{theme}-{slot} 기본 내용 보존", game.RoundManager.Round == theme && game.RoundManager.ContentSlotInRound == slot &&
                        !game.RoundManager.IsCurrentRestStage && !game.RoundManager.IsCurrentFixedBossStage && AliveEnemies().Any() &&
                        game.RoundManager.CurrentCombatKind == (slot == 6 ? StageKind.Elite : slot == 8 ? StageKind.Boss : StageKind.Normal));
                }
            }
            foreach ((int stage, int boss) in new[] { (34,3201), (68,3202), (102,3202), (136,3202) })
            {
                game.DebugLoadStage(stage);
                Check($"{ProgressionLayout.Label(stage)} 주기·실제 보스", game.RoundManager.IsCurrentFixedBossStage &&
                    game.RoundManager.CurrentBossId == boss && AliveEnemies().Any(u => u.ID == boss));
                if (boss == 3202) Check("군세장 전후열 보존 " + stage, AliveEnemies().Count(u => u.ID == 1121) == 2 &&
                    AliveEnemies().Single(u => u.ID == 3202).currentCell.xPos == GridManager.Instance.GetRearColumn(true));
                game.DebugLoadStage(stage - 1);
                Check($"{ProgressionLayout.Label(stage-1)} 전투·사건 없음", game.RoundManager.CurrentCombatKind == StageKind.Rest &&
                    !game.RoundManager.IsRoundInProgress && !AliveEnemies().Any() && game.gameState == GameState.Preparation &&
                    !game.RoundManager.TryGetScheduledEvent(out _) && game.IsFreeLongRestPreparation);
            }
            game.DebugLoadStage(32); int themeId = game.RoundManager.CurrentThemeId;
            var kinds = game.RoundManager.GetUpcomingStageKinds(8);
            Check("4-8 예고 보스·휴식·고정보스", kinds.SequenceEqual(new[] { StageKind.Boss, StageKind.Rest, StageKind.Boss }));
            run.AdvanceToNextStage();
            Check("4-8→4-9 단일 진행·테마 보존", game.RoundManager.Stage == 33 && game.RoundManager.CurrentThemeId == themeId);
            var victim = AliveAllies().First(); var killer = GridManager.Instance.SpawnUnit(1, 1, true, 1110);
            DebugMode.AllyInvincible = false;
            victim.TakeDamage(new DamageContext(killer, 10000000, CodeType.Normal, new() { DamageTag.NormalAttack, DamageTag.NonContactAttack }));
            GridManager.Instance.ClearActiveEnemies();
            foreach (Unit hero in AliveAllies()) { hero.ModifyHp(Mathf.Max(1, hero.HpMax / 3)); hero.ClearUltimateResource(); }
            game.inventoryManager.Initialize(); game.inventoryManager.ItemIdsInHand.Clear(); game.inventoryManager.ItemIdsInHand.Add(5102);
            game.RestorePreparationState(false, 0, 0);
            int food = game.inventoryManager.FoodUnits, gold = game.inventoryManager.Gold;
            var items = game.inventoryManager.ItemIdsInHand.ToList();
            game.OpenRestFromPreparation();
            var restCanvas = GameObject.Find("RestCanvas");
            var button = restCanvas?.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.transform.parent.name == "LongRest");
            Check("0식량 무료 긴 휴식 UI", food == 0 && button != null && button.interactable && button.GetComponentInChildren<TextMeshProUGUI>().text.Contains("무료"),
                $"식량{food}/버튼{button?.interactable}/{button?.GetComponentInChildren<TextMeshProUGUI>().text}");
            button?.onClick.Invoke();
            Check("실제 버튼 부활·HP/마나·횟수 회복", victim.isActive && AliveAllies().All(u => u.HpCurr == u.HpMax && u.ManaCurr == u.ManaMax) &&
                game.ShortRestCharges == GameManager.MaxShortRestCharges && game.RestUsedThisPreparation);
            Check("무료 휴식 재화·소모품·생명 보존", game.inventoryManager.FoodUnits == food && game.inventoryManager.Gold == gold &&
                game.inventoryManager.ItemIdsInHand.SequenceEqual(items) && !game.TryLongRest() && !game.TryShortRest());
            run.SaveCurrentRun(); var saved = SaveSystem.LoadRun(); string heroes = string.Join(";", saved.heroUnits.Select(u => JsonUtility.ToJson(u)));
            game.DebugResetBattle(); run.RestoreDebugSnapshot(saved);
            Check("휴식 저장 복원·무료 재사용 방지", game.RoundManager.Stage == 33 && game.RoundManager.StageInRound == 9 &&
                !AliveEnemies().Any() && game.RestUsedThisPreparation && !game.TryLongRest());
            int mana = AliveAllies().Sum(u => u.ManaCurr);
            game.StartRound();
            Check("4-9→4-10 무전투·보상 없이 진행", game.RoundManager.Stage == 34 && game.RoundManager.CurrentBossId == 3201 &&
                game.gameState == GameState.Preparation && AliveAllies().Sum(u => u.ManaCurr) == mana);
            Check("4-10 실제 보스 준비·예고 경계", AliveEnemies().Any() && game.RoundManager.GetUpcomingStageKinds(8).Count == 1);
            run.AdvanceToNextStage();
            Check("4-10→5-1 회복·자원 초기화", game.RoundManager.Stage == 35 && game.RoundManager.Round == 5 && game.RoundManager.StageInRound == 1 &&
                AliveAllies().All(u => u.HpCurr==u.HpMax && u.ManaCurr == (u.UltimateResourceType==UltimateResourceType.Passive?u.ManaMax:0)));
            foreach ((int legacy, int current) in new[] { (24,24), (32,32), (33,35), (48,50), (49,51), (64,66), (96,100) })
            {
                var old = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(saved)); old.version = 15; old.currentStage = legacy;
                SaveSystem.SaveRun(old); var loaded = SaveSystem.LoadRun();
                Check($"v15 {legacy}→{current} 호환", loaded.version == RunSaveData.CurrentVersion && loaded.currentStage == current &&
                    heroes == string.Join(";", loaded.heroUnits.Select(u => JsonUtility.ToJson(u))) && loaded.gold == saved.gold &&
                    loaded.restUsedThisPreparation == saved.restUsedThisPreparation);
                SaveSystem.SaveRun(loaded);
                Check("반복 로드 이중 이관 방지 " + legacy, SaveSystem.LoadRun().currentStage == current);
            }
            // 같은 테마ID가 연속 추첨되어도 표시 테마(8칸)가 바뀌면 한 번 회복한다.
            game.DebugLoadStage(8);
            Unit fallen=AliveAllies().First(u=>u.ID!=60),attacker=AliveEnemies().First();
            fallen.TakeDamage(new DamageContext(attacker,10000000,CodeType.Effect,new(){DamageTag.TrueDamage},false));
            foreach(Unit hero in AliveAllies()) { hero.ModifyHp(Mathf.Max(1,hero.HpMax/3));hero.RestoreUltimateResourceOutsideCombat(); }
            game.RestorePreparationState(false,0,0);
            int entryGold=game.inventoryManager.Gold,entryFood=game.inventoryManager.FoodUnits;
            var beforeIds=GridManager.Instance.heroList.Select(u=>u.ID>0?u.ID:u.LastActiveId).OrderBy(x=>x).ToArray();
            int healingEvents=0;void OnHeal(Unit target,Unit source,int effective,int overflow)=>healingEvents++;
            Unit.AnyHealed+=OnHeal;
            try { run.AdvanceToNextStage(); } finally { Unit.AnyHealed-=OnHeal; }
            Check("1-8→2-1 같은ID도 부활·전원 완전 회복",game.RoundManager.Stage==9 && game.RoundManager.CurrentThemeId==2 &&
                fallen.isActive && AliveAllies().Count==beforeIds.Length && AliveAllies().All(u=>u.HpCurr==u.HpMax &&
                    u.ManaCurr==(u.UltimateResourceType==UltimateResourceType.Passive?u.ManaMax:0)),
                string.Join(";",AliveAllies().Select(u=>$"{u.ID}:{u.HpCurr}/{u.HpMax}:M{u.ManaCurr}:{u.UltimateResourceType}")));
            Check("테마 회복은 생명·식량·재화·휴식 횟수 미소비",game.inventoryManager.Gold==entryGold &&
                game.inventoryManager.FoodUnits==entryFood && game.ShortRestCharges==0 && !game.RestUsedThisPreparation && healingEvents==0);
            Unit wounded=AliveAllies().First();wounded.ModifyHp(wounded.HpMax/2);wounded.RestoreUltimateResourceOutsideCombat();
            int hp=wounded.HpCurr,resource=wounded.ManaCurr,id=wounded.ID;
            run.SaveCurrentRun();var partial=SaveSystem.LoadRun();game.DebugResetBattle();run.RestoreDebugSnapshot(partial);
            wounded=AliveAllies().Single(u=>u.ID==id);
            Check("저장 복원은 테마 회복 재발동 없음",wounded.HpCurr==hp && wounded.ManaCurr==resource);
            run.AdvanceToNextStage();
            Check("테마 내부 전진은 HP·자원 보존",game.RoundManager.Stage==10 && wounded.HpCurr==hp && wounded.ManaCurr==resource);
            game.DebugResetBattle(); DebugMode.ForcedThemeId = 0; OsFinish(enabled);
        }
    }
}
#endif
