#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Codes.Base;
using Core;
using Managers.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static GameObject GuideRoot() => GameObject.Find("WikiCanvas");
        private static Vector2 GuideLinkPoint(TextMeshProUGUI text, string id)
        {
            text.ForceMeshUpdate();
            foreach (var link in text.textInfo.linkInfo.Take(text.textInfo.linkCount))
            {
                if (link.GetLinkID() != id) continue;
                var c = text.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
                return RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint((c.bottomLeft + c.topRight) * .5f));
            }
            return new Vector2(-100, -100);
        }
        private IEnumerator ScenarioGuideRework(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config, 6, false, true, true);
            var ui = GameManager.Instance.uiManager; var wiki = ui.GuideForVerification;
            float previousScale = Time.timeScale;
            ui.ShowGuide(); yield return WaitUnscaled(.5f);
            RuiCheck("가이드 첫 화면", "6장 · home", wiki.SelectedKey, wiki.SelectedKey == "guide:home" && RuiAll(GuideRoot(), n => n.StartsWith("Chapter_")).Count == 6);
            RuiCheck("자료실 시간 격리", "0", Time.timeScale.ToString(), Time.timeScale == 0);
            yield return CaptureStill("guide_home");
            RuiClick(RuiFind(GuideRoot(), "Chapter_build")); yield return WaitUnscaled(.15f);
            RuiCheck("Rework 빌드 안내", "활성 7 · 보관 · 드래그 · 고유 스킬 별도", wiki.ArticleText,
                wiki.SelectedKey == "guide:build" && wiki.ArticleText.Contains("7개") && wiki.ArticleText.Contains("보관") && wiki.ArticleText.Contains("드래그"));
            yield return CaptureStill("guide_build");
            ui.ShowGuide("concentration"); yield return WaitUnscaled(.1f);
            RuiCheck("집중 실제 규칙", "시전자 턴 · 행동불능 · 피격 아군 무관", wiki.ArticleText,
                wiki.ArticleText.Contains("시전자의 턴") && wiki.ArticleText.Contains("행동불능") && wiki.ArticleText.Contains("아군의 피격"));
            yield return CaptureStill("guide_concentration");
            TextMeshProUGUI body = GuideRoot().GetComponentsInChildren<TextMeshProUGUI>().First(t => t.name == "Text" && t.text.Contains("시전자의 턴"));
            var links = body.GetComponent<GameTermLinks>();
            Vector2 point = GuideLinkPoint(body, "term:freeze");
            RuiCheck("용어 실제 문자 히트", "term:freeze", links.LinkAt(point), links.LinkAt(point) == "term:freeze");
            Mouse prior = Mouse.current; Mouse mouse = InputSystem.AddDevice<Mouse>("GuideVerification");
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return WaitUnscaled(.85f);
                RuiCheck("실제 호버 사전", "빙결 1층", GuideTermTooltip.CurrentId, GuideTermTooltip.Depth == 1 && GuideTermTooltip.CurrentId == "freeze");
                yield return CaptureStill("guide_term_pinned");
                var tipRoot = GameObject.Find("GuideTooltipCanvas");
                var related = tipRoot?.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "Related");
                if (related != null)
                {
                    Vector2 next = GuideLinkPoint(related, "term:attachment");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = next });
                    yield return WaitUnscaled(.9f);
                    RuiCheck("부모를 남기는 중첩", "빙결 → 원소 부착 2층", GuideTermTooltip.Depth + "/" + GuideTermTooltip.CurrentId,
                        GuideTermTooltip.Depth == 2 && GuideTermTooltip.CurrentId == "attachment");
                    yield return CaptureStill("guide_term_nested");
                    ui.HandleEscape();
                    RuiCheck("ESC 사전 한 층 닫기", "빙결 1층 · 자료실 유지", GuideTermTooltip.Depth.ToString(), GuideTermTooltip.Depth == 1 && wiki.IsVisible);
                    GuideTermTooltip.CloseAll();
                    GuideTermTooltip.Show(links, "burn", new Vector2(Screen.width * .5f, Screen.height * .5f), true);
                    yield return null;
                    var read = GameObject.Find("Term_burn")?.transform.Find("ReadGuide");
                    RuiClick(read); yield return WaitUnscaled(.1f);
                    RuiCheck("사전에서 상세 이동", "화상 본문 · 사전 정리", wiki.SelectedKey, wiki.SelectedKey == "term:burn" && GuideTermTooltip.Depth == 0);
                    RuiClick(RuiFind(GuideRoot(), "GuideBack")); yield return WaitUnscaled(.1f);
                    RuiCheck("상세 돌아가기", "집중 복원", wiki.SelectedKey, wiki.SelectedKey == "term:concentration");
                }
                else RuiCheck("중첩 UI", "Related 있음", "없음", false);
            }
            finally { InputSystem.RemoveDevice(mouse); if (prior != null && prior.added) prior.MakeCurrent(); GuideTermTooltip.CloseAll(); }
            var search = GuideRoot().GetComponentsInChildren<TMP_InputField>().First();
            search.text = "정신력"; yield return WaitUnscaled(.1f);
            RuiCheck("전 분류 검색 · 신규 보상 코드", "정신력 검색 결과", wiki.SearchResultCount.ToString(), wiki.SearchResultCount > 0 && RuiFind(GuideRoot(), "Row_정신력") != null);
            RuiClick(RuiFind(GuideRoot(), "Row_정신력")); yield return WaitUnscaled(.1f);
            RuiCheck("보상 코드 상세", "Passive 702 · 활성7", wiki.SelectedKey, wiki.SelectedKey == "code:Passive:702" && wiki.ArticleText.Contains("활성 7칸"));
            yield return CaptureStill("guide_reward_search");
            RuiClick(RuiFind(GuideRoot(), "GuideBack")); yield return WaitUnscaled(.1f);
            RuiCheck("전체 검색 전 위치 복원", "집중 · 검색어 없음", wiki.SelectedKey + "/" + search.text,
                wiki.SelectedKey == "term:concentration" && search.text.Length == 0);
            ui.ShowGuide("unit:62"); yield return WaitUnscaled(.15f);
            RuiCheck("특수행동도 캐릭터 가이드에 포함", "로카팔라 · 특수행동", wiki.ArticleText, wiki.ArticleText.Contains("특수행동") && wiki.ArticleText.Contains("로카팔라"));
            yield return CaptureStill("guide_agni");
            wiki.Hide(); RuiCheck("직접 진입 닫기 복원", $"시간 {previousScale}", Time.timeScale.ToString(), Mathf.Approximately(Time.timeScale, previousScale));
            ui.HandleEscape(); ui.ShowGuide("shock"); yield return WaitUnscaled(.1f); wiki.Hide();
            RuiCheck("메뉴 일시정지 보존", "시간0", Time.timeScale.ToString(), Time.timeScale == 0);
            ui.HandleEscape(); RuiCheck("메뉴 닫기 시간 복원", $"시간 {previousScale}", Time.timeScale.ToString(), Mathf.Approximately(Time.timeScale, previousScale));
            RuiCheck("용어 관련 항목 완결성", "전부 유효", GameGuideCatalog.Terms.Count.ToString(), GameGuideCatalog.Terms.All(a => a.Related.All(k => GameGuideCatalog.Find(k) != null)));
            RuiCheck("고유 메커니즘 원본 연결", "9개", GameGuideCatalog.Terms.Count(a => a.Chapter == "캐릭터 메커니즘").ToString(), GameGuideCatalog.Terms.Count(a => a.Chapter == "캐릭터 메커니즘") == 9);
            RuiCheck("반복 링크화 안정성", "중첩 link 없음", "화상 · 집중", GameGuideCatalog.LinkTerms(GameGuideCatalog.LinkTerms("화상 · 집중")) == GameGuideCatalog.LinkTerms("화상 · 집중"));
            RuiCheck("동음 용어 오연결 방지", "활성 코드·성장치·집중 포격에 링크 없음", "빌드/증폭", !GameGuideCatalog.LinkTerms("활성 코드 성장치 집중 포격").Contains("<link"));
            ui.ShowCodex(RewardCodeFlow.Party().First(u => u.ID == AgniId));
            yield return WaitUnscaled(.2f); RuiClick(RuiFind(KbCodexRoot(), "Tab코드")); yield return WaitUnscaled(.15f);
            var skillText = KbCodexRoot().GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "Summary" && t.text.Contains("term:additional"));
            RuiCheck("실제 파티 스킬 설명 용어", "추가행동 링크", skillText?.text, skillText != null);
            if (skillText != null)
            {
                var previous = Mouse.current; var hoverMouse = InputSystem.AddDevice<Mouse>("GuideSkillVerification");
                try
                {
                    InputSystem.QueueStateEvent(hoverMouse, new MouseState { position = GuideLinkPoint(skillText, "term:additional") });
                    yield return WaitUnscaled(.8f);
                    RuiCheck("파티 화면 실제 용어 호버", "추가행동 사전", GuideTermTooltip.CurrentId, GuideTermTooltip.CurrentId == "additional");
                    yield return CaptureStill("guide_skill_tooltip");
                }
                finally { InputSystem.RemoveDevice(hoverMouse); if (previous != null && previous.added) previous.MakeCurrent(); }
            }
            GuideTermTooltip.CloseAll(); ui.CodexForVerification.Hide();
            RuiFinish();
        }
    }
}
#endif
