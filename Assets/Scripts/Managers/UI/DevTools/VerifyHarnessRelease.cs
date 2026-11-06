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
using Entities.View;
using Managers.Stage;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // 모드를 지정하지 않은 새 여정의 실제 UI 경로. 저장만 메모리로 격리한다.
        private IEnumerator ScenarioStageDefaultJourney(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            bool suite = DebugMode.SuiteRunning;
            DebugMode.SuiteRunning = false;
            try
            {
                yield return WaitUnscaled(.3f);
                bool menu = SceneManager.GetActiveScene().name == "MainMenu" && VisualModeState.Current == VisualMode.Stage;
                RuiCheck("기본 무대 로비", "모드 강제 지정 없이 MainMenu·Stage", SceneManager.GetActiveScene().name + "/" + VisualModeState.Current, menu);
                if (!menu) { RuiFinish(); yield break; }
                yield return CaptureStill("default_stage_menu");
                SaveSystem.SeedEmptyDebugAccount();
                var start = GameObject.Find("MainMenuBrightCanvas").GetComponentsInChildren<Button>().First(b => b.name == "NewRun");
                start.onClick.Invoke();
                yield return WaitForGameReady(20f);
                var selection = GameObject.Find("CharacterSelectCanvas");
                var selectionGroup = selection == null ? null : selection.GetComponentInChildren<CanvasGroup>();
                float deadline = Time.realtimeSinceStartup + 5f;
                while (selectionGroup != null && selectionGroup.alpha < .99f && Time.realtimeSinceStartup < deadline) yield return null;
                bool selectVisible = selectionGroup != null && selectionGroup.alpha >= .99f;
                RuiCheck("기본 무대 편성", "실제 새 여정 버튼→편성창 표시·Stage", VisualModeState.Current.ToString(), selectVisible && VisualModeState.Current == VisualMode.Stage);
                if (!selectVisible) { RuiFinish(); yield break; }
                foreach (int id in new[] {180, 125, 121, 1, 2})
                    RuiClick(selection.GetComponentsInChildren<Transform>(true).First(t => t.name == "Unit" + id));
                RuiCheck("기본 무대 선택", "우치·이시스·아누비스·세이·시 5명", string.Join(",", CharacterSelectionManager.Instance.Lineup.Select(e => e.UnitId)), CharacterSelectionManager.Instance.Lineup.Count == 5);
                yield return CaptureStill("default_stage_selection");
                selection.GetComponentsInChildren<Button>().First(b => b.name == "Primary").onClick.Invoke();
                yield return WaitForState(BaseEnums.GameState.Preparation, 8f);
                yield return WaitUnscaled(1.1f);
                var game = GameManager.Instance;
                var grid = GridManager.Instance;
                RuiCheck("기본 무대 준비", "준비 상태·실제 SD5명", game.gameState.ToString(), game.gameState == BaseEnums.GameState.Preparation && AliveAllies().Count == 5 && AliveAllies().All(u => u.currentCell.Card.StageProxy?.Frames != null && u.currentCell.Card.StageProxy.Lower.enabled));
                var cells = FindObjectsByType<Cell>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.xPos is -1 or -2 && c.yPos >= 1 && c.yPos <= 4).ToArray();
                RuiCheck("기본 무대 배치8칸", "8개 슬롯·발 중심 콜라이더 활성", cells.Length.ToString(), cells.Length == 8 && cells.All(c => c.gameObject.activeInHierarchy && c.GetComponent<BoxCollider2D>().enabled && c.transform.Find("StagePreparationSlot") != null));
                var actor = AliveAllies().First();
                var from = actor.currentCell;
                var empty = cells.First(c => !c.isOccupied);
                var drag = DragAndDropManager.Instance;
                Vector2 Point(Cell c) => Camera.main.WorldToScreenPoint(c.transform.position);
                drag.StartDrag(from, Point(from)); yield return null;
                drag.EndDrag(Point(empty)); yield return WaitUnscaled(.35f);
                RuiCheck("기본 무대 배치 이동", "화면 좌표 드롭→빈칸 이동·기존 객체 보존", actor.currentCell.name, actor.currentCell == empty && !from.isOccupied);
                var other = AliveAllies().First(u => u != actor);
                var otherCell = other.currentCell;
                drag.StartDrag(actor.currentCell, Point(actor.currentCell)); yield return null;
                drag.EndDrag(Point(otherCell)); yield return WaitUnscaled(.35f);
                RuiCheck("기본 무대 배치 교환", "점유 칸 드롭→객체 교환", actor.currentCell.name, actor.currentCell == otherCell && other.currentCell == empty);
                yield return CaptureStill("default_stage_preparation");
                int actions = 0;
                void Finished(Unit unit, ActionScheduler.ActionKind kind) => actions++;
                ActionScheduler.AnyActionFinished += Finished;
                try
                {
                    game.StartRound();
                    yield return WaitForState(BaseEnums.GameState.RoundInProgress, 8f);
                    yield return WaitUnscaled(.5f);
                    RuiCheck("기본 무대 전투 HUD", "Stage·실제 전투·하단 카드5개", game.gameState.ToString(), VisualModeState.Current == VisualMode.Stage && StageBattleHud.Visible && AliveAllies().All(u => u.currentCell.Card.StageHudHitRect(Camera.main).width > 0));
                    foreach (int id in new[] {180, 125})
                    {
                        var owner = AliveAllies().Single(u => u.ID == id);
                        // 우치의 분신술은 일반행동에서 생성된다. 개전 0.5초에 존재한다고 가정하지 않는다.
                        deadline = Time.realtimeSinceStartup + 20f;
                        while (!FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Any(v => v.Owner == owner && v.Renderer != null && v.Renderer.enabled && v.Renderer.sprite != null) && game.gameState == BaseEnums.GameState.RoundInProgress && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        yield return EndOfFrame;
                        var visuals = FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Where(v => v.Owner == owner).ToArray();
                        string badge = SmBadge(owner);
                        RuiCheck("기본 무대 소환수 " + id, "실제 생성 뒤 소환수 SD·배지", badge + "/" + visuals.Length, visuals.Length > 0 && visuals.All(v => v.Renderer != null && v.Renderer.enabled && v.Renderer.sprite != null) && badge.Contains(id == 180 ? "분신" : "수호령"));
                    }
                    yield return CaptureStill("default_stage_battle");
                    deadline = Time.realtimeSinceStartup + 120f;
                    while (ReleaseResultContinue() == null && Time.realtimeSinceStartup < deadline) yield return null;
                    RuiCheck("기본 무대 전투 결과", "실제 행동 완료→승리 결과창 표시·Stage 유지", $"actions={actions}, state={game.gameState}", actions > 0 && game.gameState == BaseEnums.GameState.RoundEnd && ReleaseResultContinue() != null && VisualModeState.Current == VisualMode.Stage);
                    yield return CaptureStill("default_stage_result");
                }
                finally { ActionScheduler.AnyActionFinished -= Finished; }
                RuiFinish();
            }
            finally { DebugMode.SuiteRunning = suite; _activeLineup = null; }
        }

        private static Button ReleaseResultContinue()
        {
            GameObject canvas = GameObject.Find("BattleResultCanvas");
            Button next = canvas == null ? null : canvas.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == "Continue" && b.interactable);
            return next != null && next.GetComponentsInParent<CanvasGroup>().All(g => g.alpha >= .99f) ? next : null;
        }
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
            RuiCheck("v12 저장 이관", "활성7·초과512보관·v15", migrated.version.ToString(), migrated.version==RunSaveData.CurrentVersion &&
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
                    Button next=ReleaseResultContinue();
                    if(next!=null) { if(victories==0) {
                        yield return CaptureStill("release_victory_result");
                        RuiClick(RuiFind(GameObject.Find("BattleResultCanvas"), "CombatLog"));
                        yield return WaitUnscaled(.15f); yield return CaptureStill("release_combat_log");
                        Managers.UI.Screens.ModalScreen.TryEscapeTop();
                    } next.onClick.Invoke(); yield return WaitUnscaled(.3f); }
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
            RuiCheck("실제 자동 전투→결과→보상→4단계", "무적/스탯 조작 없이 3단계 진행",$"stage={game.RoundManager.Stage},win={victories},actions={actions},events={events},state={game.gameState}",game.RoundManager.Stage>=4 && actions>0 && victories>0);
            RuiCheck("캠페인 자동 저장", "현재 단계 저장",SaveSystem.LoadRun()?.currentStage.ToString(),SaveSystem.LoadRun()?.currentStage==game.RoundManager.Stage);
            yield return CaptureStill("release_natural_campaign"); game.DebugResetBattle(); DebugMode.SuiteRunning=true; RuiFinish();
        }
        private IEnumerator ScenarioReleaseEvent(VerifyScenarioRequest config)
        {
            var priorMouse=Mouse.current;
            bool priorEnabled=priorMouse!=null&&priorMouse.enabled;
            var pointer=InputSystem.AddDevice<Mouse>();
            if(priorMouse!=null&&priorMouse.added)InputSystem.DisableDevice(priorMouse);
            try
            {
                // 실제 포인터 하나로만 호버한다. 물리 마우스와 수동 Enter가 서로 다른 선택지를 켜지 않게 한다.
                InputSystem.QueueStateEvent(pointer,new MouseState{position=new Vector2(Screen.width-15,15)});
                _ruiFailures.Clear();
                // 처음 두 범용 테마에는 사건이 없다. 실제 서사 테마의 사건 슬롯을 데이터에서 선택한다.
                yield return PrepareStage(config,17,false,false,false);
                GameManager game=GameManager.Instance;
                game.DebugResetBattle();DebugMode.ForcedThemeId=22;game.DebugLoadStage(17);
                int eventFloor=Enumerable.Range(17,7).First(floor=>game.RoundManager.GetStageKind(floor)==StageKind.Event);
                game.DebugResetBattle();game.DebugLoadStage(eventFloor);DebugMode.ForcedThemeId=0;
                if(game.gameState==BaseEnums.GameState.Preparation) game.StartRound();
                yield return WaitUnscaled(.3f);
                RuiCheck($"{eventFloor}단계 사건 진입", "EventStage",game.gameState.ToString(),game.gameState==BaseEnums.GameState.EventStage);
                RunManager.Instance.SaveCurrentRun(); bool restored=RunManager.Instance.LoadSavedRun();
                RuiCheck("사건 저장·복원", $"{eventFloor}단계 사건 재진입",game.gameState.ToString(),restored && game.RoundManager.Stage==eventFloor && game.gameState==BaseEnums.GameState.EventStage);
                yield return CaptureStill("release_event_dialogue");
                game.SkipEventDialogue(); yield return WaitUnscaled(.2f);
                yield return CaptureStill("release_event_choices");
                var choice=FindObjectsByType<UIPointerEvents>(FindObjectsInactive.Exclude).FirstOrDefault(c=>c.name=="Choice0");
                RuiCheck("사건 선택지 표시", "Choice0 클릭 가능",choice?.name,choice!=null);
                var rect=choice!=null?choice.GetComponent<RectTransform>():null;
                Vector2 hoverPoint=rect!=null?RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)):Vector2.zero;
                InputSystem.QueueStateEvent(pointer,new MouseState{position=hoverPoint});yield return WaitUnscaled(.16f);
                yield return CaptureStill("release_event_choice_hover");
                var hoverImage=choice!=null?choice.transform.Find("HoverRing")?.GetComponent<Image>():null;
                RuiCheck("사건 호버면 표시","색면 표시·세로 장식 없음",hoverImage?.color.a.ToString(),
                    hoverImage!=null&&hoverImage.color.a>.9f&&choice.transform.Find("Tick")==null);
                InputSystem.QueueStateEvent(pointer,new MouseState{position=new Vector2(Screen.width-15,15)});yield return WaitUnscaled(.16f);
                yield return CaptureStill("release_event_choice_hover_exit");
                RuiCheck("사건 호버면 복귀","이탈 뒤 강조 해제",hoverImage?.color.a.ToString(),hoverImage!=null&&hoverImage.color.a<.01f);
                choice?.OnPointerClick(null); yield return WaitUnscaled(.2f);
                yield return CaptureStill("release_event_choice_result");
                Button confirm=FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b=>b.name=="Confirm" && b.interactable);
                RuiCheck("사건 결과 확인", "확인 버튼",confirm?.name,confirm!=null);
                confirm?.onClick.Invoke(); yield return WaitUnscaled(.3f);
                RuiCheck("사건 후 다음 단계 저장·준비", $"stage{eventFloor+1} 준비·저장",game.gameState.ToString(),game.RoundManager.Stage==eventFloor+1 && game.gameState==BaseEnums.GameState.Preparation && SaveSystem.LoadRun()?.currentStage==eventFloor+1);
                RuiFinish();
            }
            finally
            {
                DebugMode.ForcedThemeId=0;
                InputSystem.RemoveDevice(pointer);
                if(priorMouse!=null&&priorMouse.added)
                {
                    if(priorEnabled)InputSystem.EnableDevice(priorMouse);
                    priorMouse.MakeCurrent();
                }
            }
        }
        private IEnumerator ScenarioReleaseLoss(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); DebugMode.SuiteRunning=false;
            yield return PrepareStage(config,1,true,false,true);
            GameManager game=GameManager.Instance;
            int gold=game.inventoryManager.Gold; Unit attacker=AliveEnemies().First();
            foreach(Unit hero in AliveAllies().ToList())
            {
                hero.RemoveShield(hero.ShieldCurr);
                for(int hit=0;hit<4 && hero.isActive;hit++)
                    YvProbe(attacker,hero,BaseEnums.CodeType.Normal,10000000);
            }
            float deadline=Time.realtimeSinceStartup+12f;
            while(Time.realtimeSinceStartup<deadline && ReleaseResultContinue()==null)yield return null;
            Button next=ReleaseResultContinue();
            RuiCheck("전멸 즉시 런 종료·보상 차단", "GameOver·재화 불변·저장 제거·결과 확인",game.gameState.ToString(),
                game.gameState==BaseEnums.GameState.GameOver && !RunManager.Instance.RunActive && !SaveSystem.HasSave() &&
                game.inventoryManager.Gold==gold && next!=null && !game.TryLongRest());
            yield return CaptureStill("release_gameover_result");
            next?.onClick.Invoke();yield return WaitUnscaled(.5f);
            RuiCheck("전멸 결과 확인 후 메뉴 복귀", "MainMenu",UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu");
            DebugMode.SuiteRunning=true;RuiFinish();
        }
    }
}
#endif
