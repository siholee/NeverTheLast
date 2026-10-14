#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Combat;
using Entities;
using Managers.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioTreasurySpHud(global::Core.VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            _deferCaptureEncoding = true;
            var root = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == "PartyTreasury");
            var allies = AliveAllies().ToArray();
            var actor = allies.FirstOrDefault(u => u.HasUnitTag("Treasury"));
            PartyTreasury.BeginBattle(); yield return null;
            if (actor == null)
            {
                PartyTreasury.Charge(allies[0], 4); yield return null;
                RuiCheck("재보 캐릭터 없는 파티 HUD 숨김", "자원 있어도 숨김", root.gameObject.activeInHierarchy.ToString(), !root.gameObject.activeInHierarchy);
                yield return CaptureStill("treasury_absent");
            }
            else
            {
                var points = Enumerable.Range(0, PartyTreasury.Capacity).Select(i => root.Find("TreasuryPoint" + i).GetComponent<Image>()).ToArray();
                var reservations = Enumerable.Range(0, PartyTreasury.Capacity).Select(i => root.Find("TreasuryReserved" + i).GetComponent<Image>()).ToArray();
                var count = root.Find("TreasuryCount").GetComponent<TextMeshProUGUI>();
                RuiCheck("재보 파티 빈 자원 표시", "출전·0·12빈칸", count.text, root.gameObject.activeInHierarchy && count.text == "0" && points.Length == 12);
                yield return CaptureStill("treasury_empty");
                PartyTreasury.Charge(actor, 5);
                yield return CaptureWindowAtRealtimeInterval("treasury_charge", .50f, 1f / 30);
                RuiCheck("충전 HUD 실제 잔고", "5", count.text, count.text == "5");
                PartyTreasury.Spend(actor, 3);
                yield return CaptureWindowAtRealtimeInterval("treasury_spend", .50f, 1f / 30);
                RuiCheck("소비 HUD 실제 잔고", "2", count.text, count.text == "2");
                PartyTreasury.Charge(actor, 20); yield return null;
                RuiCheck("12 상한 HUD", "12", count.text, PartyTreasury.Total == 12 && count.text == "12");
                yield return CaptureStill("treasury_full");
                using (var magazine = PartyTreasury.Reserve(actor))
                {
                    yield return null;
                    RuiCheck("예약 HUD 분리", "가용0·예약12·12표시", count.text, count.text == "0" && reservations.All(image => image.enabled) && root.Find("TreasuryReservation").GetComponent<TextMeshProUGUI>().text == "사격 예약 12");
                    yield return CaptureStill("treasury_reserved");
                    magazine.SpendOne(); yield return null;
                    var pause = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "BattlePauseButton");
                    pause.onClick.Invoke(); yield return null;
                    var echo = root.Find("TreasuryEcho11").GetComponent<Image>();
                    Vector3 scale = echo.rectTransform.localScale; Color color = echo.color;
                    yield return WaitUnscaled(.25f);
                    RuiCheck("일시정지 재보 연출 정지", "변화0", Vector3.Distance(scale, echo.rectTransform.localScale).ToString(), BattleHud.IsPaused && Vector3.Distance(scale, echo.rectTransform.localScale) < .0001f && echo.color == color);
                    yield return CaptureStill("treasury_paused"); pause.onClick.Invoke();
                    yield return CaptureWindowAtRealtimeInterval("treasury_shot", .45f, 1f / 30);
                    RuiCheck("실제 발사 예약 감소", "예약11", PartyTreasury.Reserved.ToString(), reservations.Count(image => image.enabled) == 11 && PartyTreasury.Reserved == 11);
                }
                yield return null;
                RuiCheck("미발사 반환 HUD", "가용11·예약0", count.text, count.text == "11" && reservations.All(image => !image.enabled));
                yield return CaptureStill("treasury_refund");
                PartyTreasury.BeginBattle(); PartyTreasury.Charge(actor, 3); yield return null;
                using (var mixed = PartyTreasury.Reserve(actor))
                {
                    PartyTreasury.Charge(actor, 5); yield return WaitUnscaled(.40f);
                    yield return CaptureStill("treasury_mixed_ready");
                    PartyTreasury.Spend(actor, 2); yield return null;
                    var availableEcho = root.Find("TreasuryEcho3").GetComponent<Image>();
                    var wrongEcho = root.Find("TreasuryEcho6").GetComponent<Image>();
                    RuiCheck("혼합 잔고 가용 소비 잔상", "기존 가용3~4만·예약6없음", availableEcho.enabled.ToString(), availableEcho.enabled && root.Find("TreasuryEcho4").GetComponent<Image>().enabled && !wrongEcho.enabled && availableEcho.color.b > availableEcho.color.r);
                    yield return CaptureStill("treasury_mixed_spend");
                    yield return WaitUnscaled(.40f);
                    mixed.SpendOne(); yield return null;
                    var reservedEcho = root.Find("TreasuryEcho5").GetComponent<Image>();
                    RuiCheck("혼합 잔고 예약 발사 잔상 색", "기존 예약5·금색", reservedEcho.enabled.ToString(), reservedEcho.enabled && reservedEcho.color.r > reservedEcho.color.b);
                    yield return CaptureStill("treasury_mixed_shot");
                }
                foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    text.ForceMeshUpdate(); var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    RuiCheck(text.name + " 재보 판 여백", "내부·잘림 없음", text.text, !text.isTextTruncated && corners.All(p => root.rect.Contains((Vector2)root.InverseTransformPoint(p))));
                }
                var treasuryAllies = allies.Where(u => u.HasUnitTag("Treasury")).ToArray();
                foreach (var unit in treasuryAllies) unit.isActive = false;
                yield return null;
                RuiCheck("출전 재보 캐릭터 없으면 HUD 숨김", "숨김", root.gameObject.activeInHierarchy.ToString(), !root.gameObject.activeInHierarchy);
                foreach (var unit in treasuryAllies) unit.isActive = true;
            }
            PartyTreasury.EndBattle(); yield return null;
            RuiCheck("전투 종료 재보 HUD 정리", "숨김", root.gameObject.activeInHierarchy.ToString(), !root.gameObject.activeInHierarchy);
            FlushDeferredCaptures(); GameManager.Instance.enabled = enabled; RuiFinish();
        }
    }
}
#endif
