#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Core;
using Entities;
using Managers.UI.Core;
using Managers.UI.Screens;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 2026-10-06 파티 창 · 상점 · 공용 보급 개편 검증(`reward_ui`, 편성 party5_reward_ui = 세이 · 인드라 · 쿠베라 · 아그니 · 수리야).
    ///
    /// 조작은 전부 실제 UI 경로로 한다 — 탭 · 버튼은 <see cref="Button.onClick"/>, 카드 · 레일 · 칸은 <see cref="UIPointerEvents"/>의
    /// 클릭 수신기를 부른다. 기대값은 기획(Detail_05 · Detail_17)의 규칙과 실제 유닛 상태에서 잡고, 화면이 그 값을 보여 주는지,
    /// 버튼을 누른 결과 골드 · 구매 횟수 · 코드 칸이 규칙대로 바뀌는지(또는 그대로인지)를 잰다.
    /// 960×540과 1920×1080 두 요청으로 돌린다. 캡처는 일반 경로(<c>ScreenCapture.CaptureScreenshot</c>)라 Game View 크기 그대로의 PNG다.
    /// `reward_ui_forms`는 아그니의 #주문 공격 형태가 보상 후보(613 주문 증폭)에 잡히는지를 실제 편성 두 개로 본다.
    /// 결과는 `[보상UI검증]` 줄과 mechanics에 남고, 시나리오가 끝나면 코드 칸 · 골드 · 보급 · 구매 횟수를 시작 상태로 되돌린다.
    /// </summary>
    public sealed partial class VerifyHarnessRunner
    {
        private const int AgniId = 62;
        private const int SuryaId = 67;
        private static readonly int[] PartyFiveRewardUiIds = { SeiId, IndraId, KuberaId, AgniId, SuryaId };

        /// <summary>상점 고정 코드(Detail_17 §5.2) — 재기의 바람 · 정신력 · 자가치유. 이름은 바뀔 수 있어 ID로 본다.</summary>
        private static readonly int[] RuiShopCodeIds = { 701, 702, 703 };

        /// <summary>상점 코드 기본가(Detail_05 §5 · Detail_17 §5.2). 화면 가격 = 300 × 스테이지.</summary>
        private const int RuiShopCodeBasePrice = 300;

        /// <summary>주요 글자의 하한(기준 해상도 단위). 22로 만든 글자가 자동 맞춤으로 이보다 줄면 960에서 10.5px 밑이 된다.</summary>
        private const float RuiReadableFloor = 20.85f;

        private readonly List<string> _ruiFailures = new();

        private static bool IsRewardUiScenario(string name)
            => !string.IsNullOrEmpty(name) && name.StartsWith("reward_ui", StringComparison.Ordinal);

        private void RuiCheck(string check, string expected, string actual, bool passed)
        {
            Debug.Log($"[보상UI검증] {(passed ? "통과" : "실패")} — {check}: 기대 {expected}, 실제 {actual}");
            RecordMechanic(check, expected, actual, passed);
            if (!passed) _ruiFailures.Add(check);
        }

        private void RuiFinish()
        {
            if (_ruiFailures.Count > 0) FailCurrent($"{_ruiFailures.Count}건 실패: " + string.Join(" / ", _ruiFailures));
            _ruiFailures.Clear();
        }

        // ── UI 찾기 · 누르기 ─────────────────────────────────────────

        private static Transform RuiFind(GameObject root, string name)
            => root == null ? null : root.GetComponentsInChildren<Transform>(false).FirstOrDefault(item => item.name == name);

        private static List<Transform> RuiAll(GameObject root, Func<string, bool> match)
            => root == null ? new List<Transform>()
                : root.GetComponentsInChildren<Transform>(false).Where(item => match(item.name)).ToList();

        private static TextMeshProUGUI RuiLabelOf(Transform parent, string child)
            => parent == null ? null
                : parent.GetComponentsInChildren<TextMeshProUGUI>(false).FirstOrDefault(label => label.name == child);

        private static string RuiText(Transform parent, string child) => RuiLabelOf(parent, child)?.text;

        /// <summary>보이는 글자(서식 태그를 걷은 것).</summary>
        private static string RuiParsed(Transform parent, string child)
        {
            TextMeshProUGUI label = RuiLabelOf(parent, child);
            if (label == null) return null;
            label.ForceMeshUpdate();
            return label.GetParsedText();
        }

        /// <summary>실제 클릭 경로. 버튼이면 onClick(꺼져 있으면 누르지 못한다), 카드면 클릭 수신기.</summary>
        private static bool RuiClick(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Button button = target.GetComponent<Button>();
            if (button != null)
            {
                if (!button.interactable) return false;
                button.onClick.Invoke();
                return true;
            }
            UIPointerEvents pointer = target.GetComponent<UIPointerEvents>();
            if (pointer == null) return false;
            pointer.OnPointerClick(null);
            return true;
        }

        private static Transform RuiBack(GameObject root)
            => root == null ? null : root.GetComponentsInChildren<Button>(false).FirstOrDefault(button => button.name == "Back")?.transform;

        private static Transform RuiTargetCard(GameObject root, Unit unit)
            => RuiAll(root, name => name == "Target").FirstOrDefault(card => RuiText(card, "Name") == unit.UnitName);

        private static List<string> RuiTargetNames(GameObject root)
            => RuiAll(root, name => name == "Target").Select(card => RuiText(card, "Name")).ToList();

        private static Transform RuiOption(GameObject root, string title)
            => RuiAll(root, name => name.StartsWith("Option", StringComparison.Ordinal))
                .FirstOrDefault(option => RuiText(option, "Title") == title);

        private static Rect RuiRect(Transform target)
        {
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            float xMin = corners.Min(point => point.x), yMin = corners.Min(point => point.y);
            return new Rect(xMin, yMin, corners.Max(point => point.x) - xMin, corners.Max(point => point.y) - yMin);
        }

        private static bool RuiOverlaps(Rect a, Rect b)
            => a.xMin < b.xMax - 1f && b.xMin < a.xMax - 1f && a.yMin < b.yMax - 1f && b.yMin < a.yMax - 1f;

        private static bool RuiInside(Rect outer, Rect inner)
            => inner.xMin >= outer.xMin - 2f && inner.xMax <= outer.xMax + 2f && inner.yMin >= outer.yMin - 2f && inner.yMax <= outer.yMax + 2f;

        private static string RuiName(int codeId) => RewardCodeCatalog.Find(codeId)?.verbalName ?? $"코드 {codeId}";

        private static string RuiSummary(int codeId) => CodeCatalog.Summary(RewardCodeCatalog.Find(codeId)?.description);

        private static string RuiCodes(Unit unit) => string.Join(",", unit.RewardCodeIds);

        /// <summary>
        /// 주요 글자가 자동 맞춤으로 줄지 않았는지. 22 이상으로 만든 글자만 보고, 가장 작은 것의 실제 픽셀을 함께 적는다.
        /// </summary>
        private void RuiReadable(string label, GameObject root)
        {
            if (root == null) { RuiCheck($"{label} 글자 크기", "화면 있음", "화면 없음", false); return; }
            Canvas.ForceUpdateCanvases();
            List<TextMeshProUGUI> labels = root.GetComponentsInChildren<TextMeshProUGUI>(false)
                .Where(text => !string.IsNullOrWhiteSpace(text.text) &&
                               text.GetComponent<UIScaledText>()?.BaseSize >= UITheme.FontReadSmall)
                .ToList();
            foreach (TextMeshProUGUI text in labels) text.ForceMeshUpdate();
            TextMeshProUGUI smallest = labels.OrderBy(text => text.fontSize).FirstOrDefault();
            float scale = root.GetComponentInParent<Canvas>()?.rootCanvas.scaleFactor ?? 1f;
            RuiCheck($"{label} 글자 크기", $"주요 글자 {labels.Count}개 모두 ≥ {RuiReadableFloor:0.#}(960에서 ≥10.5px)",
                smallest == null ? "글자 없음"
                    : $"최소 {smallest.name}「{Shorten(smallest.text)}」 {smallest.fontSize:0.#} → {smallest.fontSize * scale:0.#}px @ {Screen.width}×{Screen.height}",
                smallest != null && smallest.fontSize >= RuiReadableFloor);

            List<string> diagnostic = root.GetComponentsInChildren<TextMeshProUGUI>(false)
                .Where(text => text.text != null && (text.text.Contains(".yaml") || text.text.Contains("설명 없음")))
                .Select(text => $"{text.name}「{Shorten(text.text)}」").ToList();
            RuiCheck($"{label} 진단 문구 없음", "구현 파일 · ID 문구 없음",
                diagnostic.Count == 0 ? "없음" : string.Join(", ", diagnostic), diagnostic.Count == 0);

            // 이름 · 가격 · 상태 · 탭처럼 잘리면 뜻이 사라지는 글자는 말줄임도 없어야 한다.
            List<string> truncated = labels
                .Where(text => MustStayWhole.Contains(text.name) && text.isTextTruncated)
                .Select(text => $"{text.name}「{Shorten(text.text)}」").ToList();
            RuiCheck($"{label} 잘림", "이름 · 가격 · 상태 · 탭 글자 말줄임 없음",
                truncated.Count == 0 ? "없음" : string.Join(", ", truncated), truncated.Count == 0);
        }

        private static readonly HashSet<string> MustStayWhole = new()
        {
            "Name", "SelectedName", "State", "Slots", "Price", "Wallet", "Purchases", "Title", "Label", "Tier", "Number",
        };

        private static string Shorten(string text) => text.Length > 14 ? text.Substring(0, 14) + "…" : text;

        /// <summary>
        /// 가독 하한이 관리 화면에만 걸렸는지. 파티 창의 22 글자는 하한 20.9 × 글자 배율, 관리 화면 밖에서 만든 일반 24 글자는
        /// 원래 규칙(기준의 82% = 19.68, 하한 FontFloor)을 그대로 쓴다. 기대값은 규칙 수치를 직접 적는다.
        /// </summary>
        private void RuiFontFloorChecks(GameObject codexRoot)
        {
            float scale = UITheme.TextScale;
            TextMeshProUGUI managed = RuiLabelOf(RuiAll(codexRoot, name => name.StartsWith("Member_", StringComparison.Ordinal)).FirstOrDefault(), "State");
            UIScaledText managedScaled = managed != null ? managed.GetComponent<UIScaledText>() : null;
            float managedExpected = Mathf.Min(22f * scale, 20.9f * scale);
            RuiCheck("관리 화면 글자 하한", $"기준 22 · 하한 기준 20.9 · fontSizeMin {managedExpected:0.##}(배율 {scale:0.##})",
                managed == null ? "글자 없음" : $"기준 {managedScaled?.BaseSize} · 하한 기준 {managedScaled?.MinimumBaseSize:0.##} · fontSizeMin {managed.fontSizeMin:0.##}",
                managed != null && managedScaled != null && Mathf.Approximately(managedScaled.BaseSize, 22f) &&
                Mathf.Abs(managedScaled.MinimumBaseSize - 20.9f) < 0.01f && Mathf.Abs(managed.fontSizeMin - managedExpected) < 0.01f);

            // 관리 화면 밖 — 어느 화면에도 속하지 않은 임시 부모에 일반 24 글자를 만든다(EventScreen 화자 이름과 같은 크기).
            var probeParent = new GameObject("RuiFontProbe", typeof(RectTransform));
            TextMeshProUGUI general = UIBuild.Text("Probe", probeParent.transform, "일반 글자", 24f, UITheme.TextPrimary);
            UIScaledText generalScaled = general.GetComponent<UIScaledText>();
            RuiCheck("일반 화면 글자 하한(변경 없음)", "기준 24 · 하한 기준 0 · fontSizeMin 19.68(배율과 무관)",
                $"기준 {generalScaled?.BaseSize} · 하한 기준 {generalScaled?.MinimumBaseSize:0.##} · fontSizeMin {general.fontSizeMin:0.##}",
                generalScaled != null && generalScaled.MinimumBaseSize == 0f && Mathf.Abs(general.fontSizeMin - 19.68f) < 0.01f);
            Destroy(probeParent);
        }

        /// <summary>목록이 칸보다 길어도 맨 끝 항목이 굴려서 보이는지. 맨 아래로 굴린 상태로 남긴다.</summary>
        private IEnumerator RuiScrollReach(string label, GameObject root, string scrollName, string lastName)
        {
            ScrollRect scroll = RuiFind(root, scrollName)?.GetComponent<ScrollRect>();
            Transform last = RuiFind(root, lastName);
            if (scroll == null || last == null)
            {
                RuiCheck($"{label} 스크롤", $"{scrollName} · {lastName}", $"스크롤 {(scroll != null)} · 끝 항목 {(last != null)}", false);
                yield break;
            }
            scroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Rect view = RuiRect(scroll.transform);
            bool visibleBefore = RuiInside(view, RuiRect(last));
            float contentHeight = scroll.content.rect.height;
            float viewHeight = ((RectTransform)scroll.transform).rect.height;
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            bool visibleAfter = RuiInside(RuiRect(scroll.transform), RuiRect(last));
            RuiCheck($"{label} 스크롤 끝 항목", "목록이 칸보다 길고, 맨 아래로 굴리면 끝 항목이 칸 안에 보인다",
                $"내용 {contentHeight:0} / 칸 {viewHeight:0} · 처음 보임 {visibleBefore} · 굴린 뒤 보임 {visibleAfter}",
                contentHeight > viewHeight && !visibleBefore && visibleAfter);
        }

        // ── 시나리오 ─────────────────────────────────────────────────

        private IEnumerator ScenarioRewardUi(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            int stage = CombatStage(config, 6, 6);
            yield return PrepareStage(config, stage, false, true, true);
            GameManager game = GameManager.Instance;
            UIManager ui = game.uiManager;
            InventoryManager inventory = game.inventoryManager;

            Unit sei = AliveAllies().FirstOrDefault(unit => unit.ID == SeiId);
            Unit indra = AliveAllies().FirstOrDefault(unit => unit.ID == IndraId);
            Unit kubera = AliveAllies().FirstOrDefault(unit => unit.ID == KuberaId);
            Unit agni = AliveAllies().FirstOrDefault(unit => unit.ID == AgniId);
            Unit surya = AliveAllies().FirstOrDefault(unit => unit.ID == SuryaId);
            if (sei == null || indra == null || kubera == null || agni == null || surya == null)
            {
                FailCurrent("세이 · 인드라 · 쿠베라 · 아그니 · 수리야를 찾지 못했다. lineup을 party5_reward_ui로 지정해야 한다.");
                yield break;
            }
            var party = new List<Unit> { sei, indra, kubera, agni, surya };
            RuiCheck("준비 페이즈 · 스테이지", $"Preparation · {stage}", $"{game.gameState} · {game.RoundManager.Stage}",
                game.gameState == BaseEnums.GameState.Preparation && game.RoundManager.Stage == stage);
            Debug.Log($"[보상UI검증] 해상도 {Screen.width}×{Screen.height}");

            // 시작 상태 — 끝에서 되돌린다.
            var savedCodes = party.ToDictionary(unit => unit, unit => unit.RewardCodeIds.ToList());
            int savedGold = inventory.Gold;
            var savedItems = new List<int>(inventory.ItemIdsInHand ?? new List<int>());
            var savedValuables = inventory.Valuables.Select(item => new ValuableHolding { itemId = item.itemId, gold = item.gold }).ToList();
            int savedTonic = game.PendingTonicItemId;
            int savedPurchases = game.ShopPurchaseCount;

            // 상위 대체를 볼 하위 · 상위 한 쌍(상점 코드가 아닌 구현된 것). 기본은 614 → 723.
            int lower = 614, upper = RewardCodeCatalog.UpperOf(614);
            if (upper <= 0 || !Codes.Passive.Reward.RewardCodeFactory.IsImplemented(upper))
            {
                CodeCatalog.Entry pair = RewardCodeCatalog.All().FirstOrDefault(entry =>
                    entry.supersededBy > 0 && !entry.shopFixed && !RuiShopCodeIds.Contains(entry.supersededBy) &&
                    Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.id) &&
                    Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.supersededBy));
                lower = pair?.id ?? 0;
                upper = pair?.supersededBy ?? 0;
            }

            // 칸 준비: 세이 7칸 가득(T1), 인드라 빈 칸, 쿠베라 701의 상위(801) + 702 보유, 아그니 하위 코드, 수리야 빈 칸.
            foreach (Unit unit in party)
                foreach (int id in unit.RewardCodeIds.ToList()) unit.RemoveRewardCode(id);
            foreach (int id in new[] { 501, 502, 503, 504, 505, 506, 507 }) sei.TryGrantRewardCode(id);
            kubera.TryGrantRewardCode(RewardCodeCatalog.UpperOf(701));
            kubera.TryGrantRewardCode(702);
            foreach (int id in new[] { 508, lower, 509 }) if (id > 0) agni.TryGrantRewardCode(id);
            RuiCheck("칸 준비", "세이 7 · 인드라 0 · 쿠베라 701상위+702 · 아그니 하위 포함 3",
                $"세이 {RuiCodes(sei)} · 인드라 {RuiCodes(indra)} · 쿠베라 {RuiCodes(kubera)} · 아그니 {RuiCodes(agni)}",
                sei.RewardCodeIds.Count == Unit.RewardCodeSlotCount && indra.RewardCodeIds.Count == 0 &&
                kubera.RewardCodeIds.Contains(702) && kubera.RewardCodeIds.Contains(RewardCodeCatalog.UpperOf(701)) &&
                lower > 0 && agni.RewardCodeIds.Contains(lower));

            yield return RuiCodexChecks(ui, party, sei, indra, kubera, surya);
            yield return RuiShopChecks(game, ui, inventory, stage, party, sei, indra, kubera, agni, surya);
            yield return RuiInventoryChecks(game, ui, inventory, sei, surya);
            yield return RuiRewardUpperCheck(ui, agni, lower, upper);

            // 되돌리기 — 다음 시나리오가 깨끗한 칸 · 골드 · 보급에서 시작하게 한다.
            UITooltip.HideAny();
            ui.HideRewardPanel();
            ui.HideShopPanel();
            ui.HideInventoryPanel();
            if (ui.CodexForVerification?.IsOpen == true) ui.ToggleCodex();
            yield return PrepareStage(config, stage, false, true, true);
            foreach (Unit unit in party)
            {
                foreach (int id in unit.RewardCodeIds.ToList()) unit.RemoveRewardCode(id);
                foreach (int id in savedCodes[unit]) unit.TryGrantRewardCode(id);
            }
            inventory.RestoreGold(savedGold);
            inventory.ItemIdsInHand = savedItems;
            inventory.RestoreValuables(savedValuables);
            game.RestorePendingTonic(savedTonic);
            game.RestorePreparationState(game.RestUsedThisPreparation, game.ShortRestCharges, savedPurchases);
            RuiFinish();
        }

        // ── 파티 창 ─────────────────────────────────────────────────

        private IEnumerator RuiCodexChecks(UIManager ui, List<Unit> party, Unit sei, Unit indra, Unit kubera, Unit surya)
        {
            // 1) 포커스 — 유닛을 눌러 연 경로(ShowUnitDetail)는 그 유닛을 실제로 고른다.
            if (ui.CodexForVerification?.IsOpen == true) ui.ToggleCodex();
            ui.ShowUnitDetail(kubera);
            yield return WaitUnscaled(0.6f);
            GameObject root = KbCodexRoot();
            RuiCheck("포커스 열기 — 쿠베라", "선택 쿠베라 · 머리 이름 쿠베라",
                $"선택 {ui.CodexForVerification?.SelectedUnit?.UnitName ?? "없음"} · 머리 {RuiText(root?.transform, "SelectedName") ?? "없음"}",
                ui.CodexForVerification?.SelectedUnit == kubera && RuiText(root?.transform, "SelectedName") == kubera.UnitName);

            // 2) 레일 — 다섯 명 각각 한 줄, 레벨 · 자리 · 코드 점유 수가 실제와 같다.
            List<Transform> rows = RuiAll(root, name => name.StartsWith("Member_", StringComparison.Ordinal));
            var expectedNames = party.Select(unit => unit.UnitName).OrderBy(name => name).ToList();
            var shownNames = rows.Select(row => row.name.Substring("Member_".Length)).OrderBy(name => name).ToList();
            RuiCheck("레일 구성", string.Join(",", expectedNames), string.Join(",", shownNames), expectedNames.SequenceEqual(shownNames));
            foreach (Unit unit in party)
            {
                Transform row = RuiFind(root, $"Member_{unit.UnitName}");
                string place = !unit.isActive ? "전투불능" : unit.IsBench ? "대기석"
                    : unit.currentCell?.xPos == -1 ? "전열" : unit.currentCell?.xPos == -2 ? "후열" : "미배치";
                string expectState = $"Lv.{unit.Level} · {place}";
                string expectSlots = $"코드 {unit.RewardCodeIds.Count}/{Unit.RewardCodeSlotCount}";
                RuiCheck($"레일 줄 — {unit.UnitName}", $"{expectState} · {expectSlots}",
                    $"{RuiText(row, "State")} · {RuiText(row, "Slots")}",
                    RuiText(row, "State") == expectState && RuiText(row, "Slots") == expectSlots);
            }
            List<Rect> rowRects = rows.Select(RuiRect).ToList();
            bool rowsApart = rowRects.Count == 5 && !rowRects.Where((a, i) => rowRects.Skip(i + 1).Any(b => RuiOverlaps(a, b))).Any();
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            RuiCheck("레일 줄 겹침 · 화면 안", "다섯 줄이 서로 겹치지 않고 화면 안", $"줄 {rowRects.Count} · 화면 안 {rowRects.All(rect => RuiInside(screen, rect))}",
                rowsApart && rowRects.All(rect => RuiInside(screen, rect)));
            yield return CaptureStill("rui_codex_focus_kubera");
            RuiFontFloorChecks(root);

            // 3) 닫았다 다시 열기(포커스 없음) — 고른 유닛과 탭을 유지한다.
            ui.ToggleCodex();
            yield return WaitUnscaled(0.25f);
            ui.ToggleCodex();
            yield return WaitUnscaled(0.5f);
            RuiCheck("다시 열기 — 선택 유지", "쿠베라 · Sheet",
                $"{ui.CodexForVerification?.SelectedUnit?.UnitName} · {ui.CodexForVerification?.ActiveTabName}",
                ui.CodexForVerification?.SelectedUnit == kubera && ui.CodexForVerification?.ActiveTabName == "Sheet");

            // 4) 세이 — 가득 찬 7칸: 칸마다 효과(윗줄) · 이름(아랫줄)이 실제 코드와 같다.
            root = KbCodexRoot();
            bool seiPressed = RuiClick(RuiFind(root, $"Member_{sei.UnitName}"));
            RuiCheck("레일 클릭 — 세이", "누름", seiPressed ? "누름" : "못 누름", seiPressed);
            yield return WaitUnscaled(0.4f);
            root = KbCodexRoot();
            IReadOnlyList<int> seiCodes = sei.RewardCodeIds;
            var slotMismatch = new List<string>();
            for (int i = 0; i < Unit.RewardCodeSlotCount; i++)
            {
                Transform slot = RuiFind(root, $"RewardSlot{i}");
                if (RuiText(slot, "Name") != RuiName(seiCodes[i]) || RuiText(slot, "Effect") != RuiSummary(seiCodes[i]))
                    slotMismatch.Add($"{i + 1}번 {RuiText(slot, "Name") ?? "없음"}");
            }
            Transform slotsRoot = RuiFind(root, "RewardSlots");
            RuiCheck("가득 찬 7칸 — 세이", "7 / 7 · 칸마다 효과 · 이름 일치",
                $"{RuiText(slotsRoot, "Heading")} · 불일치 {(slotMismatch.Count == 0 ? "없음" : string.Join(", ", slotMismatch))}",
                RuiText(slotsRoot, "Heading") == $"보상 코드  7 / {Unit.RewardCodeSlotCount}" && slotMismatch.Count == 0);

            // 칸 · 상세 · 스탯 판이 서로 겹치지 않는다.
            var slotRects = Enumerable.Range(0, Unit.RewardCodeSlotCount).Select(i => RuiFind(root, $"RewardSlot{i}"))
                .Where(item => item != null).Select(RuiRect).ToList();
            Rect detailRect = RuiRect(RuiFind(root, "CodeDetail"));
            Rect statsRect = RuiRect(RuiFind(root, "Stats"));
            Rect identityRect = RuiRect(RuiFind(root, "Identity"));
            bool apart = slotRects.Count == 7 &&
                         !slotRects.Where((a, i) => slotRects.Skip(i + 1).Any(b => RuiOverlaps(a, b))).Any() &&
                         slotRects.All(rect => !RuiOverlaps(rect, detailRect) && !RuiOverlaps(rect, statsRect) && !RuiOverlaps(rect, identityRect)) &&
                         !RuiOverlaps(detailRect, statsRect) && RuiInside(screen, statsRect) && RuiInside(screen, identityRect);
            RuiCheck("빌드 판 배치", "7칸 · 상세 · 스탯 · 머리가 겹치지 않고 화면 안", apart ? "겹침 없음" : "겹치거나 화면 밖", apart);
            yield return CaptureStill("rui_codex_sei_full");

            // 5) 칸을 누르면 상세 — 효과 전문 · 태그 · 상위 경로.
            int picked = seiCodes[2];
            RuiClick(RuiFind(root, "RewardSlot2"));
            yield return WaitUnscaled(0.3f);
            root = KbCodexRoot();
            string detail = RuiParsed(RuiFind(root, "CodeDetail"), "DetailBody") ?? "";
            string tags = string.Join(" · ", UnitTagCatalog.Resolve(RewardCodeCatalog.TagsOf(picked)).Select(tag => tag.Name));
            int pickedUpper = RewardCodeCatalog.UpperOf(picked);
            RuiCheck("칸 상세 — 3번 칸", $"효과 전문 · 태그「{tags}」 · 상위 {(pickedUpper > 0 ? RuiName(pickedUpper) : "없음")}",
                Shorten(detail.Replace('\n', '/')) + $" (길이 {detail.Length})",
                detail.Contains(RuiSummary(picked)) && (tags.Length == 0 || detail.Contains(tags)) &&
                (pickedUpper <= 0 || detail.Contains(RuiName(pickedUpper))));
            yield return CaptureStill("rui_codex_slot_detail");

            // 6) 인드라 — 빈 7칸과 빈 칸 안내, 스탯 판의 값.
            RuiClick(RuiFind(root, $"Member_{indra.UnitName}"));
            yield return WaitUnscaled(0.4f);
            root = KbCodexRoot();
            int emptyCount = Enumerable.Range(0, Unit.RewardCodeSlotCount)
                .Count(i => RuiText(RuiFind(root, $"RewardSlot{i}"), "Empty") == "빈 칸");
            RuiClick(RuiFind(root, "RewardSlot0"));
            yield return WaitUnscaled(0.3f);
            root = KbCodexRoot();
            string guide = RuiParsed(RuiFind(root, "CodeDetail"), "DetailBody") ?? "";
            RuiCheck("빈 7칸 — 인드라", "빈 칸 7 · 안내에 전투 보상 · 상점 · 7칸 교체 · 인장 · 두루마리",
                $"빈 칸 {emptyCount} · 안내 {Shorten(guide.Replace('\n', '/'))}",
                emptyCount == Unit.RewardCodeSlotCount && guide.Contains("전투 보상") && guide.Contains("상점") &&
                guide.Contains("7칸") && guide.Contains("인장") && guide.Contains("두루마리"));
            var statMismatch = new List<string>();
            foreach (BaseEnums.StatType stat in Enum.GetValues(typeof(BaseEnums.StatType)).Cast<BaseEnums.StatType>())
            {
                string value = RuiText(RuiFind(root, $"Stat_{stat}"), "Value");
                if (value == null || !value.EndsWith(UnitStats.Format(stat, indra.StatValue(stat)), StringComparison.Ordinal))
                    statMismatch.Add($"{stat} {value ?? "없음"}");
            }
            RuiCheck("8스탯 판 — 인드라 최종값", "8줄 모두 최종 스탯으로 끝남",
                statMismatch.Count == 0 ? "일치" : string.Join(", ", statMismatch), statMismatch.Count == 0);
            RuiReadable("파티 창 빌드", root);
            yield return CaptureStill("rui_codex_indra_empty");

            // 7) 코드 탭 — 유닛마다 레일로 골라 고유 특성 · 일반 · 초월기 · (있으면) 특수행동 · 보상 코드 전문.
            RuiClick(RuiFind(root, "Tab코드"));
            yield return WaitUnscaled(0.3f);
            foreach (Unit unit in party)
            {
                root = KbCodexRoot();
                RuiClick(RuiFind(root, $"Member_{unit.UnitName}"));
                yield return WaitUnscaled(0.3f);
                root = KbCodexRoot();
                Transform column = RuiFind(root, $"Col_{unit.UnitName}");
                List<(string Name, string Kind)> entries = KbEntries(column);
                bool basics = entries.Any(entry => entry.Kind == "고유 특성") && entries.Any(entry => entry.Kind == "초월기") &&
                              entries.Any(entry => entry.Name == "일반행동" || entry.Kind == "일반행동");
                int specials = entries.Count(entry => entry.Kind == "특수행동");
                int rewardEntries = entries.Count(entry => entry.Kind.Contains("번 칸 · T"));
                List<TextMeshProUGUI> summaries = column == null ? new List<TextMeshProUGUI>()
                    : column.GetComponentsInChildren<TextMeshProUGUI>(false).Where(label => label.name == "Summary").ToList();
                foreach (TextMeshProUGUI label in summaries) label.ForceMeshUpdate();
                List<int> missing = unit.RewardCodeIds.Where(id => !summaries.Any(label => label.text == RuiSummary(id))).ToList();
                bool clipped = summaries.Any(label => label.preferredHeight > label.rectTransform.rect.height + 2f);
                RuiCheck($"코드 탭 — {unit.UnitName}",
                    $"기본 3종 · 특수행동 {(unit.ActiveSpecialCode != null ? 1 : 0)} · 보상 {unit.RewardCodeIds.Count} 전문 · 잘림 없음",
                    $"기본 {basics} · 특수행동 {specials} · 보상 {rewardEntries} · 전문 누락 {missing.Count} · 잘림 {clipped}",
                    basics && specials == (unit.ActiveSpecialCode != null ? 1 : 0) &&
                    rewardEntries == unit.RewardCodeIds.Count && missing.Count == 0 && !clipped);
                if (unit == sei) yield return CaptureStill("rui_codex_codes_sei");
                if (unit == kubera)
                {
                    string live = KbLiveText(column);
                    RuiCheck("코드 탭 — 골드 러쉬 실시간 줄", "현재 금력 배율 …", live ?? "없음", live != null && live.StartsWith("현재 금력 배율 ×"));
                    yield return CaptureStill("rui_codex_codes_kubera");
                }
            }
            RuiReadable("파티 창 코드", KbCodexRoot());

            // 8) 다시 열기 — 코드 탭 · 마지막 유닛 유지.
            ui.ToggleCodex();
            yield return WaitUnscaled(0.25f);
            ui.ToggleCodex();
            yield return WaitUnscaled(0.4f);
            RuiCheck("다시 열기 — 탭 · 선택 유지", $"Codes · {party[party.Count - 1].UnitName}",
                $"{ui.CodexForVerification?.ActiveTabName} · {ui.CodexForVerification?.SelectedUnit?.UnitName}",
                ui.CodexForVerification?.ActiveTabName == "Codes" && ui.CodexForVerification?.SelectedUnit == party[party.Count - 1]);

            // 8-1) 열린 창의 값 갱신 — 체력 · 레벨 · 스탯 · 레일 칸 수 · 전투불능이 계층을 다시 만들지 않고 바뀐다.
            root = KbCodexRoot();
            RuiClick(RuiFind(root, $"Member_{sei.UnitName}"));
            yield return WaitUnscaled(0.3f);
            RuiClick(RuiFind(KbCodexRoot(), "Tab빌드"));
            yield return WaitUnscaled(0.3f);
            RuiClick(RuiFind(KbCodexRoot(), "RewardSlot2"));
            yield return WaitUnscaled(0.3f);
            root = KbCodexRoot();
            TextMeshProUGUI vitals = RuiLabelOf(RuiFind(root, "Identity"), "Vitals");
            TextMeshProUGUI meta = RuiLabelOf(RuiFind(root, "Identity"), "Meta");
            TextMeshProUGUI attack = RuiLabelOf(RuiFind(root, "Stat_Attack"), "Value");
            TextMeshProUGUI seiState = RuiLabelOf(RuiFind(root, $"Member_{sei.UnitName}"), "State");
            TextMeshProUGUI indraState = RuiLabelOf(RuiFind(root, $"Member_{indra.UnitName}"), "State");
            TextMeshProUGUI indraSlots = RuiLabelOf(RuiFind(root, $"Member_{indra.UnitName}"), "Slots");
            int hpBefore = sei.HpCurr, levelBefore = sei.Level, indraCodes = indra.RewardCodeIds.Count;
            sei.ModifyHp(Mathf.Max(1, sei.HpMax / 3));
            sei.GrantLevels(1);
            indra.TryGrantRewardCode(508);
            yield return KbFrames(4);
            string seiPlace = sei.currentCell?.xPos == -1 ? "전열" : sei.currentCell?.xPos == -2 ? "후열" : sei.IsBench ? "대기석" : "미배치";
            bool sameLabels = vitals != null && vitals.gameObject.activeInHierarchy && attack != null && attack.gameObject.activeInHierarchy &&
                              seiState != null && seiState.gameObject.activeInHierarchy;
            string expectAttack = UnitStats.Format(BaseEnums.StatType.Attack, sei.StatValue(BaseEnums.StatType.Attack));
            RuiCheck("열린 창 값 갱신", $"체력 {sei.HpCurr:N0} · Lv.{sei.Level} · 공격 …{expectAttack} · 인드라 코드 {indraCodes + 1}/7 · 같은 글자 · 칸 3 상세 유지",
                $"{Shorten(vitals?.text ?? "")} · {Shorten(meta?.text ?? "")} · {attack?.text} · {indraSlots?.text} · 같은 글자 {sameLabels} · {RuiText(RuiFind(root, "CodeDetail"), "Heading")}",
                sameLabels && vitals.text.StartsWith($"체력 {sei.HpCurr:N0} / {sei.HpMax:N0}") &&
                meta.text.StartsWith($"Lv.{sei.Level}") && seiState.text == $"Lv.{sei.Level} · {seiPlace}" &&
                attack.text.EndsWith(expectAttack) && indraSlots?.text == $"코드 {indraCodes + 1}/{Unit.RewardCodeSlotCount}" &&
                RuiText(RuiFind(root, "CodeDetail"), "Heading") == "칸 3 상세" && ui.CodexForVerification?.SelectedUnit == sei);
            indra.isActive = false;
            yield return KbFrames(4);
            bool deadShown = indraState != null && indraState.text.EndsWith("전투불능") && indraState.color == UITheme.Danger;
            indra.isActive = true;
            yield return KbFrames(4);
            RuiCheck("레일 전투불능 갱신", "전투불능(적색) → 되돌림", $"{deadShown} → {indraState?.text}",
                deadShown && indraState != null && !indraState.text.EndsWith("전투불능"));
            yield return CaptureStill("rui_codex_live_values");
            indra.RemoveRewardCode(508);
            sei.DebugSetLevel(levelBefore);
            sei.ModifyHp(hpBefore);

            // 8-2) 구조 변화 — 고른 유닛의 코드 칸이 바뀌면 다시 그리되 코드 탭 스크롤 · 선택 · 탭을 지킨다.
            RuiClick(RuiFind(KbCodexRoot(), "Tab코드"));
            yield return WaitUnscaled(0.3f);
            root = KbCodexRoot();
            ScrollRect codeScroll = RuiFind(root, "CodeScroll")?.GetComponent<ScrollRect>();
            float scrollBefore = -1f, scrollAfter = -2f;
            int removedCode = sei.RewardCodeIds.Last();
            if (codeScroll != null)
            {
                codeScroll.content.anchoredPosition = new Vector2(codeScroll.content.anchoredPosition.x, 260f);
                yield return KbFrames(2);
                scrollBefore = codeScroll.content.anchoredPosition.y;
                sei.RemoveRewardCode(removedCode);
                yield return WaitUnscaled(0.3f);
                root = KbCodexRoot();
                ScrollRect rebuilt = RuiFind(root, "CodeScroll")?.GetComponent<ScrollRect>();
                scrollAfter = rebuilt != null ? rebuilt.content.anchoredPosition.y : -2f;
                int rewardEntries = KbEntries(RuiFind(root, $"Col_{sei.UnitName}")).Count(entry => entry.Kind.Contains("번 칸 · T"));
                RuiCheck("구조 변화 — 다시 그림 · 스크롤 유지", $"보상 항목 {sei.RewardCodeIds.Count} · 스크롤 {scrollBefore:0} 유지 · Codes · 세이",
                    $"보상 항목 {rewardEntries} · 스크롤 {scrollAfter:0} · 새 목록 {rebuilt != codeScroll} · {ui.CodexForVerification?.ActiveTabName} · {ui.CodexForVerification?.SelectedUnit?.UnitName}",
                    rewardEntries == sei.RewardCodeIds.Count && rebuilt != null && rebuilt != codeScroll &&
                    Mathf.Abs(scrollAfter - scrollBefore) < 2f && scrollBefore > 100f &&
                    ui.CodexForVerification?.ActiveTabName == "Codes" && ui.CodexForVerification?.SelectedUnit == sei);
                sei.TryGrantRewardCode(removedCode);
                yield return WaitUnscaled(0.3f);
            }
            else RuiCheck("구조 변화 — 다시 그림 · 스크롤 유지", "CodeScroll", "없음", false);

            // 8-3) 고른 유닛이 파티에서 사라지면 유효한 유닛으로 바꾸고, 돌아와도 바꾼 선택을 유지한다.
            RuiClick(RuiFind(KbCodexRoot(), $"Member_{surya.UnitName}"));
            yield return WaitUnscaled(0.3f);
            List<Unit> heroes = GridManager.Instance.heroList;
            int suryaIndex = heroes.IndexOf(surya);
            heroes.RemoveAt(suryaIndex);
            yield return WaitUnscaled(0.3f);
            root = KbCodexRoot();
            Unit fallback = ui.CodexForVerification?.SelectedUnit;
            Unit expectedFallback = CodexScreen.PartyMembers().FirstOrDefault();
            int railWithout = RuiAll(root, name => name.StartsWith("Member_", StringComparison.Ordinal)).Count;
            heroes.Insert(suryaIndex, surya);
            yield return WaitUnscaled(0.3f);
            root = KbCodexRoot();
            int railBack = RuiAll(root, name => name.StartsWith("Member_", StringComparison.Ordinal)).Count;
            RuiCheck("선택 유닛이 사라짐 → 대체", $"수리야 빠지면 레일 4 · 선택 {expectedFallback?.UnitName}(남은 첫 유닛) · 돌아오면 레일 5 · 선택 유지",
                $"레일 {railWithout} · 선택 {fallback?.UnitName} · 레일 {railBack} · 선택 {ui.CodexForVerification?.SelectedUnit?.UnitName}",
                railWithout == 4 && fallback != null && fallback == expectedFallback && fallback != surya &&
                railBack == 5 && ui.CodexForVerification?.SelectedUnit == fallback);

            // 9) 공용 보급 열기 — 파티 창을 닫고 정식 경로로 연다(모달 겹침 없음).
            root = KbCodexRoot();
            bool pressed = RuiClick(RuiFind(root, "OpenSupply"));
            yield return WaitUnscaled(0.5f);
            RuiCheck("파티 창 → 공용 보급", "버튼 눌림 · 파티 창 닫힘 · 공용 보급 열림",
                $"눌림 {pressed} · 파티 창 {ui.CodexForVerification?.IsOpen} · 공용 보급 {ui.IsInventoryVisible}",
                pressed && ui.CodexForVerification?.IsOpen == false && ui.IsInventoryVisible);
            yield return CaptureStill("rui_codex_to_supply");
            ui.HideInventoryPanel();
            yield return WaitUnscaled(0.2f);
        }

        // ── 상점 ────────────────────────────────────────────────────

        private IEnumerator RuiShopChecks(GameManager game, UIManager ui, InventoryManager inventory, int stage, List<Unit> party,
            Unit sei, Unit indra, Unit kubera, Unit agni, Unit surya)
        {
            int price = RuiShopCodeBasePrice * stage;
            inventory.RestoreGold(20000);
            game.RestorePreparationState(game.RestUsedThisPreparation, game.ShortRestCharges, 0);
            game.OpenShopFromPreparation();
            yield return WaitUnscaled(0.6f);
            GameObject root = GameObject.Find("ShopScreen");
            ShopScreen shop = ui.ShopForVerification;

            // 1) 기본 탭은 코드 — 세 장의 이름 · 가격 · 효과 전문, 지갑 · 남은 구매.
            var expectedIds = RewardCodeCatalog.All()
                .Where(entry => entry.shopFixed && Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.id))
                .Select(entry => entry.id).ToList();
            RuiCheck("상점 고정 코드 재고", string.Join(",", RuiShopCodeIds), string.Join(",", expectedIds), expectedIds.SequenceEqual(RuiShopCodeIds));
            var cardProblems = new List<string>();
            for (int i = 0; i < RuiShopCodeIds.Length; i++)
            {
                Transform card = RuiFind(root, $"CodeGood_{i}");
                TextMeshProUGUI effect = RuiLabelOf(card, "Effect");
                effect?.ForceMeshUpdate();
                if (RuiText(card, "Name") != RuiName(RuiShopCodeIds[i])) cardProblems.Add($"{i} 이름 {RuiText(card, "Name")}");
                if (RuiText(card, "Price") != $"{price:N0} G") cardProblems.Add($"{i} 가격 {RuiText(card, "Price")}");
                if (effect == null || effect.text != RuiSummary(RuiShopCodeIds[i])) cardProblems.Add($"{i} 효과");
                else if (effect.preferredHeight > effect.rectTransform.rect.height + 2f) cardProblems.Add($"{i} 효과 넘침");
            }
            RuiCheck("상점 기본 탭 = 코드", $"Code · 남은 구매 2 / 2 · 지갑 20,000 · 카드 3장 이름/가격 {price:N0} G/효과 전문",
                $"{shop?.ActiveTabName} · {RuiText(root?.transform, "Purchases")} · {RuiText(root?.transform, "Wallet")} · 문제 {(cardProblems.Count == 0 ? "없음" : string.Join(", ", cardProblems))}",
                shop?.ActiveTabName == "Code" && RuiText(root?.transform, "Purchases") == $"남은 구매 2 / {GameManager.MaxShopPurchases}" &&
                (RuiText(root?.transform, "Wallet") ?? "").Contains("20,000") && cardProblems.Count == 0);

            // 아군별 받을 수 있는지 — 쿠베라는 701의 상위 · 702를 이미 가졌다. 세이는 칸이 가득 차 덮어쓰기다.
            string receivers701 = RuiParsed(RuiFind(root, "CodeGood_0"), "Receivers") ?? "";
            string receivers702 = RuiParsed(RuiFind(root, "CodeGood_1"), "Receivers") ?? "";
            RuiCheck("받을 아군 표시(짧은 줄)", $"701: {kubera.UnitName} · 상위 보유 · {sei.UnitName} · 7/7 · 교체 · {indra.UnitName} · 0/7 · 새 칸 / 702: {kubera.UnitName} · 이미 보유",
                $"{receivers701.Replace('\n', '/')} | {receivers702.Replace('\n', '/')}",
                receivers701.Contains($"{kubera.UnitName} · 상위 보유") && receivers701.Contains($"{sei.UnitName} · 7/7 · 교체") &&
                receivers701.Contains($"{indra.UnitName} · 0/7 · 새 칸") && receivers702.Contains($"{kubera.UnitName} · 이미 보유"));
            RuiReadable("상점 코드", root);
            yield return CaptureStill("rui_shop_code_default");

            // 2) 툴팁 — 코드 카드에 고정 좌표로 띄워 효과 전문을 확인한다.
            TooltipTrigger trigger = RuiFind(root, "CodeGood_2")?.GetComponent<TooltipTrigger>();
            if (trigger != null)
            {
                Rect rect = RuiRect(trigger.transform);
                trigger.ShowAt(rect.center);
                yield return KbFrames();
                string body = KbTooltipBody();
                RuiCheck("코드 카드 툴팁", "효과 전문 포함", Shorten(body ?? "없음"), body != null && body.Contains(RuiSummary(703)));
                yield return CaptureStill("rui_shop_code_tooltip", keepTooltip: true);
                UITooltip.HideAny();
            }
            else RuiCheck("코드 카드 툴팁", "TooltipTrigger", "없음", false);

            // 3) 받을 아군에서 취소 — 값 · 횟수 · 칸 그대로.
            RuiClick(RuiFind(root, "CodeGood_0"));
            yield return WaitUnscaled(0.3f);
            List<string> targets = RuiTargetNames(root);
            string seiPreview = RuiText(RuiTargetCard(root, sei), "Preview") ?? "";
            RuiCheck("701 받을 아군 후보", "쿠베라 제외 4명 · 세이 덮어쓰기 미리보기",
                $"{string.Join(",", targets)} · 세이 {seiPreview}",
                targets.Count == 4 && !targets.Contains(kubera.UnitName) && seiPreview.Contains("덮어쓰기"));
            string targetGuide = RuiParsed(RuiFind(root, "TargetPicker"), "Guide") ?? "";
            RuiCheck("받을 아군 판 거래 요약", $"가격 {price:N0} G · 보유 20,000 G · 남은 구매 2/2",
                targetGuide.Replace('\n', '/'),
                targetGuide.Contains($"가격 {price:N0} G") && targetGuide.Contains("보유 20,000 G") &&
                targetGuide.Contains($"남은 구매 2/{GameManager.MaxShopPurchases}"));
            RuiReadable("상점 받을 아군 판", root);
            yield return CaptureStill("rui_shop_code_targets");
            RuiClick(RuiBack(root));
            yield return WaitUnscaled(0.3f);
            RuiCheck("받을 아군에서 취소 — 무과금", $"골드 20,000 · 남은 2 · 인드라 칸 0",
                $"골드 {inventory.Gold:N0} · 남은 {game.ShopPurchasesLeft} · 인드라 {indra.RewardCodeIds.Count}",
                inventory.Gold == 20000 && game.ShopPurchasesLeft == 2 && indra.RewardCodeIds.Count == 0);

            // 4) 버릴 코드에서 취소 — 세이의 7칸 그대로.
            string seiBefore = RuiCodes(sei);
            RuiClick(RuiFind(root, "CodeGood_0"));
            yield return WaitUnscaled(0.3f);
            RuiClick(RuiTargetCard(root, sei));
            yield return WaitUnscaled(0.3f);
            List<string> options = RuiAll(root, name => name.StartsWith("Option", StringComparison.Ordinal))
                .Select(option => RuiText(option, "Title")).ToList();
            RuiCheck("가득 찬 칸 → 버릴 코드 선택", "세이의 코드 7개", string.Join(",", options),
                options.SequenceEqual(sei.RewardCodeIds.Select(RuiName)));
            // 지워질 코드의 효과를 비교할 수 있어야 한다 — 선택지마다 효과 요약과 툴팁.
            var optionProblems = new List<string>();
            foreach (int id in sei.RewardCodeIds)
            {
                Transform option = RuiOption(root, RuiName(id));
                string subtitle = RuiText(option, "Subtitle") ?? "";
                string head = RuiSummary(id);
                head = head.Substring(0, Math.Min(8, head.Length));
                if (!subtitle.Contains(head) || option?.GetComponent<TooltipTrigger>() == null) optionProblems.Add(RuiName(id));
            }
            string overwriteGuide = RuiParsed(RuiFind(root, "CodeChoicePicker"), "Guide") ?? "";
            RuiCheck("버릴 코드 판 — 효과 비교 · 거래 요약", "선택지마다 효과 · 툴팁 · 안내에 가격",
                $"문제 {(optionProblems.Count == 0 ? "없음" : string.Join(",", optionProblems))} · {Shorten(overwriteGuide.Replace('\n', '/'))}",
                optionProblems.Count == 0 && overwriteGuide.Contains($"가격 {price:N0} G"));
            RuiReadable("상점 버릴 코드 판", root);
            yield return CaptureStill("rui_shop_code_overwrite_pick");
            RuiClick(RuiBack(root));
            yield return WaitUnscaled(0.3f);
            RuiCheck("버릴 코드에서 취소 — 무과금", $"골드 20,000 · 남은 2 · 세이 {seiBefore}",
                $"골드 {inventory.Gold:N0} · 남은 {game.ShopPurchasesLeft} · 세이 {RuiCodes(sei)}",
                inventory.Gold == 20000 && game.ShopPurchasesLeft == 2 && RuiCodes(sei) == seiBefore);

            // 5) 7칸 덮어쓰기 구매 — 504 자리에 701, 값은 마지막 확정에서만.
            int slot504 = sei.RewardCodeIds.ToList().IndexOf(504);
            RuiClick(RuiFind(root, "CodeGood_0"));
            yield return WaitUnscaled(0.3f);
            RuiClick(RuiTargetCard(root, sei));
            yield return WaitUnscaled(0.3f);
            RuiClick(RuiOption(root, RuiName(504)));
            yield return WaitUnscaled(0.4f);
            root = GameObject.Find("ShopScreen");
            RuiCheck("7칸 덮어쓰기 구매", $"504 칸({slot504 + 1})에 701 · 7칸 · 골드 {20000 - price:N0} · 남은 1",
                $"{RuiCodes(sei)} · 골드 {inventory.Gold:N0} · 남은 {game.ShopPurchasesLeft} · 안내 {RuiText(root?.transform, "Message")}",
                sei.RewardCodeIds.ToList().IndexOf(701) == slot504 && !sei.RewardCodeIds.Contains(504) &&
                sei.RewardCodeIds.Count == Unit.RewardCodeSlotCount && inventory.Gold == 20000 - price && game.ShopPurchasesLeft == 1);
            yield return CaptureStill("rui_shop_code_overwritten");

            // 6) 중복 — 702를 가진 쿠베라는 후보에 없다.
            RuiClick(RuiFind(root, "CodeGood_1"));
            yield return WaitUnscaled(0.3f);
            targets = RuiTargetNames(root);
            RuiCheck("중복 보유자 제외(702)", "쿠베라 없음", string.Join(",", targets), targets.Count > 0 && !targets.Contains(kubera.UnitName));
            RuiClick(RuiBack(root));
            yield return WaitUnscaled(0.3f);

            // 7) 골드 부족 — 카드에 사유, 눌러도 흐름이 열리지 않고 값도 그대로.
            inventory.RestoreGold(price - 1);
            RuiClick(RuiFind(root, "TabCode"));
            yield return WaitUnscaled(0.2f);
            root = GameObject.Find("ShopScreen");
            RuiClick(RuiFind(root, "CodeGood_2"));
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            string state = RuiText(RuiFind(root, "CodeGood_2"), "State");
            RuiCheck("골드 부족", $"사유 표시 · 흐름 안 열림 · 골드 {price - 1:N0}",
                $"상태 {state} · 후보 판 {RuiTargetNames(root).Count} · 골드 {inventory.Gold:N0}",
                state == "골드 부족" && RuiTargetNames(root).Count == 0 && inventory.Gold == price - 1);
            yield return CaptureStill("rui_shop_code_no_gold");

            // 8) 확정 직전 재검사 — 대상 고르는 사이 골드가 줄면 결제하지 않는다.
            inventory.RestoreGold(20000);
            RuiClick(RuiFind(GameObject.Find("ShopScreen"), "TabCode"));
            yield return WaitUnscaled(0.2f);
            root = GameObject.Find("ShopScreen");
            RuiClick(RuiFind(root, "CodeGood_2"));
            yield return WaitUnscaled(0.3f);
            inventory.RestoreGold(100);
            RuiClick(RuiTargetCard(root, indra));
            yield return WaitUnscaled(0.3f);
            RuiCheck("확정 재검사 — 골드", "인드라 703 없음 · 골드 100 · 남은 1",
                $"인드라 {RuiCodes(indra)} · 골드 {inventory.Gold} · 남은 {game.ShopPurchasesLeft}",
                !indra.RewardCodeIds.Contains(703) && inventory.Gold == 100 && game.ShopPurchasesLeft == 1);

            // 9) 확정 직전 재검사 — 준비 페이즈가 아니면 결제하지 않는다.
            inventory.RestoreGold(20000);
            root = GameObject.Find("ShopScreen");
            RuiClick(RuiFind(root, "CodeGood_2"));
            yield return WaitUnscaled(0.3f);
            game.gameState = BaseEnums.GameState.RoundInProgress;
            RuiClick(RuiTargetCard(root, indra));
            game.gameState = BaseEnums.GameState.Preparation;
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            RuiCheck("확정 재검사 — 준비 밖", "인드라 703 없음 · 골드 20,000 · 남은 1",
                $"인드라 {RuiCodes(indra)} · 골드 {inventory.Gold:N0} · 남은 {game.ShopPurchasesLeft} · 안내 {RuiText(root?.transform, "Message")}",
                !indra.RewardCodeIds.Contains(703) && inventory.Gold == 20000 && game.ShopPurchasesLeft == 1);

            // 10) 빈 칸 구매 — 인드라가 703을 받고 두 번째 구매가 끝난다.
            RuiClick(RuiFind(root, "CodeGood_2"));
            yield return WaitUnscaled(0.3f);
            RuiClick(RuiTargetCard(root, indra));
            yield return WaitUnscaled(0.3f);
            RuiCheck("빈 칸 구매", $"인드라 703 · 골드 {20000 - price:N0} · 남은 0",
                $"인드라 {RuiCodes(indra)} · 골드 {inventory.Gold:N0} · 남은 {game.ShopPurchasesLeft}",
                indra.RewardCodeIds.SequenceEqual(new[] { 703 }) && inventory.Gold == 20000 - price && game.ShopPurchasesLeft == 0);

            // 11) 구매 한도 — 세 번째는 흐름을 열지 않는다.
            root = GameObject.Find("ShopScreen");
            int goldBefore = inventory.Gold;
            RuiClick(RuiFind(root, "CodeGood_1"));
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            RuiCheck("구매 한도 2회", "사유 구매 한도 · 흐름 안 열림 · 골드 그대로",
                $"상태 {RuiText(RuiFind(root, "CodeGood_1"), "State")} · 후보 판 {RuiTargetNames(root).Count} · 골드 {inventory.Gold:N0}",
                RuiText(RuiFind(root, "CodeGood_1"), "State") == "구매 한도" && RuiTargetNames(root).Count == 0 && inventory.Gold == goldBefore);
            yield return CaptureStill("rui_shop_code_limit");

            // 12) 받을 아군 없음 — 다섯 명 모두 703을 가지면 막힌다.
            game.RestorePreparationState(game.RestUsedThisPreparation, game.ShortRestCharges, 0);
            sei.TryGrantRewardCode(703, sei.RewardCodeIds.Last());
            foreach (Unit unit in new[] { kubera, agni, surya }) unit.TryGrantRewardCode(703);
            RuiClick(RuiFind(root, "TabCode"));
            yield return WaitUnscaled(0.2f);
            root = GameObject.Find("ShopScreen");
            goldBefore = inventory.Gold;
            RuiClick(RuiFind(root, "CodeGood_2"));
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            RuiCheck("받을 아군 없음", "사유 받을 아군 없음 · 흐름 안 열림 · 골드 그대로",
                $"보유 {party.Count(unit => unit.RewardCodeIds.Contains(703))}/5 · 상태 {RuiText(RuiFind(root, "CodeGood_2"), "State")} · 후보 판 {RuiTargetNames(root).Count}",
                party.All(unit => unit.RewardCodeIds.Contains(703)) &&
                RuiText(RuiFind(root, "CodeGood_2"), "State") == "받을 아군 없음" && RuiTargetNames(root).Count == 0 && inventory.Gold == goldBefore);
            yield return CaptureStill("rui_shop_code_no_target");
            foreach (Unit unit in new[] { sei, kubera, agni, surya }) unit.RemoveRewardCode(703);
            sei.TryGrantRewardCode(507);

            // 13) 회복 · 성장 — 실제 진열 전부(RewardManager.BuildShopGoods)가 굴려서 닿는다.
            inventory.RestoreGold(20000);
            RuiClick(RuiFind(root, "TabConsumable"));
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            List<RewardDef> consumables = game.rewardManager.BuildShopGoods();
            List<Transform> rows = RuiAll(root, name => name.StartsWith("Good_", StringComparison.Ordinal));
            bool namesMatch = rows.Count == consumables.Count &&
                              consumables.Select((good, i) => RuiText(RuiFind(root, $"Good_{i}"), "Name") == good.displayName).All(ok => ok);
            RuiCheck("회복 · 성장 진열", $"{consumables.Count}줄 · 이름 일치", $"{rows.Count}줄 · 일치 {namesMatch} · 탭 {shop?.ActiveTabName}",
                consumables.Count > 0 && namesMatch && shop?.ActiveTabName == "Consumable");
            RuiReadable("상점 목록", root);
            yield return CaptureStill("rui_shop_consumable_top");
            yield return RuiScrollReach("회복 · 성장", root, "GoodsScroll", $"Good_{consumables.Count - 1}");
            yield return CaptureStill("rui_shop_consumable_bottom");

            // 14) 공용 보급 탭 — 식량 · 정화의 성수.
            RuiClick(RuiFind(root, "TabSupply"));
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            List<RewardDef> supplyPool = game.rewardManager.BuildShopItems();
            List<string> supplyNames = RuiAll(root, name => name.StartsWith("Good_", StringComparison.Ordinal))
                .Select(row => RuiText(row, "Name")).OrderBy(name => name).ToList();
            var expectedSupply = supplyPool.Select(good => good.displayName).OrderBy(name => name).ToList();
            string supplyTab = RuiText(RuiFind(root, "TabSupply"), "Label");
            RuiCheck("공용 보급 탭", $"탭 이름 공용 보급 · {string.Join(",", expectedSupply)}",
                $"탭 {supplyTab} · {string.Join(",", supplyNames)}",
                supplyTab == "공용 보급" && (expectedSupply.Count > 6 ? supplyNames.Count == 6 : supplyNames.SequenceEqual(expectedSupply)));
            yield return CaptureStill("rui_shop_supply");

            // 15) 판매 — 아홉 점이 굴려서 끝까지 닿고, 판매는 구매 횟수를 쓰지 않는다.
            for (int i = 0; i < 9; i++) inventory.AddValuable(9901 + i, 100 * (i + 1));
            int leftBefore = game.ShopPurchasesLeft;
            RuiClick(RuiFind(root, "TabSell"));
            yield return WaitUnscaled(0.3f);
            root = GameObject.Find("ShopScreen");
            int sellRows = RuiAll(root, name => name.StartsWith("Good_", StringComparison.Ordinal)).Count;
            RuiCheck("판매 진열", $"{inventory.Valuables.Count}줄", $"{sellRows}줄", sellRows == inventory.Valuables.Count && sellRows >= 9);
            yield return RuiScrollReach("판매", root, "GoodsScroll", $"Good_{sellRows - 1}");
            yield return CaptureStill("rui_shop_sell_bottom");
            int goldBeforeSell = inventory.Gold;
            int lastValue = inventory.Valuables[inventory.Valuables.Count - 1].gold;
            RuiClick(RuiFind(root, $"Good_{sellRows - 1}"));
            yield return WaitUnscaled(0.3f);
            RuiCheck("마지막 귀중품 판매", $"골드 +{lastValue} · 남은 구매 그대로({leftBefore})",
                $"골드 +{inventory.Gold - goldBeforeSell} · 남은 {game.ShopPurchasesLeft} · 주머니 {inventory.Valuables.Count}",
                inventory.Gold - goldBeforeSell == lastValue && game.ShopPurchasesLeft == leftBefore && inventory.Valuables.Count == sellRows - 1);
            root = GameObject.Find("ShopScreen");
            RuiClick(RuiFind(root, "SellAll"));
            yield return WaitUnscaled(0.3f);
            RuiCheck("전부 팔기", "주머니 0", inventory.Valuables.Count.ToString(), inventory.Valuables.Count == 0);

            // 16) 다시 열면 코드 탭부터.
            ui.HideShopPanel();
            yield return WaitUnscaled(0.2f);
            game.OpenShopFromPreparation();
            yield return WaitUnscaled(0.4f);
            RuiCheck("다시 열기 — 코드 탭", "Code", ui.ShopForVerification?.ActiveTabName, ui.ShopForVerification?.ActiveTabName == "Code");
            ui.HideShopPanel();
            yield return WaitUnscaled(0.2f);
        }

        // ── 공용 보급 ────────────────────────────────────────────────

        private IEnumerator RuiInventoryChecks(GameManager game, UIManager ui, InventoryManager inventory, Unit sei, Unit surya)
        {
            // 1) 빈 보급 — 다음 행동 안내.
            inventory.ItemIdsInHand = new List<int>();
            game.RestorePendingTonic(0);
            game.OpenInventoryFromPreparation();
            yield return WaitUnscaled(0.5f);
            GameObject root = GameObject.Find("InventoryScreen");
            string title = RuiText(root?.transform, "Title");
            string empty = RuiText(root?.transform, "Empty") ?? "";
            RuiCheck("빈 공용 보급", "제목 공용 보급 · 안내에 전투 보상 · 상점",
                $"제목 {title} · {Shorten(empty.Replace('\n', '/'))}",
                title == "공용 보급" && empty.Contains("전투 보상") && empty.Contains("상점"));
            yield return CaptureStill("rui_supply_empty");
            ui.HideInventoryPanel();

            // 2) 12종 초과 — 데이터의 보급품 전 종류를 넣고 끝까지 굴린다.
            List<ItemData> kinds = game.itemDataList.items
                .Where(item => item != null && !item.IsValuable && !item.eventOnly).ToList();
            var bag = new List<int>();
            foreach (ItemData item in kinds) { bag.Add(item.id); bag.Add(item.id); }
            inventory.ItemIdsInHand = bag;
            game.OpenInventoryFromPreparation();
            yield return WaitUnscaled(0.5f);
            root = GameObject.Find("InventoryScreen");
            List<int> shownIds = RuiAll(root, name => name.StartsWith("Item_", StringComparison.Ordinal))
                .Select(card => int.TryParse(card.name.Substring("Item_".Length), out int id) ? id : 0).ToList();
            RuiCheck("공용 보급 전 종류", $"{kinds.Count}종(> 12) 카드", $"{shownIds.Distinct().Count()}종",
                kinds.Count > 12 && kinds.All(item => shownIds.Contains(item.id)));
            RuiReadable("공용 보급", root);
            yield return CaptureStill("rui_supply_full_top");
            var stacks = inventory.SharedStacks().ToList();
            yield return RuiScrollReach("공용 보급", root, "ItemScroll", $"Item_{stacks[stacks.Count - 1].item.id}");
            yield return CaptureStill("rui_supply_full_bottom");

            // 3) 비약 — 한 전투에 1개.
            ItemData firstTonic = kinds.FirstOrDefault(item => item.IsTonic);
            ItemData secondTonic = kinds.Where(item => item.IsTonic).Skip(1).FirstOrDefault();
            if (firstTonic != null && secondTonic != null)
            {
                RuiClick(RuiFind(root, $"Item_{firstTonic.id}"));
                yield return WaitUnscaled(0.3f);
                root = GameObject.Find("InventoryScreen");
                int secondBefore = inventory.CountItem(secondTonic.id);
                RuiClick(RuiFind(root, $"Item_{secondTonic.id}"));
                yield return WaitUnscaled(0.3f);
                root = GameObject.Find("InventoryScreen");
                RuiCheck("비약 한 전투 1개", $"예약 {firstTonic.name} · {firstTonic.name} 1개 남음 · 두 번째 거절",
                    $"예약 {game.PendingTonicItemId} · 남음 {inventory.CountItem(firstTonic.id)} · 두 번째 {inventory.CountItem(secondTonic.id)}/{secondBefore} · 안내 {RuiText(root?.transform, "Notice")}",
                    game.PendingTonicItemId == firstTonic.id && inventory.CountItem(firstTonic.id) == 1 &&
                    inventory.CountItem(secondTonic.id) == secondBefore);
                yield return CaptureStill("rui_supply_tonic");
            }
            else RuiCheck("비약 한 전투 1개", "비약 2종", "데이터 부족", false);

            // 4) 준비 밖 — 써지지 않는다.
            ItemData seal = kinds.Where(item => item.IsSeal).OrderBy(item => item.sealMaxTier).FirstOrDefault();
            if (seal != null)
            {
                int sealBefore = inventory.CountItem(seal.id);
                game.gameState = BaseEnums.GameState.RoundInProgress;
                RuiClick(RuiFind(root, $"Item_{seal.id}"));
                game.gameState = BaseEnums.GameState.Preparation;
                yield return WaitUnscaled(0.3f);
                root = GameObject.Find("InventoryScreen");
                RuiCheck("준비 밖 사용 차단", "선택 판 안 열림 · 인장 그대로",
                    $"후보 판 {RuiTargetNames(root).Count} · 인장 {inventory.CountItem(seal.id)}/{sealBefore} · 안내 {RuiText(root?.transform, "Notice")}",
                    RuiTargetNames(root).Count == 0 && inventory.CountItem(seal.id) == sealBefore);
            }

            // 5) 청동 인장 — 세이의 502를 같은 칸에서 602로.
            if (seal != null && sei.RewardCodeIds.Contains(502))
            {
                int slot = sei.RewardCodeIds.ToList().IndexOf(502);
                int target = RewardCodeFlow.SealTarget(502, seal.sealMaxTier);
                int sealBefore = inventory.CountItem(seal.id);
                root = GameObject.Find("InventoryScreen");
                RuiClick(RuiFind(root, $"Item_{seal.id}"));
                yield return WaitUnscaled(0.3f);
                RuiClick(RuiTargetCard(root, sei));
                yield return WaitUnscaled(0.3f);
                yield return CaptureStill("rui_supply_seal_pick");
                RuiClick(RuiOption(root, RuiName(502)));
                yield return WaitUnscaled(0.3f);
                RuiCheck("승급의 인장", $"{slot + 1}번 칸 502 → {target} · 인장 -1",
                    $"{RuiCodes(sei)} · 인장 {inventory.CountItem(seal.id)}/{sealBefore}",
                    target > 0 && sei.RewardCodeIds.ToList().IndexOf(target) == slot && !sei.RewardCodeIds.Contains(502) &&
                    inventory.CountItem(seal.id) == sealBefore - 1);
            }
            else RuiCheck("승급의 인장", "청동 인장 · 세이 502", $"인장 {seal?.id} · 세이 {RuiCodes(sei)}", false);

            // 6) 전수의 두루마리 — 세이의 503을 수리야의 빈 칸으로.
            ItemData scroll = kinds.FirstOrDefault(item => item.transferScroll);
            if (scroll != null && sei.RewardCodeIds.Contains(503))
            {
                int scrollBefore = inventory.CountItem(scroll.id);
                root = GameObject.Find("InventoryScreen");
                RuiClick(RuiFind(root, $"Item_{scroll.id}"));
                yield return WaitUnscaled(0.3f);
                RuiClick(RuiTargetCard(root, sei));
                yield return WaitUnscaled(0.3f);
                RuiClick(RuiOption(root, RuiName(503)));
                yield return WaitUnscaled(0.3f);
                yield return CaptureStill("rui_supply_transfer_receiver");
                RuiClick(RuiTargetCard(root, surya));
                yield return WaitUnscaled(0.3f);
                RuiCheck("전수의 두루마리", "세이 503 → 수리야 · 두루마리 -1",
                    $"세이 {RuiCodes(sei)} · 수리야 {RuiCodes(surya)} · 두루마리 {inventory.CountItem(scroll.id)}/{scrollBefore}",
                    !sei.RewardCodeIds.Contains(503) && surya.RewardCodeIds.Contains(503) &&
                    inventory.CountItem(scroll.id) == scrollBefore - 1);
            }
            else RuiCheck("전수의 두루마리", "두루마리 · 세이 503", $"두루마리 {scroll?.id} · 세이 {RuiCodes(sei)}", false);

            yield return CaptureStill("rui_supply_after_tools");
            ui.HideInventoryPanel();
            game.RestorePendingTonic(0);
            yield return WaitUnscaled(0.2f);
        }

        // ── 보상 화면의 상위 자동 대체 ──────────────────────────────

        /// <summary>
        /// 상점 코드에는 하위가 없어 상위 자동 대체는 같은 흐름(RewardCodeFlow)을 쓰는 보상 화면에서 본다.
        /// 확정하면 보상 정산이 다음 스테이지로 넘기므로 시나리오의 마지막에 두고, 끝에서 준비 장면을 다시 만든다.
        /// </summary>
        private IEnumerator RuiRewardUpperCheck(UIManager ui, Unit holder, int lower, int upper)
        {
            if (lower <= 0 || upper <= 0 || !holder.RewardCodeIds.Contains(lower))
            {
                RuiCheck("상위 자동 대체", "하위 · 상위 한 쌍", $"{lower} → {upper} · 보유 {RuiCodes(holder)}", false);
                yield break;
            }

            int slot = holder.RewardCodeIds.ToList().IndexOf(lower);
            int count = holder.RewardCodeIds.Count;
            ui.ShowRewardPanel(new List<RewardDef> { RewardManager.BuildCodeReward(RewardCodeCatalog.Find(upper)) });
            yield return WaitUnscaled(0.6f);
            GameObject root = GameObject.Find("RewardScreen");
            RuiClick(RuiFind(root, "Reward0"));
            yield return WaitUnscaled(0.3f);
            string preview = RuiText(RuiTargetCard(root, holder), "Preview") ?? "";
            RuiCheck("상위 대체 미리보기", $"{holder.UnitName}: ▲ {RuiName(lower)} 대체", preview,
                preview.Contains("대체") && preview.Contains(RuiName(lower)));
            yield return CaptureStill("rui_reward_upper_preview");
            RuiClick(RuiTargetCard(root, holder));
            yield return WaitUnscaled(0.6f);
            RuiCheck("상위 자동 대체", $"{slot + 1}번 칸 {lower} → {upper} · 칸 수 {count} 유지",
                RuiCodes(holder),
                holder.RewardCodeIds.ToList().IndexOf(upper) == slot && !holder.RewardCodeIds.Contains(lower) &&
                holder.RewardCodeIds.Count == count);
            yield return WaitForState(BaseEnums.GameState.Preparation, 5f);
            yield return WaitUnscaled(0.6f);
            yield return CaptureStill("rui_reward_upper_done");
        }

        // ── 공격 형태 → 보상 후보 ─────────────────────────────────────

        /// <summary>
        /// 아그니의 추가 공격은 #주문인데 고유 특성이 형태를 선언하지 않아 주문 증폭이 후보에서 빠졌다.
        /// 실제 팩토리로 만든 편성 두 개에서 <see cref="AttackForms.Of"/>와 <see cref="RewardManager.BuildCodePool"/>을 본다.
        /// </summary>
        private IEnumerator ScenarioRewardUiForms(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            List<int> FormCodes(int form) => RewardCodeCatalog.All()
                .Where(entry => entry.requiresAttackForm == form && Codes.Passive.Reward.RewardCodeFactory.IsImplemented(entry.id))
                .Select(entry => entry.id).ToList();
            List<int> spellCodes = FormCodes(DamageTag.Spell);
            List<int> shotCodes = FormCodes(DamageTag.Shot);
            List<int> strikeCodes = FormCodes(DamageTag.Strike);
            RuiCheck("형태 코드 표", "주문 613 · 사격 611 · 타격 612",
                $"주문 {string.Join(",", spellCodes)} · 사격 {string.Join(",", shotCodes)} · 타격 {string.Join(",", strikeCodes)}",
                spellCodes.Contains(613) && shotCodes.Contains(611) && strikeCodes.Contains(612));

            yield return BuildFreshRun("duo_agni_surya");
            Unit agni = AliveAllies().FirstOrDefault(unit => unit.ID == AgniId);
            List<int> agniForms = Combat.AttackForms.Of(agni).Distinct().ToList();
            RuiCheck("아그니 공격 형태", "타격 · 주문", string.Join(",", agniForms.Select(DamageTag.AttackFormName)),
                agni != null && agniForms.Contains(DamageTag.Strike) && agniForms.Contains(DamageTag.Spell));
            List<int> pool = GameManager.Instance.rewardManager.BuildCodePool().Select(reward => reward.codeId).ToList();
            RuiCheck("아그니 + 수리야 코드 후보", "611 · 612 · 613 포함",
                $"611 {pool.Contains(611)} · 612 {pool.Contains(612)} · 613 {pool.Contains(613)} · 후보 {pool.Count}",
                pool.Contains(611) && pool.Contains(612) && pool.Contains(613));
            yield return CaptureStill("rui_forms_agni_surya");

            yield return BuildFreshRun("duo_shi_sabah");
            List<int> partyForms = Combat.AttackForms.PartyUnits().SelectMany(Combat.AttackForms.Of).Distinct().ToList();
            pool = GameManager.Instance.rewardManager.BuildCodePool().Select(reward => reward.codeId).ToList();
            RuiCheck("시 + 사바흐 코드 후보", "주문 · 사격 · 타격 없음 · 613 없음",
                $"형태 {string.Join(",", partyForms.Select(DamageTag.AttackFormName))} · 613 {pool.Contains(613)} · 611 {pool.Contains(611)} · 612 {pool.Contains(612)}",
                !partyForms.Contains(DamageTag.Spell) && !partyForms.Contains(DamageTag.Shot) && !partyForms.Contains(DamageTag.Strike) &&
                !pool.Contains(613) && !pool.Contains(611) && !pool.Contains(612));
            yield return CaptureStill("rui_forms_shi_sabah");
            RuiFinish();
        }
    }
}
#endif
