#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Effects;
using Effects.Buffs;
using Entities;
using Entities.Status;
using TMPro;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private static string[] FeedbackNames() => Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
            .Where(t=>t.gameObject.name=="StatusName" && t.GetComponentsInParent<CanvasGroup>().All(g=>g.alpha>.1f)).Select(t=>t.text).ToArray();
        // PNG 저장으로 벽시계가 늘어나도 표시 수명과 동일한 검수 시계를 사용한다.
        private static IEnumerator WaitFeedbackTime(float seconds)
        {
            float deadline=Effects.Vfx.VfxTiming.ReadableTime+seconds;
            while(Effects.Vfx.VfxTiming.ReadableTime<deadline)yield return null;
        }
        // 코드상 Rect 크기가 아니라 실제 폰트 메시와 화면 픽셀을 검사한다.
        private void CheckPartyHudReadability()
        {
            var cards=Object.FindObjectsByType<Entities.View.UnitCardView>(FindObjectsSortMode.None).Where(c=>c.gameObject.activeInHierarchy&&c.BoundUnit!=null&&c.BoundUnit.IsOnField).ToArray();
            var names=cards.SelectMany(c=>c.GetComponentsInChildren<TextMeshPro>(true)).Where(t=>t.name=="Name").ToArray();
            RuiCheck("양 진영 카드 이름 숨김", "이름은 호버로만 제공",names.Length.ToString(),
                names.Length>=AliveAllies().Count+AliveEnemies().Count && names.All(t=>!t.enabled || string.IsNullOrEmpty(t.text)));
            var unique=cards.SelectMany(c=>c.GetComponentsInChildren<TextMeshPro>()).Where(t=>t.name=="ResourceValue" && t.enabled && !string.IsNullOrEmpty(t.text)).ToArray();
            foreach(var label in unique)label.ForceMeshUpdate();
            RuiCheck("전장 고유 자원 렌더링", "표시한 고유 자원은 실제 글자 존재",string.Join(" / ",unique.Select(t=>t.text)),unique.All(t=>t.textInfo.characterInfo.Take(t.textInfo.characterCount).Any(c=>c.isVisible)));
            RuiCheck("전장 초월기 원형 게이지", "모든 활성 카드에 실제 수위 이미지",cards.Length.ToString(),cards.All(c=>c.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.name=="LiquidFront"&&i.enabled&&i.fillMethod==UnityEngine.UI.Image.FillMethod.Vertical)));
            Camera camera=Camera.main;
            float pixelsPerWorld=Screen.height/(camera.orthographicSize*2f);
            RuiCheck("전장 고유 자원 가독성", "요청된1920 화면 표시 자원 최소16px(공통 캡션15px 이상)",string.Join(" / ",unique.Select(t=>$"{t.text}:{t.fontSize*t.transform.lossyScale.y*.1f*pixelsPerWorld:F1}px")),unique.All(t=>t.fontSize*t.transform.lossyScale.y*.1f*pixelsPerWorld>=16f));
        }
        private void CheckFeedbackHudOverlap()
        {
            var hudRects = Object.FindObjectsByType<Entities.View.UnitCardView>(FindObjectsSortMode.None)
                .Select(c => c.HudScreenRect(Camera.main)).Where(r => r.width > 0).ToArray();
            var labels = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Where(t => t.name == "StatusName").ToArray();
            var overlaps = new List<string>();
            foreach (var label in labels)
            {
                Rect rect = RuiRect(label.transform);
                rect.yMin += 5 * label.GetComponentInParent<Canvas>().scaleFactor;
                rect.yMax -= 5 * label.GetComponentInParent<Canvas>().scaleFactor;
                if (hudRects.Any(h => h.Overlaps(rect))) overlaps.Add(label.text);
            }
            RuiCheck("상태 알림과 카드 정보 비중첩", "이름·체력 판과 겹침0", string.Join(" / ", overlaps), labels.Length > 0 && overlaps.Count == 0);
        }
        private IEnumerator ScenarioCombatFeedback(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear(); bool wasEnabled=true;
            yield return YvPrepareScene(config, value=>wasEnabled=value);
            GameManager game=GameManager.Instance;
            Unit ally=AliveAllies().First(), enemy=AliveEnemies().First();
            var aura=ally.GetStatus(70630);
            RuiCheck("중립 특성 오라의 상태 아이콘", "데바의 왕을 디버프로 표시하지 않음",aura?.StatusName??"없음",aura!=null && Managers.UI.Theme.StatusIcons.Resolve(aura).Glyph!=Managers.UI.Theme.StatusGlyph.Debuff);
            DebugMode.AllyInvincible=false; DebugMode.EnemyInvincible=false;
            yield return WaitUnscaled(8.2f);
            CheckPartyHudReadability();
            var focus=BuffStatus.Create(99770,"feedback_focus","집중",ally,ally,
                new StatModifierEffect(BaseEnums.StatType.Attack,1f),duration:2,stackPolicy:BaseEnums.StatusStackPolicy.RefreshDuration);
            ally.AddStatus(focus);
            ally.AddStatus(BuffStatus.Create(99770,"feedback_focus","집중",ally,ally,
                new StatModifierEffect(BaseEnums.StatType.Attack,1f),duration:2,stackPolicy:BaseEnums.StatusStackPolicy.RefreshDuration));
            enemy.GrantCombatElement(BaseEnums.UnitElement.Hydro,2,ally,true,true);
            ally.GrantCombatElement(BaseEnums.UnitElement.Cryo,2,ally,true,true);
            var shock=BuffStatus.Create(99771,"feedback_shock","감전",ally,enemy,
                new ReceivingDamageMultiplierEffect(1.01f),duration:2,category:BaseEnums.StatusCategory.Negative);
            shock.SkipEffectHitRoll=true; enemy.AddStatus(shock);
            int before=enemy.HpCurr;
            yield return WaitUnscaled(.18f);
            yield return new WaitForEndOfFrame();
            string[] names=FeedbackNames();
            var badge=Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None)
                .FirstOrDefault(image=>image.enabled && image.sprite==Managers.UI.Theme.StatusIcons.Element(BaseEnums.UnitElement.Cryo));
            RuiCheck("아군 부착 원소 하단 아이콘 보존","얼음 아이콘 표시",badge==null ? "없음" : badge.name,badge!=null && badge.enabled);
            foreach(var label in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Where(t=>t.gameObject.name=="StatusName"))
            {
                label.ForceMeshUpdate();
                bool visible=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.isVisible);
                RuiCheck("부여 알림 이름 실제 렌더링", "보이는 글자 있음",$"{label.text}: rect={label.rectTransform.rect.size}, font={label.fontSize}, chars={label.textInfo.characterCount}, preferred={label.preferredHeight}",visible);
            }
            RuiCheck("성공한 버프와 감전·물 부착 알림","세 이름 표시",string.Join(" / ",names),
                names.Any(n=>n.StartsWith("집중")) && names.Contains("감전") && names.Contains("물 원소 부여"));
            RuiCheck("동일 부여 갱신 알림 합침","집중 1개·갱신",string.Join(" / ",names),names.Count(n=>n.StartsWith("집중"))==1 && names.Contains("집중 · 갱신"));
            enemy.TakeDamage(new DamageContext(ally,1842,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage},true));
            ally.ModifyHp(ally.HpMax-150);ally.Heal(120,ally);
            var seenAfterDamage = new HashSet<string>();
            bool numberOverlap = false;
            bool initialNumbersChecked = false;
            float observeUntil = Time.unscaledTime + 1.45f;
            while (Time.unscaledTime < observeUntil)
            {
                yield return EndOfFrame;
                var visible = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
                    .Where(t => t.GetComponentsInParent<CanvasGroup>().All(g => g.alpha > .1f)).ToArray();
                var status = visible.Where(t => t.name == "StatusName").ToArray();
                var numbers = visible.Where(t => t.name == "Number" && t.GetComponentInParent<DamagePopup>() != null).ToArray();
                foreach (var label in status) seenAfterDamage.Add(label.text);
                numberOverlap |= status.Any(s => numbers.Any(n => RuiOverlaps(RuiRect(s.transform), RuiRect(n.transform))));
                Capture("feedback_number_status_sequence");
                if (!initialNumbersChecked && observeUntil - Time.unscaledTime < 1.27f)
                {
                    initialNumbersChecked = true;
                    RuiCheck("피해 직후 숫자 표시", "HP 감소·숫자 생성", $"{before}->{enemy.HpCurr}", enemy.HpCurr<before && numbers.Length>=2);
                }
            }
            RuiCheck("숫자와 상태 이름 동시 읽기", "연속 프레임 글자 영역 비중첩", numberOverlap.ToString(), !numberOverlap);
            RuiCheck("숫자 후 밀린 상태 알림 보존", "감전·물 부여 표시", string.Join(" / ", seenAfterDamage), seenAfterDamage.Contains("감전") && seenAfterDamage.Contains("물 원소 부여"));
            CheckFeedbackHudOverlap();
            yield return CaptureStill("feedback_status_critical_heal");
            yield return WaitUnscaled(1.5f);
            enemy.AddStatus(BuffStatus.Create(99772,"feedback_resist","저항 시험",null,enemy,new EhResistance()));
            yield return WaitUnscaled(1.5f);
            var refused=BuffStatus.Create(99773,"feedback_refused","거부된 상태",ally,enemy,
                new ReceivingDamageMultiplierEffect(1.1f),category:BaseEnums.StatusCategory.Negative);
            refused.SkipEffectHitRoll=true;enemy.AddStatus(refused);
            yield return WaitUnscaled(.18f);
            RuiCheck("저항 시 성공 알림 금지","성공 상태 알림 없음",string.Join(" / ",FeedbackNames()),!refused.ApplicationSucceeded && !FeedbackNames().Contains("거부된 상태"));
            yield return CaptureStill("feedback_effect_resisted");
            enemy.RemoveStatusByKey("feedback_resist");
            yield return WaitUnscaled(1.5f);
            enemy.AddShield(9999,enemy);enemy.TakeDamage(new DamageContext(ally,423,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage}));
            yield return WaitUnscaled(.18f);yield return CaptureStill("feedback_shielded");
            yield return WaitUnscaled(1.5f);
            Time.timeScale=8;
            enemy.TakeDamage(new DamageContext(ally,625,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage}));
            yield return WaitFeedbackTime(.35f);
            RuiCheck("8배속 숫자 읽기 시간","실시간 0.35초 후 존재",Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Length.ToString(),Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Length>0);
            yield return CaptureStill("feedback_speed8_readable");
            yield return WaitFeedbackTime(1.3f);
            RuiCheck("숫자 종료 정리","잔여 0",Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Length.ToString(),Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Length==0);
            // 같은 프레임에 이웃 유닛들의 알림을 생성해, 다음 프레임의 실제 글자 영역으로 겹침을 확인한다.
            foreach(var target in AliveAllies())
                for(int n=0;n<2;n++)target.AddStatus(BuffStatus.Create(99810+n,"feedback_neighbour"+n,"동시 부여 확인 "+n,target,target,
                    new StatModifierEffect(BaseEnums.StatType.Attack,0f),duration:2,stackPolicy:BaseEnums.StatusStackPolicy.RefreshDuration));
            yield return WaitUnscaled(.18f);
            yield return new WaitForEndOfFrame();
            var notices=Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Where(t=>t.name=="StatusNotice").ToArray();
            var overlaps=new List<string>();
            for(int i=0;i<notices.Length;i++) for(int j=i+1;j<notices.Length;j++)
            {
                Rect a=RuiRect(notices[i]),b=RuiRect(notices[j]);float inset=5f*notices[i].GetComponentInParent<Canvas>().scaleFactor;
                a.yMin+=inset;a.yMax-=inset;b.yMin+=inset;b.yMax-=inset;
                if(RuiOverlaps(a,b))overlaps.Add(i+"/"+j);
            }
            RuiCheck("이웃 동시 부여 알림 겹침 방지", "동시2유닛 이상 · 실제 글자 영역 비중첩",$"shown={notices.Length} overlaps={string.Join(",",overlaps)}",notices.Length>=4 && overlaps.Count==0);
            CheckFeedbackHudOverlap();
            yield return CaptureStill("feedback_neighbour_simultaneous");
            yield return WaitUnscaled(3.5f);
            RuiCheck("이웃 알림 큐 종료 정리", "알림 잔여0",FeedbackNames().Length.ToString(),FeedbackNames().Length==0);
            for(int i=0;i<5;i++) ally.AddStatus(BuffStatus.Create(99780+i,"feedback_queue"+i,"공격 강화 "+(i+1),ally,ally,new StatModifierEffect(BaseEnums.StatType.Attack,1f)));
            yield return WaitUnscaled(.18f);
            RuiCheck("동시 부여 줄 수 제한","캐릭터당 최대 3줄",FeedbackNames().Length.ToString(),FeedbackNames().Length<=3);
            yield return CaptureStill("feedback_status_queue");
            var queuedNames = new HashSet<string>();
            float queueDeadline = Time.unscaledTime + 8;
            while (Time.unscaledTime < queueDeadline)
            {
                foreach (string name in FeedbackNames()) queuedNames.Add(name);
                if (queuedNames.Contains("공격 강화 4") && queuedNames.Contains("공격 강화 5")) break;
                yield return null;
            }
            RuiCheck("대기 상태 순서 표시","공간을 기다린 4·5번 알림도 표시",string.Join(" / ",queuedNames),queuedNames.Contains("공격 강화 4") && queuedNames.Contains("공격 강화 5"));
            yield return WaitUnscaled(1.5f);
            for(int i=0;i<5;i++)enemy.TakeDamage(new DamageContext(ally,110+i*23,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage}));
            yield return WaitUnscaled(.18f);
            var burst=Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Where(p=>p.gameObject.activeInHierarchy).ToArray();
            RuiCheck("연속 피해 숫자 자리 제한", "최대 4개·서로 다른 2차원 위치",burst.Length.ToString(),burst.Length==4 && burst.Select(p=>p.GetComponent<RectTransform>().anchoredPosition).Distinct().Count()==4);
            yield return CaptureStill("feedback_multi_hit");
            game.gameState=BaseEnums.GameState.Preparation;yield return WaitUnscaled(.1f);
            RuiCheck("전투 종료 상태 알림 정리","잔여 0",FeedbackNames().Length.ToString(),FeedbackNames().Length==0);
            game.ActionScheduler.EndRound();GridManager.Instance.OnRoundEnd();game.enabled=wasEnabled;
            Time.timeScale=1;RuiFinish();
        }
    }
}
#endif
