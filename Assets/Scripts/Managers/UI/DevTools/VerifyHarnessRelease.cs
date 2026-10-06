#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Core;
using Effects.Buffs;
using Entities.Status;
using Managers.UI.Core;
using UnityEngine.UI;
using Entities;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioReleaseSave(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 1, false, true, false);
            GameManager game = GameManager.Instance; bool enabled = game.enabled; game.enabled = false;
            RunManager run = RunManager.Instance;
            game.RestorePreparationState(true, 1, 2);
            game.gameState = BaseEnums.GameState.RewardSelection;
            run.AdvanceToNextStage();
            RunSaveData saved = SaveSystem.LoadRun();
            RuiCheck("스테이지 전진 자동 저장 준비 초기화", "2단계·휴식 미사용·상점0", $"{saved.currentStage}/{saved.restUsedThisPreparation}/{saved.shopPurchaseCount}",
                saved.currentStage == 2 && !saved.restUsedThisPreparation && saved.shopPurchaseCount == 0);
            game.RestorePreparationState(false, 1, 0);
            Unit hero = AliveAllies().First();
            int[] slots = {501,0,503,0,0,0,604};
            var store = new List<int>{507,629};
            hero.ApplyRewardCodeInventory(slots.ToList(), store);
            hero.SetPermanentStacks("qa.persist", 21, .75f); hero.DebugSetLevel(8);
            game.inventoryManager.RestoreGold(12345);
            run.SaveCurrentRun(); saved = SaveSystem.LoadRun();
            bool restored = run.LoadSavedRun();
            hero = AliveAllies().First(x=>x.ID == saved.heroUnits[0].unitId);
            RuiCheck("런 저장·복원 코드 빈 칸/보관/영구중첩", "슬롯 순서·보관·21.75·레벨8", string.Join(",",hero.RewardCodeSlots),
                restored && hero.RewardCodeSlots.SequenceEqual(slots) && hero.StoredRewardCodeIds.SequenceEqual(store) &&
                hero.GetPermanentStacks("qa.persist") == 21 && Near(hero.GetPermanentStackFraction("qa.persist"), .75f) && hero.Level == 8);
            RuiCheck("런 저장·복원 재화/휴식", "골드12345·휴식1", game.inventoryManager.Gold.ToString(), game.inventoryManager.Gold==12345 && game.ShortRestCharges==1);
            var legacy = JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(saved)); legacy.version=12;
            legacy.heroUnits[0].activeRewardCodeSlots.Clear(); legacy.heroUnits[0].storedRewardCodeIds.Clear();
            legacy.heroUnits[0].grantedPassiveCodeIds = new List<int>{501,503,504,505,507,509,510,512};
            SaveSystem.SaveRun(legacy); var migrated=SaveSystem.LoadRun();
            RuiCheck("v12 저장 이관", "활성7·초과512보관·v13", migrated.version.ToString(), migrated.version==13 &&
                migrated.heroUnits[0].activeRewardCodeSlots.Count==7 && migrated.heroUnits[0].storedRewardCodeIds.Contains(512));
            SaveSystem.SaveRun(saved);
            var dict=(Dictionary<string,string>)typeof(SaveSystem).GetField("DebugStrings",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
            dict["NTL_RunSave"]="{bad json";
            bool safe=false; string error="없음";
            try { safe=SaveSystem.LoadRun()==null; } catch(Exception exception){error=exception.GetType().Name;}
            RuiCheck("손상된 런 JSON 안전 처리", "예외 없이 불러오기 거부",error,safe && dict["NTL_RunSave"]=="{bad json");
            bool refused=!run.LoadSavedRun();
            RuiCheck("손상 저장 계속하기 원본 보존", "거부 후 원본 유지",dict["NTL_RunSave"],refused && dict["NTL_RunSave"]=="{bad json");
            dict["NTL_UnlockedCharacters_v9"]="{bad json";
            bool accountSafe=false; error="없음";
            try { accountSafe=SaveSystem.LoadTrainedCharacters()!=null; } catch(Exception exception){error=exception.GetType().Name;}
            RuiCheck("손상된 계정 JSON 안전 처리", "예외 없이 기본 계정·원본 유지",error,accountSafe && dict["NTL_UnlockedCharacters_v9"]=="{bad json");
            SaveSystem.SeedEmptyDebugAccount();
            SaveSystem.SaveRun(saved);
            yield return CaptureStill("release_save_roundtrip");
            game.enabled=enabled; RuiFinish();
        }

        private IEnumerator ScenarioReleaseShield(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); bool enabled=true;
            yield return YvPrepareScene(config,x=>enabled=x);
            Unit anubis=AliveAllies().First(x=>x.ID==121);
            anubis.SetPermanentStacks(AnubisSoulHarvest.SoulKey,300);
            var passive=anubis.ActivePassiveCodes.OfType<AnubisSoulHarvest>().First();
            anubis.RemoveShield(anubis.ShieldCurr); passive.StopCode(); passive.CastCode();
            int first=anubis.ShieldCurr;
            YvAddTestStatus(anubis,99891,"qa_hp",new StatModifierEffect(BaseEnums.StatType.MaxHp,5000f));
            anubis.RemoveShield(anubis.ShieldCurr); passive.StopCode(); passive.CastCode();
            RuiCheck("아누비스 시작 보호막 HP 독립", "체력만 바꿔도 동일",$"{first}->{anubis.ShieldCurr}", first==anubis.ShieldCurr);
            anubis.RemoveStatusByKey("qa_hp");
            var empty=Enumerable.Repeat(0,7).ToList();
            foreach(int id in new[]{714,719,802})
            {
                anubis.ApplyRewardCodeInventory(empty,new List<int>()); anubis.TryGrantRewardCode(id);
                var code=anubis.ActivePassiveCodes.First(x=>x.CatalogId==id); code.StopCode(); code.CastCode();
                anubis.RemoveShield(anubis.ShieldCurr);
                if(id==714)
                {
                    anubis.ModifyHp(anubis.HpMax-1000); anubis.Heal(100,anubis); int shield=anubis.ShieldCurr;
                    anubis.BeginTurn(); anubis.RemoveShield(anubis.ShieldCurr); anubis.ModifyHp(anubis.HpMax-1000); anubis.Heal(200,anubis);
                    RuiCheck("응급치료 보호막 치유량 독립","같은 방어력→같은 양",$"{shield}->{anubis.ShieldCurr}",shield>0 && shield==anubis.ShieldCurr);
                }
                else if(id==719)
                {
                    anubis.RestoreHpOutsideCombat(anubis.HpMax); anubis.Heal(100,anubis); int shield=anubis.ShieldCurr;
                    anubis.BeginTurn(); anubis.RemoveShield(anubis.ShieldCurr); anubis.Heal(200,anubis);
                    RuiCheck("과다치유 보호막 초과치유량 독립","같은 방어력→같은 양",$"{shield}->{anubis.ShieldCurr}",shield>0 && shield==anubis.ShieldCurr);
                }
                else
                {
                    Unit enemy=AliveEnemies().First();
                    anubis.RestoreHpOutsideCombat(anubis.HpMax); YvProbe(anubis,enemy,BaseEnums.CodeType.Normal,500); int shield=anubis.ShieldCurr;
                    anubis.BeginTurn(); anubis.RemoveShield(anubis.ShieldCurr); YvProbe(anubis,enemy,BaseEnums.CodeType.Normal,1000);
                    RuiCheck("하루종일 보호막 초과치유량 독립","같은 방어력→같은 양",$"{shield}->{anubis.ShieldCurr}",shield>0 && shield==anubis.ShieldCurr);
                }
                int baseline=anubis.ShieldCurr;
                if(id==714) { anubis.ModifyHp(anubis.HpMax-1000); anubis.Heal(100,anubis); }
                else if(id==719) anubis.Heal(100,anubis);
                else YvProbe(anubis,AliveEnemies().First(),BaseEnums.CodeType.Normal,500);
                RuiCheck($"{id} 턴당 보호막 제한", "같은 턴 추가 생성 없음",anubis.ShieldCurr.ToString(),anubis.ShieldCurr==baseline);
                anubis.BeginTurn(); anubis.RemoveShield(anubis.ShieldCurr);
                YvAddTestStatus(anubis,99892,"qa_def",new StatModifierEffect(BaseEnums.StatType.Defense,100f));
                if(id==714) { anubis.ModifyHp(anubis.HpMax-1000); anubis.Heal(100,anubis); }
                else if(id==719) anubis.Heal(100,anubis);
                else YvProbe(anubis,AliveEnemies().First(),BaseEnums.CodeType.Normal,500);
                RuiCheck($"{id} 방어력 반영", "방어력 증가→보호막 증가",$"{baseline}->{anubis.ShieldCurr}",anubis.ShieldCurr>baseline);
                anubis.RemoveStatusByKey("qa_def"); code.StopCode();
            }
            anubis.ApplyRewardCodeInventory(empty,new List<int>()); anubis.TryGrantRewardCode(810);
            anubis.ActivePassiveCodes.First(c=>c.CatalogId==810).CastCode();
            UnitStatus benefit=BuffStatus.Create(99895,"qa_benefit","검증 버프",null,anubis,new StatModifierEffect(BaseEnums.StatType.Attack,1f),category:BaseEnums.StatusCategory.Positive);
            UnitStatus harmful=BuffStatus.Create(99895,"qa_harm","검증 디버프",null,anubis,new ReceivingDamageMultiplierEffect(1.1f),category:BaseEnums.StatusCategory.Negative);
            anubis.AddStatus(benefit); anubis.AddStatus(harmful); anubis.AddShield(10,anubis);
            RuiCheck("의신의 가호 동일 ID 정화", "버프 보존·해로운 상태 제거", anubis.GetAllStatuses().Count.ToString(),anubis.GetAllStatuses().Contains(benefit) && !anubis.GetAllStatuses().Contains(harmful));
            yield return CaptureStill("release_shield_rules");
            GameManager.Instance.ActionScheduler.EndRound(); GridManager.Instance.OnRoundEnd();
            GameManager.Instance.gameState=BaseEnums.GameState.Preparation; GameManager.Instance.enabled=enabled; RuiFinish();
        }
        private IEnumerator ScenarioReleaseCampaign(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config,1,false,false,false,8f);
            GameManager game=GameManager.Instance;
            Unit hero=AliveAllies().First();
            hero.ModifyHp(hero.HpMax/4); game.RestorePreparationState(false,2,0);
            int before=hero.HpCurr; bool shortRest=game.TryShortRest();
            RuiCheck("짧은 휴식 회복·소모·연속 차단", "50% 회복·1회 소모·같은 준비 중 거부",hero.HpCurr.ToString(),shortRest && hero.HpCurr>before && game.ShortRestCharges==1 && !game.TryShortRest());
            game.RestorePreparationState(false,0,0); game.inventoryManager.AddItem(5002);
            int food=game.inventoryManager.FoodUnits; bool longRest=game.TryLongRest();
            RuiCheck("긴 휴식 식량·완전 회복", "40식량 소모·체력 완전·충전2",game.inventoryManager.FoodUnits.ToString(),longRest && game.inventoryManager.FoodUnits==food-40 && hero.HpCurr==hero.HpMax && game.ShortRestCharges==2);
            DebugMode.SuiteRunning=false; // 결과 화면 생략 경로를 끄고 실제 확인 버튼을 검증한다.
            int victories=0, events=0, actions=0;
            using var recorder=new YvRecorder();
            float deadline=Time.realtimeSinceStartup+150f;
            while(game.RoundManager.Stage<4 && game.gameState!=BaseEnums.GameState.GameOver && Time.realtimeSinceStartup<deadline)
            {
                if(game.gameState==BaseEnums.GameState.Preparation) game.StartRound();
                else if(game.gameState==BaseEnums.GameState.RoundEnd)
                {
                    Button next=FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b=>b.name=="Continue" && b.interactable);
                    if(next!=null) { next.onClick.Invoke(); yield return WaitUnscaled(.3f); }
                }
                else if(game.gameState==BaseEnums.GameState.RewardSelection)
                {
                    victories++;
                    var choices=RewardManager.Instance.GenerateRewards(3,game.RoundManager.Stage,game.BattleKilledEnemyIds);
                    var reward=choices.FirstOrDefault(r=>r.IsCodeReward) ?? choices.First();
                    Unit target=reward.RequiresTargetSelection ? AliveAllies().FirstOrDefault():null;
                    RewardManager.Instance.ApplyReward(reward,target);
                    yield return WaitUnscaled(.3f);
                }
                else if(game.gameState==BaseEnums.GameState.EventStage)
                {
                    var choice=FindObjectsByType<UIPointerEvents>(FindObjectsInactive.Exclude).FirstOrDefault(c=>c.name.StartsWith("Choice"));
                    if(choice!=null) { events++; choice.OnPointerClick(null); yield return WaitUnscaled(.3f); }
                    else
                    {
                        Button confirm=FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b=>b.interactable && (b.name=="Confirm" || b.name=="DeclineRecruit"));
                        if(confirm!=null) { confirm.onClick.Invoke(); yield return WaitUnscaled(.3f); }
                    }
                }
                yield return null;
            }
            actions=recorder.Finished.Count;
            RuiCheck("실제 자동 전투→결과→보상→4단계", "무적/스탯 조작 없이 3단계 진행",$"stage={game.RoundManager.Stage},win={victories},actions={actions},events={events},life={game.life}",game.RoundManager.Stage>=4 && actions>0 && victories>0);
            RuiCheck("캠페인 자동 저장", "현재 단계 저장",SaveSystem.LoadRun()?.currentStage.ToString(),SaveSystem.LoadRun()?.currentStage==game.RoundManager.Stage);
            yield return CaptureStill("release_natural_campaign"); game.DebugResetBattle(); DebugMode.SuiteRunning=true; RuiFinish();
        }
        private IEnumerator ScenarioReleaseEvent(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config,5,false,false,false);
            GameManager game=GameManager.Instance; if(game.gameState==BaseEnums.GameState.Preparation) game.StartRound();
            yield return WaitUnscaled(.3f);
            RuiCheck("5단계 사건 진입", "EventStage",game.gameState.ToString(),game.gameState==BaseEnums.GameState.EventStage);
            RunManager.Instance.SaveCurrentRun(); bool restored=RunManager.Instance.LoadSavedRun();
            RuiCheck("사건 저장·복원", "5단계 사건 재진입",game.gameState.ToString(),restored && game.RoundManager.Stage==5 && game.gameState==BaseEnums.GameState.EventStage);
            game.SkipEventDialogue(); yield return WaitUnscaled(.2f);
            var choice=FindObjectsByType<UIPointerEvents>(FindObjectsInactive.Exclude).FirstOrDefault(c=>c.name=="Choice0");
            RuiCheck("사건 선택지 표시", "Choice0 클릭 가능",choice?.name,choice!=null);
            choice?.OnPointerClick(null); yield return WaitUnscaled(.2f);
            yield return CaptureStill("release_event_choice_result");
            Button confirm=FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b=>b.name=="Confirm" && b.interactable);
            RuiCheck("사건 결과 확인", "확인 버튼",confirm?.name,confirm!=null);
            confirm?.onClick.Invoke(); yield return WaitUnscaled(.3f);
            RuiCheck("사건→6단계 저장·준비", "stage6 준비·저장6",game.gameState.ToString(),game.RoundManager.Stage==6 && game.gameState==BaseEnums.GameState.Preparation && SaveSystem.LoadRun()?.currentStage==6);
            RuiFinish();
        }
        private IEnumerator ScenarioReleaseLoss(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            DebugMode.SuiteRunning=false;
            for(int attempt=0;attempt<2;attempt++)
            {
                yield return PrepareStage(config,1,true,false,true);
                GameManager game=GameManager.Instance; game.life=attempt==0?20:1;
                int gold=game.inventoryManager.Gold;
                Unit attacker=AliveEnemies().First();
                foreach(Unit hero in AliveAllies().ToList()) {hero.RemoveShield(hero.ShieldCurr);YvProbe(attacker,hero,BaseEnums.CodeType.Normal,10000000);}
                float deadline=Time.realtimeSinceStartup+12f;
                while(Time.realtimeSinceStartup<deadline && !FindObjectsByType<Button>(FindObjectsInactive.Exclude).Any(b=>b.name=="Continue" && b.interactable)) yield return null;
                Button next=FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b=>b.name=="Continue" && b.interactable);
                if(attempt==0)
                {
                    RuiCheck("패배 목숨·보상 차단", "목숨19·골드 동일·결과 확인",game.life+"/"+game.inventoryManager.Gold,game.life==19 && game.inventoryManager.Gold==gold && next!=null);
                    next?.onClick.Invoke(); yield return WaitUnscaled(.3f);
                    RuiCheck("패배 다음 단계", "보상 없이 2단계",game.RoundManager.Stage.ToString(),game.RoundManager.Stage==2 && game.gameState==BaseEnums.GameState.Preparation);
                    game.RestorePreparationState(false,0,0);game.inventoryManager.AddItem(5002);bool revived=game.TryLongRest();
                    RuiCheck("패배 후 긴 휴식 부활", "모든 편성원 회복",AliveAllies().Count.ToString(),revived && AliveAllies().Count==5 && AliveAllies().All(h=>h.HpCurr==h.HpMax));
                }
                else
                {
                    RuiCheck("게임오버 저장 제거·결과", "GameOver·세이브 없음·확인 버튼",game.gameState.ToString(),game.gameState==BaseEnums.GameState.GameOver && !SaveSystem.HasSave() && next!=null);
                    yield return CaptureStill("release_gameover_result");
                    next?.onClick.Invoke(); yield return WaitUnscaled(.5f);
                    RuiCheck("게임오버 메뉴 복귀", "MainMenu",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu");
                }
            }
            DebugMode.SuiteRunning=true; RuiFinish();
        }
    }
}
#endif
