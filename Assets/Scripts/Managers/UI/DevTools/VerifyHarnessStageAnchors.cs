#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using BaseClasses;
using Core;
using Effects;
using Effects.Vfx;
using Entities;
using Entities.View;
using Managers.Stage;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioStageHitAnchors(VerifyScenarioRequest config)
        {
            if (VisualModeState.Current != VisualMode.Stage) { FailCurrent("Stage 전용 앵커 검사다."); yield break; }
            yield return PrepareStage(config, Stage(config, 6), false, true, true);
            ConfigureStageHudEnemies(config);
            if (config.trigger == "d2") { yield return CaptureStill("stage_d2"); yield break; }
            GameManager game = GameManager.Instance;
            bool enabled = game.enabled;
            game.enabled = false;
            game.gameState = BaseEnums.GameState.RoundInProgress;
            game.uiManager?.HidePreparationPhasePanel();
            game.ActionScheduler.BeginRound();
            // 수동 촬영도 현재 편성으로 집계를 시작한다. 이전 런의 파괴된 유닛 행을 남기지 않는다.
            Combat.DamageMeter.Begin(AliveAllies());
            GridManager.Instance.RefreshFieldLayout();
            float priorDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                yield return WaitUnscaled(.4f);
                CheckS6DamageMeter();
                Camera camera = Camera.main;
                foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
                {
                    UnitCardView view = unit.currentCell.Card;
                    Bounds lower = view.StageProxy.LowerWorldBounds;
                    Vector3 chest = view.StageProxy.ChestWorld;
                    bool Inside(Vector3 p) => p.x >= lower.min.x-.0001f && p.x <= lower.max.x+.0001f &&
                        p.y >= lower.min.y-.0001f && p.y <= lower.max.y+.0001f;
                    RecordMechanic("stage_anchor_" + unit.UnitName, "공개 중심=chest·모든 접점 Lower 안·portraitContacts 미사용",
                        $"center={view.CenterWorld}, chest={chest}, lower={lower}, contact={VfxSpace.ContactPoint(unit)}",
                        (view.CenterWorld-chest).sqrMagnitude < .000001f &&
                        (view.PortraitFocusWorldPoint-chest).sqrMagnitude < .000001f &&
                        Inside(VfxSpace.ContactPoint(unit)) && Inside(VfxSpace.BodyContactPoint(unit)) &&
                        Inside(view.ClampPortraitHitPoint(chest + Vector3.one * 100f)) &&
                        Inside(view.ClampPortraitHitPoint(chest - Vector3.one * 100f)) &&
                        !view.TryAuthoredPortraitContact(out _));
                    Rect head = view.PortraitFocusScreenRect(camera), hud = view.HudScreenRect(camera);
                    Bounds ellipse = view.StageProxy.HeadWorldBounds;
                    Vector3 lo = camera.WorldToScreenPoint(ellipse.min), hi = camera.WorldToScreenPoint(ellipse.max);
                    Rect badge = view.ResourceBadgeScreenRect(camera);
                    RecordMechanic("stage_rect_" + unit.UnitName, "Head 타원 외접·발밑 묶음·고유 배지 묶음 내부",
                        $"head={head}, hud={hud}, badge={badge}",
                        Mathf.Abs(head.xMin-lo.x)<.1f && Mathf.Abs(head.yMax-hi.y)<.1f &&
                        hud == view.StageHudHitRect(camera) &&
                        (badge.width<=0f || (hud.Contains(badge.min) && hud.Contains(badge.max))));
                    Vector2 Screen(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null,
                        CombatFeedbackSurface.Root.TransformPoint(local));
                    Vector2 notice = Screen(CombatFeedbackSurface.Position(unit, true));
                    Vector2 number = Screen(CombatFeedbackSurface.Position(unit, false));
                    RecordMechanic("stage_text_origin_" + unit.UnitName, "상태 Head+26px·숫자 chest+60px",
                        $"notice={notice.y-camera.WorldToScreenPoint(view.StageProxy.VisibleHeadWorldBounds.max).y:F3}, number={number.y-camera.WorldToScreenPoint(chest).y:F3}",
                        Mathf.Abs(notice.y-camera.WorldToScreenPoint(view.StageProxy.VisibleHeadWorldBounds.max).y-26f)<.1f &&
                        Mathf.Abs(number.y-camera.WorldToScreenPoint(chest).y-60f)<.1f);
                }
                // 실루엣은 가려진 카드 스프라이트를 복제하지 않고 같은 SD 마스크를 보존해야 한다.
                UnitCardView sample = AliveAllies().First().currentCell.Card;
                GameObject snapshot = sample.CreateDeathSnapshot(out var portrait);
                var slices = snapshot.GetComponentsInChildren<SpriteRenderer>();
                RecordMechanic("stage_snapshot_slices", "Head/Lower 독립 재질·같은 mask·침식에도 mask 보존",
                    string.Join(",", slices.Select(s => s.name)), slices.Length == 2 &&
                    slices.All(s => s.sharedMaterial.shader.name == "NeverTheLast/StageFigureSlice") && portrait != null);
                foreach (var slice in slices)
                {
                    ErosionFx.Attach(slice, 0f, 0f, 0f, .1f, Color.white, Color.white, Color.black);
                    RecordMechanic("stage_snapshot_erosion_"+slice.name, "침식 Stage mask 유지", slice.sharedMaterial.shader.name,
                        slice.sharedMaterial.GetFloat("_StageSlice") > .5f);
                }
                Destroy(snapshot);
                yield return CaptureStill("stage_anchors");

                Unit melee = AliveAllies().First(u => u.ID == 7);
                Unit archer = AliveAllies().First(u => u.ID == 165);
                Unit crystal = AliveAllies().First(u => u.ID == 1);
                Unit target = AliveEnemies().First();
                foreach (string key in new[] { "jean.holy_strike", "atalante.normal", "sei.bigbang" })
                {
                    Unit caster = key.StartsWith("jean") ? melee : key.StartsWith("atalante") ? archer : crystal;
                    bool area = key == "sei.bigbang";
                    int callbacks = 0, peaks = 0;
                    bool captured = false;
                    IEnumerator PeakFrame(VfxSlotPlaybackEvent e)
                    {
                        yield return EndOfFrame;
                        Bounds lower = e.Target.currentCell.Card.StageProxy.LowerWorldBounds;
                        Vector3 contact = VfxSpace.ContactPoint(e.Target);
                        RecordMechanic("stage_peak_"+key+"_"+e.Target.UnitName, "실제 정점·몸통 접점·CombatVfx 앞 레이어",
                            $"contact={contact}, event={e.WorldPoint}, lower={lower}, layer={e.SortingLayerId}",
                            contact.x>=lower.min.x && contact.x<=lower.max.x && contact.y>=lower.min.y && contact.y<=lower.max.y &&
                            e.WorldPoint.x>=lower.min.x && e.WorldPoint.x<=lower.max.x && e.WorldPoint.y>=lower.min.y && e.WorldPoint.y<=lower.max.y &&
                            e.SortingLayerId == SortingLayer.NameToID("CombatVfx"));
                        // 논리 정점에서 막 생성된 명중광은 첫 셀이 비어 있다. 실제 필수 정점 셀의 렌더를 촬영한다.
                        if (!captured)
                        {
                            captured = true;
                            bool observed=false;
                            for (int frame=0; frame<24; frame++)
                            {
                                bool crest=FindObjectsByType<FlipbookFx>(FindObjectsSortMode.None)
                                    .Any(f => f.RequiredVisibleFrame>=0 && f.DisplayedFrame==f.RequiredVisibleFrame &&
                                        (f.transform.position-VfxSpace.ContactPoint(e.Target)).sqrMagnitude<.1f);
                                if (crest) { observed=true; break; }
                                yield return null;
                            }
                            yield return EndOfFrame;
                            RecordMechanic("stage_rendered_crest_"+key, "실제 명중 필수 정점 셀", observed.ToString(), observed);
                            Capture("peak_"+key);
                        }
                    }
                    void Peak(VfxSlotPlaybackEvent e)
                    {
                        if (e.SkillKey != key || e.Phase != "peak" || e.SlotName != "hit") return;
                        peaks++; StartCoroutine(PeakFrame(e));
                    }
                    VfxComposer.SlotPlayed += Peak;
                    try
                    {
                        if (area) yield return VfxComposer.PlayMultiTargetHits(key, caster, AliveEnemies(), _ => callbacks++, .05f);
                        else if (key == "jean.holy_strike") yield return JeanFx.HolyStrike(caster, target, false, () => callbacks++);
                        else yield return VfxComposer.PlaySingleTarget(key, caster, target, () => callbacks++);
                        yield return new WaitForSeconds(.7f);
                        RecordMechanic("stage_callback_"+key, "정상 완료·대상별 콜백1회·정점 촬영",
                            $"callbacks={callbacks}, peaks={peaks}, captured={captured}",
                            callbacks == (area ? AliveEnemies().Count : 1) && peaks > 0 && captured);
                    }
                    finally { VfxComposer.SlotPlayed -= Peak; }
                    BattlefieldBackdrop.ClearFocus();
                }
                DamagePopup.Show(target, 123, false, Color.white, actionId: 987654);
                DamagePopup.Show(target, 234, false, Color.white, actionId: 987654);
                var cumulative = FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)
                    .Single(p => p.ActionId == 987654 && p.Recipient == target);
                RecordMechanic("stage_number_cumulative", "같은 행동·대상 2타 합계357", $"{cumulative.HitCount}/{cumulative.DisplayedAmount}",
                    cumulative.HitCount == 2 && cumulative.DisplayedAmount == 357);
                yield return CaptureStill("number_cumulative");
            }
            finally
            {
                Time.captureDeltaTime = priorDelta;
                game.ActionScheduler.EndRound(); GridManager.Instance.OnRoundEnd();
                game.gameState = BaseEnums.GameState.Preparation; game.enabled = enabled;
            }
        }
    }
}
#endif
