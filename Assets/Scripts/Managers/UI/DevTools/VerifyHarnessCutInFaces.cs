#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using BaseClasses;
using Core;
using Effects.Vfx;
using Entities;
using Managers.Stage;
using TMPro;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioS4CutIn(VerifyScenarioRequest config)
        {
            bool enabled = GameManager.Instance.enabled;
            float prior = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return S8Prepare(config);
            var game = GameManager.Instance;
            var scheduler = game.ActionScheduler;
            try
            {
                var data = DataManager.LoadStageFigureDataList();
                foreach (var bad in new[] { new CutInFaceData(), new CutInFaceData { x = -0.1f, y = .5f }, new CutInFaceData { x = .5f, y = 1.1f } })
                {
                    bool rejected = false;
                    try { bad.Validate(); } catch (InvalidOperationException) { rejected = true; }
                    RecordMechanic("s4_ratio_reject", "누락/범위 밖 cutinFace 거절", $"{bad.x}/{bad.y}", rejected);
                }
                RecordMechanic("s4_environment", "현재 프로젝트·열린 Unity IPC·Stage", Application.dataPath + "|" + Application.unityVersion,
                    VisualModeState.Current == VisualMode.Stage);
                foreach (var caster in AliveAllies().Where(u => u.ID == 81).Concat(AliveEnemies()).ToArray())
                foreach (float speed in new[] { 1f, 5f })
                {
                    foreach (var unit in AliveAllies().Concat(AliveEnemies())) { unit.ClearUltimateResource(); unit.RestoreHpOutsideCombat(unit.HpMax); }
                    bool done = false, crest = false, paused = false;
                    int starts = 0, ends = 0;
                    void Start(Unit u, ActionScheduler.ActionKind k, string label) { if (u == caster && k == ActionScheduler.ActionKind.Ultimate) starts++; }
                    void End(Unit u, ActionScheduler.ActionKind k) { if (u == caster && k == ActionScheduler.ActionKind.Ultimate) { done = true; ends++; } }
                    ActionScheduler.AnyActionStarted += Start;
                    ActionScheduler.AnyActionFinished += End;
                    Time.timeScale = speed;
                    scheduler.BeginRound(); caster.FillUltimateResource();
                    try
                    {
                        float deadline = Time.realtimeSinceStartup + 45f;
                        while ((!done || scheduler.IsPresenting) && Time.realtimeSinceStartup < deadline)
                        {
                            scheduler.Tick(Time.deltaTime);
                            yield return EndOfFrame;
                            var cut = UltimateCutIn.Current;
                            if (!UltimateCutIn.IsShowing(caster)) continue;
                            if (!paused && cut.Progress >= .25f && cut.Progress < .86f)
                            {
                                paused = true;
                                float progress = cut.Progress; var point = cut.FaceWorld;
                                Time.timeScale = 0f;
                                yield return WaitUnscaled(.3f);
                                RecordMechanic($"s4_pause_{caster.ID}_{speed}", "0.3초 정지 중 진행률/원화 위치 불변",
                                    $"{progress}/{cut.Progress}", progress == cut.Progress && point == cut.FaceWorld && starts == 0);
                                Time.timeScale = speed;
                            }
                            if (crest || cut.Progress < .4f || cut.Progress >= .86f) continue;
                            crest = true;
                            var source = caster.currentCell.Card.StageProxy.StandingSource;
                            var p = data.figures.Find(f => f.sprite == source.name) ?? data.@default;
                            bool nonHuman = !p.deform && p.sd == null, whole = nonHuman && p.cutinFace == null;
                            Vector2 pivot = whole ? new Vector2(.5f,.5f) : nonHuman ? new Vector2(p.cutinFace.x,1f-p.cutinFace.y) : new Vector2(p.cx,1f-(p.top+p.chin)*.5f);
                            RecordMechanic($"s4_frame_{caster.ID}_{speed}", "DTO 실제 반영·얼굴 피벗/전체 축소·등방·입력 비차단",
                                $"sprite={source.name}, pivot={cut.Art.pivot}, size={cut.Art.sizeDelta}, whole={whole}",
                                (cut.Art.pivot-pivot).sqrMagnitude < .000001f &&
                                Mathf.Abs(cut.Art.rect.width/cut.Art.rect.height-source.rect.width/source.rect.height)<.00001f &&
                                Mathf.Abs(cut.Art.rect.height-(whole?250f:nonHuman?600f:1100f))<.001f && !cut.GetComponent<CanvasGroup>().blocksRaycasts);
                            // 원본 PNG를 직접 보고 정한 머리 외접 영역. fallback은 투명 여백을 포함한 원화 전체다.
                            Rect roi = caster.ID switch
                            {
                                1001 => Rect.MinMaxRect(54f/1024f, 650f/1536f, 330f/1024f, 1070f/1536f),
                                11070 => Rect.MinMaxRect(230f/1024f, 700f/1536f, 420f/1024f, 1010f/1536f),
                                11142 => Rect.MinMaxRect(45f/1024f, 600f/1536f, 650f/1024f, 1050f/1536f),
                                _ => whole ? Rect.MinMaxRect(0,0,1,1) : Rect.MinMaxRect(p.cx-.07f,p.top,p.cx+.07f,p.chin)
                            };
                            bool inside = true;
                            var art = cut.Art.rect;
                            foreach (float x in new[] { roi.xMin, roi.xMax }) foreach (float y in new[] { roi.yMin, roi.yMax })
                            {
                                var point = cut.Band.InverseTransformPoint(cut.Art.TransformPoint(new Vector3(art.xMin+art.width*x,art.yMax-art.height*y)));
                                var band = cut.Band.rect; band.xMin-=.01f; band.xMax+=.01f; band.yMin-=.01f; band.yMax+=.01f;
                                inside &= band.Contains(point);
                            }
                            var skill = cut.GetComponentsInChildren<TMP_Text>().First(t => t.name == "Skill");
                            skill.ForceMeshUpdate(); bool title = true;
                            foreach (var ch in skill.textInfo.characterInfo.Take(skill.textInfo.characterCount).Where(c => c.isVisible))
                            foreach (var point in new[] { ch.bottomLeft,ch.topLeft,ch.topRight,ch.bottomRight })
                                title &= cut.Band.rect.Contains(cut.Band.InverseTransformPoint(skill.transform.TransformPoint(point)));
                            RecordMechanic($"s4_head_{caster.ID}_{speed}", "머리/전체 원화·기술명이 띠 안", $"head={inside}, title={title}, progress={cut.Progress}", inside && title);
                            if (speed == 1f) Capture("cutin_crest_" + caster.ID);
                        }
                        RecordMechanic($"s4_cast_{caster.ID}_{speed}", "실제 초월기 시작/완료 각1회·컷인 정점", $"start={starts}, end={ends}, crest={crest}", starts==1 && ends==1 && crest);
                    }
                    finally
                    {
                        ActionScheduler.AnyActionStarted -= Start; ActionScheduler.AnyActionFinished -= End;
                        scheduler.EndRound(); caster.ActiveUltimateCode.StopCode();
                    }
                    yield return new WaitForSeconds(.4f);
                }
            }
            finally
            {
                scheduler.EndRound(); game.gameState=BaseEnums.GameState.Preparation; game.enabled=enabled;
                Time.captureDeltaTime=prior; Time.timeScale=1f;
            }
        }
    }
}
#endif
