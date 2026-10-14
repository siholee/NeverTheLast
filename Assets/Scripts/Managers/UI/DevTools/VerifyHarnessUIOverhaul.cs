#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Core;
using Entities;
using Managers.UI.Core;
using Managers.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static RewardCodeInventory.Drag CodeDrag(Unit unit, bool stored, int index)
            => new() { Owner = unit, Stored = stored, Index = index,
                CodeId = stored ? unit.StoredRewardCodeIds[index] : unit.RewardCodeSlots[index] };

        private IEnumerator ActualCodeDrag(Transform source, Transform destination, string capture)
        {
            UIDragSource handle = source?.GetComponent<UIDragSource>();
            if (handle == null || destination == null) { RuiCheck("실제 드래그 경로", "손잡이/대상 있음", "없음", false); yield break; }
            // 자동 보관 직후에는 목록이 아래로 내려가 있다. 화면 밖 카드의 좌표를 눌러 다른 활성 칸을 집지 않는다.
            ScrollRect sourceScroll = source.GetComponentInParent<ScrollRect>();
            if (sourceScroll != null)
            {
                sourceScroll.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                yield return null;
            }
            Canvas.ForceUpdateCanvases();
            Vector2 from = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)source).TransformPoint(((RectTransform)source).rect.center));
            Vector2 to = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)destination).TransformPoint(((RectTransform)destination).rect.center));
            Mouse prior = Mouse.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>("CodeDragVerification");
            bool priorEnabled=prior!=null&&prior.enabled;
            float priorDelta=Time.captureDeltaTime;
            bool priorDeferred=_deferCaptureEncoding;
            Time.captureDeltaTime=1f/60f;_deferCaptureEncoding=true;
            try
            {
                if(priorEnabled)InputSystem.DisableDevice(prior);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                // 실제 입력 모듈이 OnPointerDown을 전달해야 한다. 손잡이를 직접 깨우면 수신 결함이 가려진다.
                for(int step=1;step<=18;step++)
                {
                    Vector2 position=Vector2.Lerp(from,to,step/18f);
                    InputSystem.QueueStateEvent(mouse,new MouseState {position=position}.WithButton(MouseButton.Left));
                    yield return EndOfFrame;Capture(capture+"_live_move");
                }
                for(int hold=0;hold<8;hold++){yield return EndOfFrame;Capture(capture+"_live_hover");}
                RuiCheck("실제 드래그 유령", "요청한 출발 카드의 Payload", UIDragSource.Current?.GetType().Name ?? "없음", ReferenceEquals(UIDragSource.Current, handle.Payload));
                yield return CaptureStill(capture);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to });
                yield return null; yield return null;
                for(int settle=0;settle<25;settle++){yield return EndOfFrame;Capture(capture+"_live_drop");}
                RuiCheck("드래그 종료", "Payload 정리", UIDragSource.Current == null ? "정리" : "남음", UIDragSource.Current == null);
            }
            finally
            {
                FlushDeferredCaptures();_deferCaptureEncoding=priorDeferred;Time.captureDeltaTime=priorDelta;
                InputSystem.RemoveDevice(mouse);
                if(prior!=null&&prior.added){if(priorEnabled)InputSystem.EnableDevice(prior);prior.MakeCurrent();}
            }
        }

        private IEnumerator RuiRevealInput(List<RewardDef> samples,int replay)
        {
            Mouse prior=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>("RevealInputVerification");
            float oldDelta=Time.captureDeltaTime;Time.captureDeltaTime=0f;_deferCaptureEncoding=true;
            try
            {
                float start=Time.realtimeSinceStartup;
                foreach(float at in new[]{.10f,.30f,.45f})
                {
                    while(Time.realtimeSinceStartup<start+at){yield return EndOfFrame;Capture("overhaul_rebind"+replay+"_reveal_live");}
                    var root=GameObject.Find("RewardScreen");int index=at<.40f?0:2;
                    var card=(RectTransform)RuiFind(root,"Reward"+index);
                    Vector2 point=RectTransformUtility.WorldToScreenPoint(null,card.TransformPoint(card.rect.center));
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));
                    yield return EndOfFrame;Capture("overhaul_rebind"+replay+"_click_down_"+at);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
                    yield return EndOfFrame;yield return EndOfFrame;
                    bool selected=RuiFind(root,"ConfirmReward").GetComponent<Button>().interactable;
                    RuiCheck("리빌 실제 클릭 "+replay+"/"+at,at<.2f?"보이지 않는 카드 선택 차단":"표시된 카드 선택 허용",selected.ToString(),selected==(at>=.2f));
                    Capture("overhaul_rebind"+replay+"_click_up_"+at);
                }
                while(Time.realtimeSinceStartup<start+.62f){yield return EndOfFrame;Capture("overhaul_rebind"+replay+"_reveal_live");}
            }
            finally{FlushDeferredCaptures();Time.captureDeltaTime=oldDelta;InputSystem.RemoveDevice(mouse);if(prior!=null&&prior.added)prior.MakeCurrent();}
        }

        private IEnumerator ScenarioUIOverhaul(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 6, false, true, true);
            var game = GameManager.Instance; var ui = game.uiManager;
            yield return WaitUnscaled(.3f);
            CheckPartyHudReadability();
            RuiCheck("상단 식량 표시 제외","준비 상태만 표시",RuiText(GameObject.Find("TopStatusBar")?.transform,"Phase"),RuiText(GameObject.Find("TopStatusBar")?.transform,"Phase")=="준비");
            var priorMouse=Mouse.current;var previewMouse=InputSystem.AddDevice<Mouse>("StageForecastVerification");
            try
            {
                int distance=Mathf.Min(4,RoundManager.EncountersPerTheme-game.RoundManager.StageInRound);
                int future=game.RoundManager.Stage+distance;
                string expectedStage=$"{(future-1)/RoundManager.EncountersPerTheme+1}-{(future-1)%RoundManager.EncountersPerTheme+1}";
                var preview=RuiFind(GameObject.Find("TopStatusBar"),"Slot"+distance);
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)preview).TransformPoint(((RectTransform)preview).rect.center));
                InputSystem.QueueStateEvent(previewMouse,new MouseState{position=point});
                yield return WaitUnscaled(.3f);
                string forecast=RuiText(GameObject.Find("TooltipCanvas")?.transform,"Title");
                string body=RuiText(GameObject.Find("TooltipCanvas")?.transform,"Body");
                RuiCheck("미래 스테이지 실제 호버",expectedStage+" · "+distance+"단계 후",forecast+" / "+body,forecast?.Contains(expectedStage)==true&&body?.Contains(distance+"단계 후")==true);
                yield return CaptureStill("overhaul_forecast_tooltip");
            }
            finally { InputSystem.RemoveDevice(previewMouse);if(priorMouse!=null&&priorMouse.added)priorMouse.MakeCurrent();UITooltip.HideAny(); }

            List<Unit> party = RewardCodeFlow.Party();
            Unit a = party.First(); Unit b = party[1]; Unit c = party[2];
            var original = party.ToDictionary(u => u, u =>
            {
                var state = new UnitSaveData { currentHP = u.HpCurr, ultimateResource = u.ManaCurr };
                u.WriteRunState(state); return state;
            });
            int originalGold = game.inventoryManager.Gold;
            foreach (Unit unit in party) unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
            int[] initial = { 501, 502, 503, 504, 506, 507, 509 };
            foreach (int id in initial) a.TryGrantRewardCode(id);
            RuiCheck("활성 상한", "활성 7 · 보관 0", $"{a.RewardCodeIds.Count}/{a.StoredRewardCodeIds.Count}", a.RewardCodeIds.Count == 7 && a.StoredRewardCodeIds.Count == 0);
            float special = a.StatValue(BaseEnums.StatType.SpecialAttack);
            bool grant = a.TryGrantRewardCode(505);
            RuiCheck("8번째 코드 보관", "505 보관 · 활성 7 · 특공 불변", $"{grant}, {string.Join(",", a.StoredRewardCodeIds)}", grant && a.StoredRewardCodeIds.Contains(505) && a.RewardCodeIds.Count == 7 && Mathf.Approximately(special, a.StatValue(BaseEnums.StatType.SpecialAttack)));
            RuiCheck("보관 패시브 미등록", "505 패시브 없음", "패시브 조회", !a.ActivePassiveCodes.Any(p => p.CatalogId == 505));
            RuiCheck("중복 보상 차단", "같은 보유 코드 재획득 거절", "505 재획득", !a.TryGrantRewardCode(505));
            foreach (int id in new[] { 508, 510, 511, 512, 617, 618, 619, 620, 621, 622, 623, 624 }) a.TryGrantRewardCode(id);
            RuiCheck("추가 보관", "보관 13 · 활성 7", $"{a.StoredRewardCodeIds.Count}/{a.RewardCodeIds.Count}", a.StoredRewardCodeIds.Count == 13 && a.RewardCodeIds.Count == 7);
            ui.CodexForVerification.Show(a);
            yield return WaitUnscaled(0.45f);
            RuiClick(RuiFind(KbCodexRoot(), "Tab빌드"));
            yield return WaitUnscaled(0.2f);
            RuiCheck("새 내비게이션", "남색 제목 띠 있음", "NavigationInk", RuiFind(KbCodexRoot(), "NavigationInk") != null);
            RuiReadable("파티 빌드", KbCodexRoot());
            yield return CaptureStill("overhaul_party_active_storage");
            float hpRatio = a.HpCurr / (float)a.HpMax;
            yield return ActualCodeDrag(RuiFind(KbCodexRoot(), "RewardSlot0"), RuiFind(KbCodexRoot(), "CodeStorage"), "overhaul_drag_to_storage");
            RuiCheck("활성 해제 실드롭", "0번 빈 칸 · 501 보관", string.Join(",", a.RewardCodeSlots), a.RewardCodeSlots[0] == 0 && a.StoredRewardCodeIds.Contains(501));
            RuiCheck("체력 비율 보존", "편집 전후 비율 동일", $"{hpRatio:0.000}/{a.HpCurr / (float)a.HpMax:0.000}", Mathf.Abs(hpRatio - a.HpCurr / (float)a.HpMax) < 0.005f);
            yield return ActualCodeDrag(RuiFind(KbCodexRoot(), "StoredCode0"), RuiFind(KbCodexRoot(), "RewardSlot0"), "overhaul_drag_activate");
            RuiCheck("보관 활성화 실드롭", "0번 505 · 특공+10", string.Join(",", a.RewardCodeSlots), a.RewardCodeSlots[0] == 505 && !a.StoredRewardCodeIds.Contains(505) && Mathf.Approximately(a.StatValue(BaseEnums.StatType.SpecialAttack), special + 10f));
            yield return ActualCodeDrag(RuiFind(KbCodexRoot(), "RewardSlot1"), RuiFind(KbCodexRoot(), "RewardSlot2"), "overhaul_drag_swap");
            RuiCheck("점유 칸 교환 실드롭", "1번503 · 2번502", string.Join(",", a.RewardCodeSlots), a.RewardCodeSlots[1] == 503 && a.RewardCodeSlots[2] == 502);
            RewardCodeInventory.TryMove(CodeDrag(a, false, 1), a, false, 2);
            // 점유 칸 교환은 밀려난 코드를 정확히 출발 위치에 돌린다.
            b.TryGrantRewardCode(701);
            bool cross = RewardCodeInventory.TryMove(CodeDrag(a, false, 0), b, false, 0);
            RuiCheck("아군 간 활성 교환", "a0=701 · b0=505", $"{a.RewardCodeSlots[0]}/{b.RewardCodeSlots[0]}", cross && a.RewardCodeSlots[0] == 701 && b.RewardCodeSlots[0] == 505);
            b.TryGrantRewardCode(502);
            var beforeA = a.RewardCodeSlots.ToList(); var beforeB = b.RewardCodeSlots.ToList();
            bool blocked = RewardCodeInventory.TryMove(CodeDrag(a, false, 1), b, false, 0);
            RuiCheck("중복 활성 교환 원자성", "양쪽 불변", "502 중복 드롭", !blocked && beforeA.SequenceEqual(a.RewardCodeSlots) && beforeB.SequenceEqual(b.RewardCodeSlots));
            c.TryGrantRewardCode(629); c.TryGrantRewardCode(726);
            RuiCheck("상위 획득 시 하위 보관", "활성 726 · 보관 629", $"{string.Join(",", c.RewardCodeIds)}/{string.Join(",", c.StoredRewardCodeIds)}", c.RewardCodeIds.Contains(726) && c.StoredRewardCodeIds.Contains(629));
            RuiCheck("상하위 동시 활성 차단", "하위 활성 거절", "629 → 빈 칸", !RewardCodeInventory.TryMove(CodeDrag(c, true, 0), c, false, 1));
            RuiCheck("보관 코드 승급", "508 → 608, 활성 변화 없음", "인장 대체", a.TryReplaceRewardCode(508, 608) && a.StoredRewardCodeIds.Contains(608) && !a.RewardCodeIds.Contains(608));
            var stale = CodeDrag(a, true, 0);
            a.RemoveRewardCode(stale.CodeId);
            RuiCheck("오래된 드래그 차단", "바뀐 출발 코드 이동 거절", "출발 위치 검사", !RewardCodeInventory.TryMove(stale, b, true));
            game.gameState = BaseEnums.GameState.RoundInProgress;
            RuiCheck("전투 중 편집 차단", "모든 이동 거절", "준비 밖", !RewardCodeInventory.CanMove(CodeDrag(a, false, 0), b, true));
            game.gameState = BaseEnums.GameState.Preparation;
            // 빈 자리와 보관 순서는 실제 JSON 직렬화/복원 경로를 탄다.
            RewardCodeInventory.TryMove(CodeDrag(a, false, 3), a, true);
            var saved = new UnitSaveData { currentHP = a.HpCurr, ultimateResource = a.ManaCurr }; a.WriteRunState(saved);
            var parsed = JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved));
            a.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>()); a.RestoreRunState(parsed);
            RuiCheck("활성/보관 JSON 왕복", "빈3번 포함 순서 보존", "v13 유닛 스냅샷", a.RewardCodeSlots.SequenceEqual(saved.activeRewardCodeSlots) && a.StoredRewardCodeIds.SequenceEqual(saved.storedRewardCodeIds) && a.RewardCodeSlots[3] == 0);
            var legacy = new RunSaveData { version = 12, heroUnits = new List<UnitSaveData> { new() { grantedPassiveCodeIds = initial.ToList() } } };
            SaveSystem.SaveRun(legacy); RunSaveData migrated = SaveSystem.LoadRun();
            RuiCheck("v12 저장 이관", "v14 · 활성7 그대로 · 보관0", $"v{migrated?.version}", migrated?.version == RunSaveData.CurrentVersion && migrated.heroUnits[0].activeRewardCodeSlots.SequenceEqual(initial) && migrated.heroUnits[0].storedRewardCodeIds.Count == 0);
            // 파티 초상화 드롭도 실제 레이캐스트 경로로 검증한다.
            ui.CodexForVerification.Show(a); yield return WaitUnscaled(0.3f);
            int transferId = a.StoredRewardCodeIds[0]; int targetCount = b.StoredRewardCodeIds.Count;
            yield return ActualCodeDrag(RuiFind(KbCodexRoot(), "StoredCode0"), RuiFind(KbCodexRoot(), $"Member_{b.UnitName}"), "overhaul_drag_to_ally");
            RuiCheck("아군 보관함 실드롭", "b 보관 +1 · 내 코드 이동", $"코드 {transferId}", b.StoredRewardCodeIds.Count == targetCount + 1 && b.StoredRewardCodeIds.Last() == transferId && !a.StoredRewardCodeIds.Contains(transferId));
            ScrollRect storage = RuiFind(KbCodexRoot(), "StorageScroll")?.GetComponent<ScrollRect>();
            if (storage != null) storage.verticalNormalizedPosition = 0f;
            yield return WaitUnscaled(0.2f); yield return CaptureStill("overhaul_code_storage_bottom");
            RuiCheck("보관 스크롤", "목록 높이 > 뷰포트", "전체 보관 코드 접근", storage != null && storage.content.rect.height > storage.viewport.rect.height);
            // 상점의 일곱 칸 가득 찬 대상에게 사면 폐기 선택 없이 보관된다.
            if (a.RewardCodeSlots[3] == 0)
            {
                int index = a.StoredRewardCodeIds.ToList().IndexOf(504);
                if (index >= 0) RewardCodeInventory.TryMove(CodeDrag(a, true, index), a, false, 3);
            }
            ui.CodexForVerification.Hide();
            game.inventoryManager.RestoreGold(20000); game.RestorePreparationState(false, 2, 0);
            ui.ShowShopPanel(); yield return WaitUnscaled(0.35f);
            RuiReadable("상점", GameObject.Find("ShopScreen")); yield return CaptureStill("overhaul_shop_codes");
            var shopRoot = GameObject.Find("ShopScreen"); int gold = game.inventoryManager.Gold;
            foreach(string tab in new[]{"Consumable","Supply","Sell","Code"})
            {
                RuiClick(RuiFind(shopRoot,"Tab"+tab));yield return WaitUnscaled(.1f);
                var panel=RuiFind(shopRoot,"Panel") as RectTransform;
                RuiCheck("상점 "+tab+" 전체 공간","너비1800 · 높이990 이상",panel?.rect.ToString(),panel!=null&&panel.rect.width>=1800&&panel.rect.height>=990);
                RuiReadable("상점 "+tab,shopRoot);
                yield return CaptureStill("overhaul_shop_"+tab.ToLowerInvariant());
            }

            RuiClick(RuiFind(shopRoot, "CodeGood_2")); yield return WaitUnscaled(0.2f);
            // 새 카드가 보이고 입력 차단이 풀린 뒤 호버를 검사한다(.12초 지연 + .18초 등장).
            RuiClick(RuiBack(shopRoot)); yield return WaitUnscaled(0.35f);
            RuiCheck("상점 취소 무과금", "골드 그대로", $"{gold}/{game.inventoryManager.Gold}", gold == game.inventoryManager.Gold);
            Transform buy = RuiFind(RuiFind(shopRoot, "CodeGood_2")?.gameObject, "StateBand");
            RuiCheck("상점 구매 조작", "실제 Button 존재", buy?.GetComponent<Button>() != null ? "Button" : "없음", buy?.GetComponent<Button>() != null);
            if (buy != null)
            {
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(buy.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return WaitUnscaled(.18f);
                var feedback=buy.Find("InteractionFeedback")?.GetComponent<Image>();
                RuiCheck("구매 호버 군청 채도", "청색광 · 파랑이 빨강의 두 배 이상",feedback?.color.ToString(),feedback!=null&&feedback.color.b>feedback.color.r*2f);
                yield return CaptureStill("overhaul_shop_hover");
                ExecuteEvents.Execute(buy.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return WaitUnscaled(.12f); yield return CaptureStill("overhaul_shop_pressed");
                ExecuteEvents.Execute(buy.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(buy.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                EventSystem.current?.SetSelectedGameObject(null);
                RuiCheck("누름 예고 무과금", "포인터 클릭 전 골드 그대로", $"{gold}/{game.inventoryManager.Gold}", gold == game.inventoryManager.Gold);
            }
            RuiClick(buy); yield return WaitUnscaled(0.2f);
            yield return CaptureStill("overhaul_shop_recipient");
            RuiClick(RuiTargetCard(shopRoot, a)); yield return WaitUnscaled(0.3f);
            RuiCheck("가득 찬 대상 상점 구매", "703 보관 · 활성7 · 1800G 결제", $"활성{a.RewardCodeIds.Count} 보관{a.StoredRewardCodeIds.Count} G{game.inventoryManager.Gold}", a.StoredRewardCodeIds.Contains(703) && a.RewardCodeIds.Count == 7 && game.inventoryManager.Gold == gold - 1800 && game.ShopPurchasesLeft == 1);
            RuiClick(RuiFind(shopRoot, "TabConsumable")); yield return WaitUnscaled(0.2f);
            RuiCheck("상점 전 상품", "11종", $"{RuiAll(shopRoot, n => n.StartsWith("Good_", StringComparison.Ordinal)).Count}", RuiAll(shopRoot, n => n.StartsWith("Good_", StringComparison.Ordinal)).Count == 11);
            yield return CaptureStill("overhaul_shop_recovery");
            ui.HideShopPanel(); ui.CodexForVerification.Show(a); yield return WaitUnscaled(0.2f);
            yield return CaptureStill("overhaul_party_after_purchase");
            RuiClick(RuiFind(KbCodexRoot(), "Tab코드")); yield return WaitUnscaled(0.2f); yield return CaptureStill("overhaul_code_reference");
            ui.CodexForVerification.Hide();
            ui.ShowInventoryPanel(); yield return WaitUnscaled(0.2f); yield return CaptureStill("overhaul_supply"); ui.HideInventoryPanel();
            ui.ShowRestPanel(); yield return WaitUnscaled(0.2f);
            var host=RuiFind(GameObject.Find("RestScreen"),"HimikoStanding")?.GetComponent<Image>();
            RuiCheck("휴식 히미코", "원화 있음 · 비율 유지",host?.sprite?.name,host!=null&&host.sprite!=null&&host.type==Image.Type.Simple&&host.preserveAspect);
            RuiReadable("휴식",GameObject.Find("RestScreen")); yield return CaptureStill("overhaul_rest");
            ui.HideRestPanel();
            ui.ShowCharacterSelection(); yield return WaitUnscaled(0.3f); yield return CaptureStill("overhaul_character_select"); ui.HideCharacterSelection();
            var tonicReward = (RewardDef)typeof(RewardManager).GetMethod("BuildItemReward", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { game.inventoryManager.Find(5101) });
            ui.ShowRewardPanel(new List<RewardDef> { tonicReward, RewardManager.BuildCodeReward(RewardCodeCatalog.Find(723)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(618)) });
            yield return WaitUnscaled(1.5f); yield return CaptureStill("overhaul_tonic_reward_fallback");
            ui.ShowRewardPanel(new List<RewardDef> { RewardManager.BuildCodeReward(RewardCodeCatalog.Find(808)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(723)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(618)) });
            yield return WaitUnscaled(1.5f); RuiReadable("보상 세 장", GameObject.Find("RewardScreen")); yield return CaptureStill("overhaul_rewards");
            var samples=new List<RewardDef> { RewardManager.BuildCodeReward(RewardCodeCatalog.Find(808)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(723)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(618)) };
            // 실제 보상 코드 설명과 메타의 용어 링크를 TMP 문자 좌표에서 호버한다.
            var glossaryReward=RewardManager.BuildCodeReward(RewardCodeCatalog.Find(702));
            ui.ShowRewardPanel(new List<RewardDef>{glossaryReward,samples[1],samples[2]});yield return WaitUnscaled(.2f);
            var glossaryRoot=GameObject.Find("RewardScreen");
            var summary=RuiFind(RuiFind(glossaryRoot,"Reward0")?.gameObject,"Summary")?.GetComponent<TMPro.TextMeshProUGUI>();
            var meta=RuiFind(RuiFind(glossaryRoot,"Reward0")?.gameObject,"Meta")?.GetComponent<TMPro.TextMeshProUGUI>();
            var termPrior=Mouse.current;var termMouse=InputSystem.AddDevice<Mouse>("RewardTermVerification");
            try
            {
                Vector2 termPoint=GuideLinkPoint(summary,"term:control");
                RuiCheck("보상 본문 실제 용어 문자 히트","term:control",summary.GetComponent<GameTermLinks>().LinkAt(termPoint),summary.GetComponent<GameTermLinks>().LinkAt(termPoint)=="term:control");
                InputSystem.QueueStateEvent(termMouse,new MouseState{position=termPoint});yield return WaitUnscaled(.85f);
                RuiCheck("보상 본문 실제 호버 사전","제어 1층",GuideTermTooltip.CurrentId,GuideTermTooltip.Depth==1&&GuideTermTooltip.CurrentId=="control");
                yield return CaptureStill("overhaul_reward_term_hover");GuideTermTooltip.CloseAll();
                Vector2 metaPoint=GuideLinkPoint(meta,"term:rewardcodes");
                InputSystem.QueueStateEvent(termMouse,new MouseState{position=metaPoint});yield return WaitUnscaled(.85f);
                RuiCheck("보상 코드 메타 실제 호버 사전","보상 코드 1층",GuideTermTooltip.CurrentId,GuideTermTooltip.Depth==1&&GuideTermTooltip.CurrentId=="rewardcodes");
                yield return CaptureStill("overhaul_reward_meta_hover");
            }
            finally {InputSystem.RemoveDevice(termMouse);if(termPrior!=null&&termPrior.added)termPrior.MakeCurrent();GuideTermTooltip.CloseAll();}
            for(int replay=0;replay<3;replay++)
            {
                ui.ShowRewardPanel(samples);
                yield return RuiRevealInput(samples,replay);
            }
            ui.ShowRewardPanel(samples.Concat(samples).ToList());
            _deferCaptureEncoding=true;
            float sixUntil=Time.realtimeSinceStartup+.62f;
            while(Time.realtimeSinceStartup<sixUntil){yield return EndOfFrame;Capture("overhaul_six_reveal_live");}
            FlushDeferredCaptures();
            var rewardRoot=GameObject.Find("RewardScreen");
            var choices=rewardRoot.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("Reward")&&r.name.Length==7&&char.IsDigit(r.name[6])).OrderBy(r=>r.position.x).ToList();
            RuiCheck("보상 여섯 장 가로 비교", "모두 같은 행 · 높이600 이상 · 서로 겹치지 않음", choices.Count.ToString(),choices.Count==6&&choices.All(r=>r.rect.height>=600&&Mathf.Abs(r.position.y-choices[0].position.y)<1)&&choices.Zip(choices.Skip(1),(l,r)=>l.position.x+l.rect.width*.5f<r.position.x-r.rect.width*.5f).All(v=>v));
            int selectedGold=game.inventoryManager.Gold;
            RuiClick(RuiFind(rewardRoot,"Reward0")); yield return WaitUnscaled(.1f);
            RuiCheck("보상 선택 후 확정 대기", "획득 흐름 안 열림 · 골드 유지 · 확정 가능",selectedGold.ToString(),RuiFind(rewardRoot,"RewardFooter").gameObject.activeInHierarchy&&game.inventoryManager.Gold==selectedGold&&RuiFind(rewardRoot,"ConfirmReward").GetComponent<Button>().interactable);
            RuiReadable("보상 여섯 장",rewardRoot);
            yield return CaptureStill("overhaul_rewards_six_selected");
            RuiClick(RuiFind(rewardRoot,"ConfirmReward"));yield return WaitUnscaled(.15f);
            RuiCheck("보상 확정 후 대상 지정","대상 흐름 열림 · 아직 획득 전",game.inventoryManager.Gold.ToString(),RuiFind(rewardRoot,"RewardFooter")==null&&game.inventoryManager.Gold==selectedGold);
            yield return CaptureStill("overhaul_reward_target");
            RuiClick(RuiBack(rewardRoot));yield return WaitUnscaled(.1f);
            RuiCheck("보상 대상 취소 복원","원래 선택 보존 · 확정 가능",RuiText(rewardRoot.transform,"SelectionHint"),RuiFind(rewardRoot,"RewardFooter").gameObject.activeInHierarchy&&RuiFind(rewardRoot,"ConfirmReward").GetComponent<Button>().interactable);
            int ticketBefore=game.inventoryManager.rerollTicketCount;
            game.inventoryManager.rerollTicketCount=2;
            ui.ShowRewardPanel(samples);yield return WaitUnscaled(.1f);
            RuiCheck("리롤 티켓 보유 활성", "2장 · 실제 버튼 활성",RuiText(rewardRoot.transform,"Label"),RuiFind(rewardRoot,"Reroll").GetComponent<Button>().interactable);
            yield return CaptureStill("overhaul_rewards_reroll_enabled");
            game.inventoryManager.rerollTicketCount=1;
            // 이전 화면의 열려 있던 툴팁은 선택지를 바꾸면서 즉시 정리해야 한다.
            object staleTip=new object();
            UITooltip.ShowAt(staleTip,"이전 보상",new[]{UITooltip.Line.Note("이전 선택지 설명",UITheme.TextSecondary)},UITheme.Accent,new Vector2(300,500));
            RuiCheck("교체 전 툴팁 재현", "이전 툴팁 표시","툴팁 소유자",UITooltip.IsShowing(staleTip));
            ui.ShowRewardPanel(samples);
            RuiCheck("선택지 교체 툴팁 정리", "이전 툴팁 닫힘","툴팁 소유자",!UITooltip.IsShowing(staleTip));
            RuiClick(RuiFind(rewardRoot,"Reward0"));
            RuiClick(RuiFind(rewardRoot,"Reroll"));yield return WaitUnscaled(.1f);
            RuiCheck("리롤 비용과 선택 초기화","티켓0 · 획득 없음 · 확정 비활성",game.inventoryManager.rerollTicketCount.ToString(),game.inventoryManager.rerollTicketCount==0&&game.inventoryManager.Gold==selectedGold&&!RuiFind(rewardRoot,"ConfirmReward").GetComponent<Button>().interactable);
            game.inventoryManager.rerollTicketCount=ticketBefore;
            var healReward=game.dataManager.FetchRewardDataList().rewards.First(r=>r.id=="healing_domain_t1");
            a.ModifyHp(Mathf.Max(1,a.HpMax/2));int beforeHeal=a.HpCurr;
            ui.ShowRewardPanel(new List<RewardDef>{samples[0],healReward,samples[2]});
            yield return WaitUnscaled(.1f);RuiClick(RuiFind(rewardRoot,"Reward1"));
            RuiClick(RuiFind(rewardRoot,"ConfirmReward"));yield return WaitUnscaled(.1f);
            RuiCheck("회복 보상 확정 후 대상 지정", "대상 흐름 · HP 유지",a.HpCurr.ToString(),RuiFind(rewardRoot,"RewardFooter")==null&&RuiTargetCard(rewardRoot,a)!=null&&a.HpCurr==beforeHeal);
            yield return CaptureStill("overhaul_healing_target");
            RuiClick(RuiBack(rewardRoot));yield return WaitUnscaled(.1f);
            RuiCheck("회복 대상 취소 선택 보존", "회복 선택 유지 · HP 유지",RuiText(rewardRoot.transform,"SelectionHint"),RuiFind(rewardRoot,"ConfirmReward").GetComponent<Button>().interactable&&RuiText(rewardRoot.transform,"SelectionHint").Contains(healReward.displayName)&&a.HpCurr==beforeHeal);
            yield return CaptureStill("overhaul_healing_back");
            var longReward=RewardManager.BuildCodeReward(RewardCodeCatalog.Find(618));
            longReward.description=string.Join("\n",Enumerable.Repeat("효과가 길어도 끝까지 읽을 수 있어야 합니다.",30))+"\n설명 마지막 확인";
            ui.ShowRewardPanel(new List<RewardDef>{samples[0],longReward,samples[2]});yield return WaitUnscaled(.1f);
            var longScroll=RuiFind(rewardRoot,"Reward1").GetComponentsInChildren<ScrollRect>().First();
            RuiCheck("보상 긴 설명 내부 스크롤","본문 끝까지 읽기 · 세 선택 동시 유지",longScroll.content.rect.height.ToString(),longScroll.content.rect.height>longScroll.viewport.rect.height&&RuiText(longScroll.content,"Summary").Contains("설명 마지막 확인"));
            longScroll.verticalNormalizedPosition=0;yield return WaitUnscaled(.1f);
            yield return CaptureStill("overhaul_reward_long_bottom");ui.HideRewardPanel();
            yield return CaptureStill("overhaul_preparation");
            ui.HandleEscape(); yield return WaitUnscaled(0.2f);
            yield return CaptureStill("overhaul_pause_menu");
            RuiClick(RuiFind(GameObject.Find("PauseMenuCanvas"), "Exit"));
            yield return WaitUnscaled(.15f);
            RuiCheck("나가기 확인 표시", "취소 가능 · 씬 유지", GameObject.Find("ConfirmDialog")?.name, GameObject.Find("ConfirmDialog") != null);
            yield return CaptureStill("overhaul_exit_confirmation");
            RuiClick(RuiFind(GameObject.Find("ConfirmCanvas"), "Cancel"));
            yield return WaitUnscaled(.15f);
            RuiCheck("나가기 취소 유지", "Game · 메뉴 유지", SceneManager.GetActiveScene().name, SceneManager.GetActiveScene().name == "Game" && ui.IsPauseMenuOpen);
            RuiClick(RuiFind(GameObject.Find("PauseMenuCanvas"), "Wiki"));
            yield return WaitUnscaled(0.2f);
            var wikiRoot=GameObject.Find("WikiScreen");
            RuiCheck("자료실 선택 막대 제거","활성 SelectionBar 없음","선택 표시",!wikiRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="SelectionBar"));
            var home=RuiFind(wikiRoot,"GuideHomeCards") as RectTransform;
            var content=home?.parent as RectTransform;
            RuiCheck("자료실 첫 화면 수직 중앙","상하 여백 동일",home?.anchoredPosition.y.ToString(),home!=null&&Mathf.Abs(-home.anchoredPosition.y-(content.rect.height-home.rect.height)*.5f)<1);
            var wikiSubs=wikiRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Where(t=>t.name=="Sub"&&t.transform.parent.name.StartsWith("Row_")).ToList();
            foreach(var sub in wikiSubs)sub.ForceMeshUpdate();
            RuiCheck("자료실 목록 부제 표시", "부제 높이/문자 가시성 확보",string.Join("/",wikiSubs.Select(t=>$"{t.rectTransform.rect.height:F1}:{t.textInfo.characterCount}")),wikiSubs.Count>0&&wikiSubs.All(t=>t.rectTransform.rect.height>=28&&t.textInfo.characterInfo.Any(c=>c.isVisible)));
            yield return CaptureStill("overhaul_wiki");
            ui.HandleEscape(); ui.HandleEscape();

            foreach (Unit unit in party) unit.RestoreRunState(original[unit]);
            game.inventoryManager.RestoreGold(originalGold); game.RestorePreparationState(false, 2, 0);
            RuiFinish();
        }

        private IEnumerator ScenarioUIOverhaulMenu(VerifyScenarioRequest config)
        {
            GameManager.Instance?.DebugResetBattle();
            GameManager.LoadMainMenuScene();
            yield return WaitUnscaled(0.7f);
            RuiCheck("메뉴 저장 차단", "전장 없는 메뉴에서 저장 불가", "CanSaveNow", RunManager.Instance?.CanSaveNow != true);
            RunManager.Instance?.SaveCurrentRun();
            RuiFinish();
            Image lobbyArt = GameObject.Find("LobbyCharacter")?.GetComponent<Image>();
            RuiCheck("로비 원화 표시 방식", "Simple / 비율 유지", lobbyArt?.type.ToString(),
                lobbyArt != null && lobbyArt.type == Image.Type.Simple && lobbyArt.preserveAspect);
            Vector3 artScale = lobbyArt != null ? lobbyArt.transform.lossyScale : Vector3.zero;
            RuiCheck("로비 원화 축 배율", "가로/세로 동일", artScale.ToString(),
                lobbyArt != null && Mathf.Approximately(artScale.x, artScale.y));
            Canvas.ForceUpdateCanvases();
            var lobby = GameObject.Find("MainMenuBrightCanvas");
            bool overflowing = lobby.GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                .Any(t => t.isTextOverflowing);
            RuiCheck("로비 텍스트 수용", "넘치는 제목/설명 없음", overflowing.ToString(), !overflowing);
            yield return CaptureStill("overhaul_main_menu");
            Managers.UI.SettingsUI settings = FindAnyObjectByType<Managers.UI.SettingsUI>();
            settings?.Open(); yield return WaitUnscaled(0.3f); yield return CaptureStill("overhaul_settings"); settings?.Close();
            float menuScale=Time.timeScale;
            RuiClick(GameObject.Find("MainMenuBrightCanvas")?.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Wiki")?.transform);
            yield return WaitUnscaled(.2f);
            RuiCheck("로비 자료실 직접 진입", "자료실 활성 / 시간0", GameObject.Find("WikiScreen")?.activeInHierarchy.ToString(), GameObject.Find("WikiScreen")!=null && Time.timeScale==0f);
            yield return CaptureStill("overhaul_main_menu_guide");
            bool closed=Managers.UI.Screens.ModalScreen.TryEscapeTop();
            yield return WaitUnscaled(.1f);
            RuiCheck("로비 자료실 닫기 복원", "닫기 / 이전 시간", Time.timeScale.ToString(), closed && Mathf.Approximately(Time.timeScale,menuScale));
            // 사용자 저장은 DebugMode의 메모리 저장소로 격리된다. 빈 여정 로비도 실제 씬으로 검수한다.
            if (DebugMode.SessionActive)
            {
                RunSaveData saved = SaveSystem.HasSave() ? SaveSystem.LoadRun() : null;
                SaveSystem.DeleteSave();
                GameManager.LoadMainMenuScene();
                yield return WaitUnscaled(.7f);
                var buttons = GameObject.Find("MainMenuBrightCanvas").GetComponentsInChildren<Button>();
                Button resume = buttons.FirstOrDefault(b => b.name == "Continue");
                Button start = buttons.FirstOrDefault(b => b.name == "NewRun");
                RuiCheck("빈 로비 이어하기", "비활성", resume?.interactable.ToString(), resume != null && !resume.interactable);
                RuiCheck("빈 로비 새 여정", "주 행동 활성", start?.interactable.ToString(), start != null && start.interactable);
                yield return CaptureStill("overhaul_main_menu_empty");
                RuiClick(start?.transform);
                yield return WaitUnscaled(.8f);
                RuiCheck("로비 새 여정 실제 진입", "Game / 편성", SceneManager.GetActiveScene().name,
                    SceneManager.GetActiveScene().name == "Game" && GameObject.Find("CharacterSelectCanvas") != null);
                // 실제 새 여정은 편성 대기로 끝난다. 다음 시나리오가 이전 파티 캐시를 재사용하면
                // 편성을 확인하지 않은 빈 런에서 전투하여 결과 검사가 잘못 실패한다.
                _activeLineup = null;
                if (saved != null) SaveSystem.SaveRun(saved);
            }
            RuiFinish();
        }
    }
}
#endif
