#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseClasses;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using Entities.Status;
using Managers.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 쿠베라 현행 재보 검증과 기존 로카팔라 코드 UI 검사.
    ///   `kubera_followup_core`는 최신 treasury_core(재보 편성)를 실행한다. 과거 소환 후원 검사는 폐기했다.
    ///   `kubera_followup_lokapala_ui` (party5_lokapala) — 인드라 · 바유 · 쿠베라 · 찬드라의 특수행동 항목, 특수행동이 없는 시.
    ///
    /// 장면 준비 · 실제 행동 실행 · 피해 기록은 야마 · 바루나 하네스의 <see cref="YvPrepareScene"/> · <see cref="YvRecorder"/>를 그대로 쓴다.
    /// 결과는 `[쿠베라후속검증]` 줄과 mechanics에 남는다. 기대값은 기획 수치(Detail_08 §11)이며 코드 상수를 읽어 오지 않는다.
    /// </summary>
    public sealed partial class VerifyHarnessRunner
    {
        private const string KbPatronageKey = "kubera_patronage";
        private const string KbTreasuryKey = "kubera_treasury";
        private const string KbGalateaKey = "pygmalion_galatea_aura";
        private const int KbRaw = 20000;
        // 피해 2만 안팎의 정수 반올림 오차는 1e-4 수준이다. 곱연산 오류(1.38)와 합산(1.35)의 차이 0.03보다 훨씬 좁다.
        private const float KbTolerance = 0.003f;

        private readonly List<string> _kbFailures = new();

        private void KbCheck(string check, string expected, string actual, bool passed)
        {
            Debug.Log($"[쿠베라후속검증] {(passed ? "통과" : "실패")} — {check}: 기대 {expected}, 실제 {actual}");
            RecordMechanic(check, expected, actual, passed);
            if (!passed) _kbFailures.Add(check);
        }

        private void KbFinish()
        {
            if (_kbFailures.Count > 0) FailCurrent($"{_kbFailures.Count}건 실패: " + string.Join(" / ", _kbFailures));
            _kbFailures.Clear();
        }

        /// <summary>공격자 하나의 측정 방식. 실제 분신 일격과 같은 태그를 쓰는 "소환수 피해"와 일반 피해를 나눈다.</summary>
        private readonly struct KbProbeKind
        {
            public readonly string Label;
            public readonly Unit Attacker;
            public readonly bool SummonTag;
            public readonly bool IsSummonDamage;

            public KbProbeKind(string label, Unit attacker, bool summonTag, bool isSummonDamage)
            {
                Label = label;
                Attacker = attacker;
                SummonTag = summonTag;
                IsSummonDamage = isSummonDamage;
            }
        }

        private static List<int> KbTags(bool summonTag)
        {
            // 분신 일격(UchiCloneStrike.StrikeAndVanish)과 같은 태그. 일반 피해는 SummonAttack만 뺀다.
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack, DamageTag.Strike };
            if (summonTag) tags.Add(DamageTag.SummonAttack);
            return tags;
        }

        /// <summary>치명 없는 원시 피해를 공용 경로(TakeDamage)로 넣고 실제로 깎인 양을 돌려준다. 매번 체력을 채워 같은 조건으로 잰다.</summary>
        private static int KbProbe(Unit attacker, Unit target, bool summonTag, int raw = KbRaw)
        {
            target.RestoreHpOutsideCombat(target.HpMax);
            var context = new DamageContext(attacker, raw, BaseEnums.CodeType.Normal, KbTags(summonTag), false);
            target.TakeDamage(context);
            int dealt = context.ResolvedDamage;
            target.RestoreHpOutsideCombat(target.HpMax);
            return dealt;
        }

        private Dictionary<string, int> KbMeasure(IEnumerable<KbProbeKind> kinds, Unit target)
        {
            // 구간 사이에 적의 부착 원소 · 해로운 상태가 남아 받는 피해가 달라지지 않게 매번 깨끗이 되돌린다.
            YvResetEnemy(target);
            return kinds.ToDictionary(kind => kind.Label, kind => KbProbe(kind.Attacker, target, kind.SummonTag));
        }

        private void KbCompare(string phase, List<KbProbeKind> kinds, Dictionary<string, int> baseline,
            Dictionary<string, int> current, float summonExpected, float plainExpected, string note = null)
        {
            foreach (KbProbeKind kind in kinds)
            {
                float expected = kind.IsSummonDamage ? summonExpected : plainExpected;
                float ratio = baseline[kind.Label] > 0 ? current[kind.Label] / (float)baseline[kind.Label] : 0f;
                KbCheck($"{phase} — {kind.Label}",
                    $"×{expected:0.000}{(note != null && kind.IsSummonDamage ? $" ({note})" : "")}",
                    $"×{ratio:0.0000} ({baseline[kind.Label]} → {current[kind.Label]})",
                    Mathf.Abs(ratio - expected) <= KbTolerance);
            }
        }

        private static int KbStatusCount(Unit unit, string key)
            => unit?.GetStatuses().Count(status => status.Key == key) ?? 0;

        private static UnitStatus KbStatus(Unit unit, string key)
            => unit?.GetStatuses().FirstOrDefault(status => status.Key == key);

        private IEnumerator KbCastTreasury(Unit kubera)
        {
            kubera.FillUltimateResource();
            kubera.CastUltimateCode();
            yield return WaitForCasting(kubera, 8f);
            yield return WaitUnscaled(0.3f);
        }

        // ── 시나리오 1: 합산 + 실제 UI ─────────────────────────────────

        private IEnumerator ScenarioKuberaReworkCore(VerifyScenarioRequest config)
        {
            // 2026-10-10: 옛 소환 후원/보고 개방 검사는 더 이상 현행 사양이 아니다.
            config.lineup = "party5_treasury";
            yield return ScenarioTreasuryCore(config);
        }

        private IEnumerator ScenarioKuberaFollowupLokapalaUi(VerifyScenarioRequest config)
        {
            _kbFailures.Clear();
            yield return PrepareStage(config, CombatStage(config, 6, 6), false, true, true);
            yield return KbCodexUi(false);
            KbFinish();
        }

        // ── TAB 캐릭터 창 ────────────────────────────────────────────

        private static GameObject KbCodexRoot() => GameObject.Find("CodexScreen");

        /// <summary>파티 창 왼쪽 레일의 그 유닛 줄을 실제 클릭 경로(UIPointerEvents)로 누른다. 이미 골라져 있어도 true.</summary>
        private static bool KbSelectMember(GameObject root, Unit unit)
        {
            if (root == null || unit == null) return false;
            Transform row = root.GetComponentsInChildren<Transform>(false).FirstOrDefault(item => item.name == $"Member_{unit.UnitName}");
            UIPointerEvents pointer = row != null ? row.GetComponent<UIPointerEvents>() : null;
            if (pointer == null) return false;
            pointer.OnPointerClick(null);
            return true;
        }

        private static Transform KbColumn(GameObject root, Unit unit)
            => root == null || unit == null ? null
                : root.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(rect => rect.name == $"Col_{unit.UnitName}");

        /// <summary>칸 안 코드 항목의 (이름, 종류). 항목 머리("Code")의 두 글자만 읽는다.</summary>
        private static List<(string Name, string Kind)> KbEntries(Transform column)
        {
            var entries = new List<(string, string)>();
            if (column == null) return entries;
            foreach (RectTransform head in column.GetComponentsInChildren<RectTransform>(false).Where(rect => rect.name == "Code"))
            {
                string name = head.Find("Name")?.GetComponent<TextMeshProUGUI>()?.text ?? "";
                string kind = head.Find("Kind")?.GetComponent<TextMeshProUGUI>()?.text ?? "";
                entries.Add((name, kind));
            }
            return entries;
        }

        private static string KbLiveText(Transform column)
        {
            var label = column?.GetComponentsInChildren<TextMeshProUGUI>(false).FirstOrDefault(text => text.name == "LiveNote");
            if (label == null) return null;
            label.ForceMeshUpdate(); return label.GetParsedText();
        }

        private static string KbTooltipBody()
        {
            GameObject canvas = GameObject.Find("TooltipCanvas");
            Transform body = canvas != null ? canvas.transform.Find("Tooltip/Body") : null;
            if (body == null || !body.gameObject.activeInHierarchy) return null;
            var label = body.GetComponent<TextMeshProUGUI>();
            label.ForceMeshUpdate(); return label.GetParsedText();
        }

        /// <summary>기획표(Detail_08 §11.3)에서 손으로 옮긴 기대 문구. 코드의 식을 부르지 않는다.</summary>
        private static string KbExpectedLive(float multiplier, int gold, int stage)
            => $"현재 금력 배율 ×{multiplier:0.00}\n보유 골드 {gold:N0} · 스테이지 {stage}";

        private static void KbSetStageForCheck(int stage)
        {
            // RoundManager.Stage는 외부에서 쓸 수 없다. 스테이지 0(런 시작 전) 대체만 검증에서 잠깐 흉내 내고 곧바로 되돌린다.
            RoundManager round = GameManager.Instance?.RoundManager;
            round?.GetType().GetField("<Stage>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(round, stage);
        }

        private IEnumerator KbFrames(int count = 3)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private IEnumerator KbCodexUi(bool summonParty)
        {
            UIManager ui = GameManager.Instance.uiManager;
            InventoryManager inventory = GameManager.Instance.inventoryManager;
            Managers.UI.Core.UITooltip.HideAny();
            if (KbCodexRoot() == null) ui.ToggleCodex();
            yield return WaitUnscaled(0.6f);
            GameObject root = KbCodexRoot();
            if (root == null) { KbCheck("TAB 캐릭터 창 열기", "열림", "열리지 않음", false); yield break; }

            // 탭 전환은 실제 버튼을 누르는 경로로 한다.
            Button codeTab = root.GetComponentsInChildren<Button>(false).FirstOrDefault(button => button.name == "Tab코드");
            if (codeTab == null) { KbCheck("코드 탭 버튼", "있음", "없음", false); yield break; }
            codeTab.onClick.Invoke();
            yield return WaitUnscaled(0.4f);
            root = KbCodexRoot();

            // ── 특수행동 항목: 가진 유닛은 실제 코드 이름 + "특수행동", 없는 유닛은 항목이 없다.
            // 2026-10-06 파티 창은 한 번에 한 명만 그린다. 왼쪽 레일의 실제 버튼으로 유닛마다 고른 뒤 그 유닛의 본문을 읽는다.
            foreach (Unit unit in AliveAllies())
            {
                bool picked = KbSelectMember(root, unit);
                yield return WaitUnscaled(0.25f);
                root = KbCodexRoot();
                Transform column = KbColumn(root, unit);
                KbCheck($"레일에서 고르기 — {unit.UnitName}", "레일 버튼 · 본문 열림",
                    $"버튼 {picked} · 본문 {(column != null ? "있음" : "없음")}", picked && column != null);
                List<(string Name, string Kind)> entries = KbEntries(column);
                KbCheck($"코드 탭 기본 코드 — {unit.UnitName}", "고유 특성 · 일반행동 · 초월기",
                    string.Join(", ", entries.Select(entry => entry.Kind).Distinct()),
                    entries.Any(entry => entry.Kind == "고유 특성") && entries.Any(entry => entry.Name == "일반행동" || entry.Kind == "일반행동") &&
                    entries.Any(entry => entry.Kind == "초월기"));
                List<string> specials = entries.Where(entry => entry.Kind == "특수행동").Select(entry => entry.Name).ToList();
                string expected = unit.ActiveSpecialCode != null ? unit.ActiveSpecialCode.CodeName : "없음";
                KbCheck($"코드 탭 특수행동 — {unit.UnitName}", expected,
                    specials.Count == 0 ? "없음" : string.Join(", ", specials),
                    unit.ActiveSpecialCode != null
                        ? specials.Count == 1 && specials[0] == unit.ActiveSpecialCode.CodeName
                        : specials.Count == 0);
            }
            if (!summonParty)
            {
                Unit indra = AliveAllies().FirstOrDefault(unit => unit.ID == IndraId);
                Unit shi = AliveAllies().FirstOrDefault(unit => unit.ID == ShiId);
                KbCheck("인드라 바즈라 · 시 특수행동 없음(데이터)", "인드라 IndraVajra · 시 없음",
                    $"인드라 {indra?.ActiveSpecialCode?.GetType().Name ?? "없음"} · 시 {shi?.ActiveSpecialCode?.GetType().Name ?? "없음"}",
                    indra?.ActiveSpecialCode is Codes.Special.IndraVajra && shi != null && shi.ActiveSpecialCode == null);
            }

            Unit kubera = AliveAllies().FirstOrDefault(unit => unit.ID == KuberaId);
            KbCheck("쿠베라 고르기", "레일 버튼", KbSelectMember(root, kubera) ? "누름" : "버튼 없음", kubera != null);
            yield return WaitUnscaled(0.25f);
            root = KbCodexRoot();
            Transform kuberaColumn = KbColumn(root, kubera);
            KbCheck("쿠베라 본문", "Col_쿠베라", kuberaColumn != null ? kuberaColumn.name : "없음", kuberaColumn != null);
            int stage = GameManager.Instance.RoundManager.Stage;
            KbCheck("검증 스테이지", "6", stage.ToString(), stage == 6);

            // 금력 배율 기대값: 6스테이지 = 금력 1당 600골드. 0 → ×1.00, 3,000(금력 5) → ×1.75, 12,000(20) → ×4.00, 24,000(40) → ×4.00 상한.
            IEnumerator Expect(string label, int gold, float multiplier, int shownStage, string capture = null, bool restore = true)
            {
                if (restore) inventory.RestoreGold(gold);
                yield return KbFrames();
                float actual = Codes.Special.KuberaGoldRush.GoldMultiplier();
                string expectedText = KbExpectedLive(multiplier, inventory.Gold, shownStage);
                string liveText = KbLiveText(kuberaColumn);
                KbCheck($"금력 배율 {label}", $"×{multiplier:0.00} · \"{expectedText.Replace('\n', '/')}\"",
                    $"×{actual:0.0000} · \"{liveText?.Replace('\n', '/')}\"",
                    Mathf.Abs(actual - multiplier) < 0.0005f && liveText == expectedText);
                if (capture != null) yield return CaptureStill(capture);
            }

            yield return CaptureStill("kubera_codex_codes");
            inventory.RestoreGold(-500);
            yield return Expect("음수 입력 → 0골드", 0, 1.00f, 6, restore: false);
            KbCheck("음수 골드 입력 정리", "보유 0", inventory.Gold.ToString(), inventory.Gold == 0);
            yield return Expect("0골드", 0, 1.00f, 6, "kubera_codex_x100");
            yield return Expect("3,000골드(금력 5)", 3000, 1.75f, 6, "kubera_codex_x175");
            yield return Expect("12,000골드(금력 20)", 12000, 4.00f, 6);
            yield return Expect("24,000골드(금력 40, 상한)", 24000, 4.00f, 6, "kubera_codex_x400_cap");

            // 상점 구매와 같은 소비 경로(TrySpendGold) — 열린 창의 값이 곧바로 내려간다.
            inventory.RestoreGold(12000);
            yield return KbFrames();
            bool spent = inventory.TrySpendGold(9000);
            KbCheck("상점 소비 경로 TrySpendGold", "성공 · 3,000 남음", $"{spent} · {inventory.Gold}", spent && inventory.Gold == 3000);
            yield return Expect("소비 뒤 하락 12,000 → 3,000", 3000, 1.75f, 6, "kubera_codex_after_spend", restore: false);

            // 스테이지 0 대체(런 시작 전) — 1로 본다. 300골드 → 금력 3 → ×1.45.
            KbSetStageForCheck(0);
            yield return Expect("스테이지 0 → 1로 대체", 300, 1.45f, 1);
            KbSetStageForCheck(6);

            // 열린 툴팁 갱신 — 같은 항목의 툴팁을 고정 좌표로 띄운 채 골드를 바꾼다.
            TooltipTrigger trigger = kuberaColumn?.GetComponentsInChildren<TooltipTrigger>(false)
                .FirstOrDefault(item => item.name == $"Hover_{kubera?.ActiveSpecialCode?.CodeName}");
            if (trigger == null) KbCheck("골드 러쉬 툴팁 영역", "있음", "없음", false);
            else
            {
                inventory.RestoreGold(0);
                yield return KbFrames();
                var corners = new Vector3[4];
                ((RectTransform)trigger.transform).GetWorldCorners(corners);
                trigger.ShowAt((corners[0] + corners[2]) * 0.5f);
                yield return KbFrames();
                string before = KbTooltipBody();
                KbCheck("툴팁 금력 배율 ×1.00", "본문에 ×1.00", before?.Contains("×1.00").ToString() ?? "툴팁 없음",
                    before != null && before.Contains("현재 금력 배율 ×1.00"));
                yield return CaptureStill("kubera_tooltip_x100", keepTooltip: true);

                inventory.RestoreGold(3000);
                yield return KbFrames();
                string after = KbTooltipBody();
                KbCheck("열린 툴팁 갱신 0 → 3,000골드", "같은 툴팁이 ×1.75로 바뀜",
                    $"표시 {UITooltip.IsShowing(trigger)} · ×1.75 {after?.Contains("현재 금력 배율 ×1.75")}",
                    UITooltip.IsShowing(trigger) && after != null && after.Contains("현재 금력 배율 ×1.75"));
                yield return CaptureStill("kubera_tooltip_x175", keepTooltip: true);
                UITooltip.HideAny();
            }

            // 실제 스테이지 변경 — 같은 골드라도 스테이지가 오르면 금력이 줄어든다. 6,000골드: 6스테이지 ×2.50 → 12스테이지 ×1.75.
            yield return Expect("6,000골드 · 6스테이지", 6000, 2.50f, 6);
            GameManager.Instance.DebugLoadStage(12);
            yield return WaitForState(BaseEnums.GameState.Preparation, 5f);
            yield return WaitUnscaled(1.05f);
            root = KbCodexRoot();
            bool stillOpen = root != null;
            if (!stillOpen)
            {
                // 스테이지 이동이 창을 닫았다면 다시 여는 경로로 확인한다(어느 쪽인지 기록한다).
                ui.ToggleCodex();
                yield return WaitUnscaled(0.6f);
                root = KbCodexRoot();
            }
            KbCheck("스테이지 이동 중 창 상태", "열림 유지 또는 다시 열기", stillOpen ? "열림 유지" : "다시 엶", root != null);
            kubera = AliveAllies().FirstOrDefault(unit => unit.ID == KuberaId) ?? kubera;
            kuberaColumn = KbColumn(root, kubera);
            yield return Expect("스테이지 6 → 12 (6,000골드)", 6000, 1.75f, 12, "kubera_codex_stage12", restore: false);

            // 다시 열기 — 닫힌 동안 바뀐 골드가 다시 열 때 반영된다(코드 탭 유지).
            ui.ToggleCodex();
            yield return WaitUnscaled(0.3f);
            inventory.RestoreGold(0);
            ui.ToggleCodex();
            yield return WaitUnscaled(0.6f);
            root = KbCodexRoot();
            kuberaColumn = KbColumn(root, kubera);
            yield return Expect("닫은 뒤 0골드로 바꾸고 다시 열기", 0, 1.00f, 12, "kubera_codex_reopen", restore: false);

            if (KbCodexRoot() != null) ui.ToggleCodex();
            yield return WaitUnscaled(0.3f);
        }
    }
}
#endif
