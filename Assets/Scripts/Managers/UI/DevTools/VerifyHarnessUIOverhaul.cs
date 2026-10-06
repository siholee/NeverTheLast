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
            Canvas.ForceUpdateCanvases();
            Vector2 from = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)source).TransformPoint(((RectTransform)source).rect.center));
            Vector2 to = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)destination).TransformPoint(((RectTransform)destination).rect.center));
            Mouse prior = Mouse.current;
            Mouse mouse = InputSystem.AddDevice<Mouse>("CodeDragVerification");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                handle.OnPointerDown(new PointerEventData(EventSystem.current) { position = from, button = PointerEventData.InputButton.Left, pointerId = -1 });
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to }.WithButton(MouseButton.Left));
                yield return null; yield return null; yield return null;
                RuiCheck("실제 드래그 유령", "Payload 있음", UIDragSource.Current?.GetType().Name ?? "없음", UIDragSource.Current != null);
                yield return CaptureStill(capture);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = to });
                yield return null; yield return null; yield return WaitUnscaled(0.25f);
                RuiCheck("드래그 종료", "Payload 정리", UIDragSource.Current == null ? "정리" : "남음", UIDragSource.Current == null);
            }
            finally { InputSystem.RemoveDevice(mouse); if (prior != null && prior.added) prior.MakeCurrent(); }
        }

        private IEnumerator ScenarioUIOverhaul(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 6, false, true, true);
            var game = GameManager.Instance; var ui = game.uiManager;
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
            RuiCheck("v12 저장 이관", "v13 · 활성7 그대로 · 보관0", $"v{migrated?.version}", migrated?.version == 13 && migrated.heroUnits[0].activeRewardCodeSlots.SequenceEqual(initial) && migrated.heroUnits[0].storedRewardCodeIds.Count == 0);
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
            RuiClick(RuiFind(shopRoot, "CodeGood_2")); yield return WaitUnscaled(0.2f);
            RuiClick(RuiBack(shopRoot)); yield return WaitUnscaled(0.15f);
            RuiCheck("상점 취소 무과금", "골드 그대로", $"{gold}/{game.inventoryManager.Gold}", gold == game.inventoryManager.Gold);
            RuiClick(RuiFind(shopRoot, "CodeGood_2")); yield return WaitUnscaled(0.2f);
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
            ui.ShowRestPanel(); yield return WaitUnscaled(0.2f); yield return CaptureStill("overhaul_rest");
            ui.HideRestPanel();
            ui.ShowCharacterSelection(); yield return WaitUnscaled(0.3f); yield return CaptureStill("overhaul_character_select"); ui.HideCharacterSelection();
            ui.ShowRewardPanel(new List<RewardDef> { RewardManager.BuildCodeReward(RewardCodeCatalog.Find(808)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(723)), RewardManager.BuildCodeReward(RewardCodeCatalog.Find(618)) });
            yield return WaitUnscaled(0.3f); yield return CaptureStill("overhaul_rewards"); ui.HideRewardPanel();
            yield return CaptureStill("overhaul_preparation");
            ui.HandleEscape(); yield return WaitUnscaled(0.2f);
            yield return CaptureStill("overhaul_pause_menu");
            RuiClick(RuiFind(GameObject.Find("PauseMenuCanvas"), "Wiki"));
            yield return WaitUnscaled(0.2f); yield return CaptureStill("overhaul_wiki");
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
            yield return CaptureStill("overhaul_main_menu");
            Managers.UI.SettingsUI settings = FindAnyObjectByType<Managers.UI.SettingsUI>();
            settings?.Open(); yield return WaitUnscaled(0.3f); yield return CaptureStill("overhaul_settings"); settings?.Close();
        }
    }
}
#endif
