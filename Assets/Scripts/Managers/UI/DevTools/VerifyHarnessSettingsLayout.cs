#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Core;
using TMPro;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        // SDF 렌더링 쿼드는 흐림/외곽선용 투명 패딩을 포함한다. 그 쿼드를 글자 폭으로
        // 검사하면 다른 UI가 폰트 재질을 준비한 뒤 정상적인 좌/우 정렬까지 실패한다.
        // 동일 문자/배율/정렬 원점의 실제 글리프 메트릭으로 행의 수용 폭을 검사한다.
        private static bool SettingsGlyphFitsRow(TextMeshPro label, TMP_CharacterInfo character)
        {
            var metrics = character.textElement.glyph.metrics;
            float left = character.origin + metrics.horizontalBearingX * character.scale;
            float right = left + metrics.width * character.scale;
            Rect row = label.rectTransform.rect;
            return left >= row.xMin - .01f && right <= row.xMax + .01f;
        }

        private IEnumerator ScenarioSettingsLayout(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            yield return PrepareStage(config,6,false,true,true);
            var settings=SettingsManager.Instance;
            int textBefore=settings.TextScaleIndex,hudBefore=settings.HudScaleIndex;
            try
            {
                for(int hud=0;hud<3;hud++) for(int text=0;text<3;text++)
                {
                    settings.HudScaleIndex=hud;settings.TextScaleIndex=text;
                    yield return null;yield return null;yield return WaitUnscaled(.3f);
                    Canvas.ForceUpdateCanvases();
                    var cards=Object.FindObjectsByType<Entities.View.UnitCardView>(FindObjectsSortMode.None).Where(c=>c.gameObject.activeInHierarchy).ToArray();
                    var labels=cards.SelectMany(c=>c.GetComponentsInChildren<TextMeshPro>()).Where(t=>(t.name=="Name" || t.name=="HpValue" || t.name=="ResourceValue") && t.enabled && !string.IsNullOrEmpty(t.text)).ToArray();
                    foreach(var label in labels)label.ForceMeshUpdate();
                    bool visible=labels.Length>0 && labels.All(t=>!t.isTextTruncated && t.textInfo.characterInfo.Take(t.textInfo.characterCount).Any(c=>c.isVisible));
                    RuiCheck($"크기 설정 HUD{hud} 글자{text} 카드 글자", "양 진영 이름·체력·자원 렌더링/잘림 없음",string.Join(" / ",labels.Where(t=>t.isTextTruncated).Select(t=>t.text)),visible);
                    bool inside=labels.All(t=>t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.isVisible).All(c=>SettingsGlyphFitsRow(t,c)));
                    RuiCheck($"크기 설정 HUD{hud} 글자{text} 실제 글자 폭", "실제 글리프가 자기 행 안(SDF 투명 여백 제외)",string.Join(" / ",labels.Where(t=>t.textInfo.characterInfo.Take(t.textInfo.characterCount).Where(c=>c.isVisible).Any(c=>!SettingsGlyphFitsRow(t,c))).Select(t=>t.name+":"+t.text)),inside);
                    var dock=GameObject.Find("PreparationPanel");
                    Camera camera=Camera.main;Rect dockRect=RuiRect(dock.transform);
                    bool clear=cards.All(c=>{var back=c.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r.name=="Portrait");return back==null || camera.WorldToScreenPoint(back.bounds.min).y>dockRect.yMax+4f;});
                    RuiCheck($"크기 설정 HUD{hud} 글자{text} 도크 분리", "전장 카드와 명령 리본 비중첩",dockRect.ToString(),clear);
                    yield return CaptureStill($"settings_hud{hud}_text{text}");
                }
            }
            finally { settings.HudScaleIndex=hudBefore;settings.TextScaleIndex=textBefore; }
            // SaveSettings는 호출하지 않는다. 실제 사용자 설정과 저장을 보존한다.
            RuiFinish();
        }
    }
}
#endif
