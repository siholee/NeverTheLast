#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive;
using Combat;
using Core;
using Entities;
using Entities.View;
using Managers.Stage;
using TMPro;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioSummonStage(VerifyScenarioRequest config)
        {
            if (config.trigger == "front_v11") { yield return ScenarioSingleSummonFrontV11(config); yield break; }
            if (config.trigger != null && config.trigger.StartsWith("layout_")) { yield return ScenarioSummonLayoutFix(config); yield break; }
            if (config.trigger == "authors") { yield return ScenarioSummonStageAuthors(config); yield break; }
            bool enabled = GameManager.Instance.enabled;
            float previous = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f; Time.timeScale = 1;
            yield return S8Prepare(config, startRound: true);
            var game = GameManager.Instance; var grid = GridManager.Instance;
            var uchi = AliveAllies().Single(u => u.ID == 180);
            var loki = AliveAllies().Single(u => u.ID == 80);
            var orion = AliveAllies().Single(u => u.ID == 22);
            var enemy = AliveEnemies().First();
            var trace = new List<(StageSummonVisual visual, string kind, string phase, float time, int frame, SdFrameRig.Pose pose)>();
            var strikes = new Dictionary<int, float>();
            var impacts = new List<(int actor, float delay)>();
            bool guardFrontSeen = false, guardFrontCorrect = false;
            void Observe(StageSummonVisual v, string p)
            {
                trace.Add((v, v.Kind, p, Time.time, Time.frameCount, v.Pose));
                if (p is "spawn_end" or "windup" or "strike" or "guard_hit" or "rain_windup" or "owner_strike" or "down" or "owner_down")
                    SdRenderSelected(v.Renderer, v.Kind, v.Pose, "summon_frames");
                if (p == "windup" && v.Summon != null) strikes[v.Summon.GetInstanceID()] = Time.time;
                if (p == "guard_hit")
                {
                    guardFrontSeen = true;
                    var ownerView = v.Owner.currentCell.Card;
                    guardFrontCorrect = v.Detached && v.GetComponent<Unit>() == null &&
                        v.Renderer.sortingOrder == ownerView.PortraitRenderer.sortingOrder + 1 &&
                        ownerView.StageProxy.Frames.CurrentPose == SdFrameRig.Pose.IDLE;
                    RecordMechanic("sm_guard_front", "표시잔상은 주인+1·Unit없음·주인IDLE", $"order={v.Renderer.sortingOrder}/{ownerView.PortraitRenderer.sortingOrder}, owner={ownerView.StageProxy.Frames.CurrentPose}", guardFrontCorrect);
                }
            }
            void Damage(Unit target, DamageContext context)
            {
                var actor = game.ActionScheduler.ExecutingUnit;
                if (actor != null && actor.IsSummon && strikes.TryGetValue(actor.GetInstanceID(), out float start))
                    impacts.Add((actor.ID, Time.time - start));
            }
            StageSummonVisual.MotionObserved += Observe; Unit.AnyDamageIncoming += Damage;
            try
            {
                RecordMechanic("sm_environment", "열린6000.4.9f1·현재 프로젝트·1920×1080 Stage", Application.dataPath + "|" + Application.unityVersion,
                    Application.dataPath.Replace('\\','/').EndsWith("/NeverTheLast/Assets") && Application.unityVersion == "6000.4.9f1" && !Application.isBatchMode && Screen.width == 1920 && Screen.height == 1080 && VisualModeState.Current == VisualMode.Stage);
                for (int i = 0; i < 3; i++) Summons.Create(uchi);
                yield return new WaitForSeconds(.65f);
                var clones = Summons.Of(uchi).ToArray();
                RecordMechanic("sm_three", "분신3·오래된 순서", string.Join(",", clones.Select(c => c.GetInstanceID())), clones.Length == 3);
                var visuals = FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None);
                foreach (var v in visuals)
                {
                    int index = v.Summon != null ? Summons.Of(v.Owner).IndexOf(v.Summon) : 0;
                    float[] front = { 100, -165, -250 }, up = { -6, 28, -24 };
                    bool cohort = v.Kind.StartsWith("isis_guardian_");
                    float dx = cohort ? new[] { 105, -170, -255 }[index] : v.IsSwarm ? front[index] : 128;
                    float dy = cohort ? new[] { 26, 56, 8 }[index] : v.IsSwarm ? up[index] : v.Kind == "fenrir" ? 20 : -12;
                    if (v.Kind == "fenrir") dx = -70;
                    var owner = v.Owner.currentCell.Card; var figure = owner.StageProxy;
                    float px = StageLayout.WorldHeight / 1080f, ds = v.Owner.currentCell.transform.lossyScale.y;
                    Vector3 expected = figure.transform.position + new Vector3((v.Owner.IsEnemy ? -1 : 1) * dx, dy, 0) * px * ds;
                    if (v.IsSurround) expected = SmExpectedHome(v,index);
                    else if (v.IsFrontCompanion) expected = SmSingleFrontHome(v);
                    float error = Vector3.Distance(v.Feet, expected) / px;
                    float scale = cohort ? .45f : v.Kind == "uchi_clone" ? .55f : v.Kind == "fenrir" ? .62f : .6f;
                    int order = owner.PortraitRenderer.sortingOrder + (v.DrawsInFront ? 1 + index : -1 - index);
                    if (cohort) order = StageSummonVisual.SpiritSortingOrder + index;
                    RecordMechanic("sm_slot_" + v.Kind + "_" + index, "슬롯≤1px·몸높이 배율·다수/앞동반형 주인+1+i·체력판 없음",
                        $"errorPx={error:R}, feet={v.Feet}, ds={ds:R}, height={v.Height:R}, order={v.Renderer.sortingOrder}/{owner.PortraitRenderer.sortingOrder}",
                        error <= 1 && Mathf.Abs(v.Height / figure.BodyWorldHeight - scale) < .0001f && v.Renderer.sortingOrder == order && SmSpiritSorting(v) &&
                        (v.Summon == null || !v.Summon.GetComponent<SummonCardView>().HasHealthDisplay));
                    SmOcclusion(v);
                    if (v.Kind == "uchi_clone")
                    {
                        Color tint = v.Renderer.color;
                        RecordMechanic("sm_clone_tint_" + index, "분신 곱색(.88,1,.96)·불투명도.88", tint.ToString(),
                            Mathf.Abs(tint.r-.88f)<.0001f && Mathf.Abs(tint.g-1f)<.0001f && Mathf.Abs(tint.b-.96f)<.0001f && Mathf.Abs(tint.a-.88f)<.0001f);
                    }
                }
                foreach (var visual in visuals)
                {
                    var unused = visual.Kind == "fenrir" ? new[] { SdFrameRig.Pose.HIT, SdFrameRig.Pose.DOWN }
                        : visual.Kind == "uchi_clone" ? new[] { SdFrameRig.Pose.DOWN } : new[] { SdFrameRig.Pose.HIT };
                    foreach (var pose in unused) SmRenderUnused(visual, pose);
                }
                var sirius = StageSummonVisual.Sirius(orion);
                RecordMechanic("sm_sirius_display_only", "시리우스에는 Unit·summonList·행동서열·대상 참가 없음", $"visual={sirius != null}; summons={grid.summonList.Count}",
                    sirius != null && sirius.GetComponent<Unit>() == null && sirius.Summon == null && !grid.summonList.Any(s => s.ID == 22 || s.SummonKind == "sirius") &&
                    game.ActionScheduler.ForecastOrder(24).All(r => r.Unit == null || r.Unit.SummonKind != "sirius") &&
                    CombatTargets.AliveEnemies(enemy).All(u => u == null || u.GetComponent<StageSummonVisual>() == null));
                foreach (var owner in new[] { uchi, loki, orion })
                {
                    string badge = owner.currentCell.Card.GetComponentsInChildren<TextMeshPro>(true).Single(t => t.name == "StageSummonBadge").text;
                    RecordMechanic("sm_badge_" + owner.ID, "분신×3 / 펜리르 / 시리우스5칸", badge,
                        owner == uchi ? badge == "분신 ×3" : owner == loki ? badge == "펜리르" : badge.Contains("시리우스") && badge.Count(c => c == '◆') == 5);
                }
                yield return CaptureStill("battle_five");
                yield return SmShots("uchi_three", () => { }, .55f);
                var frontCell = uchi.currentCell;
                var rearCell = AliveAllies().First(u => u.currentCell.xPos == -2).currentCell;
                bool movedRear = grid.RelocateUnit(uchi, rearCell);
                yield return grid.WaitForFieldLayoutSettled(); yield return new WaitForSeconds(.35f);
                RecordMechanic("sm_rear_fixture", "실제 배치 API로 우치 후열 이동", uchi.currentCell.xPos.ToString(), movedRear && uchi.currentCell.xPos == -2);
                SmSwarmFormation(uchi, "rear"); yield return CaptureStill("uchi_rear_three");
                bool restored = grid.RelocateUnit(uchi, frontCell);
                yield return grid.WaitForFieldLayoutSettled(); yield return new WaitForSeconds(.35f);
                RecordMechanic("sm_front_restore", "동일 객체/분신 유지·전열 복원", uchi.currentCell.xPos.ToString(), restored && uchi.currentCell == frontCell && Summons.Of(uchi).Count == 3);
                foreach (var owner in new[] { uchi, loki })
                {
                    owner.TakeDamage(new DamageContext(enemy, 1, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }));
                    int ownerHits = 0, summonHits = 0;
                    float until = Time.time + .45f;
                    while (Time.time < until)
                    {
                        yield return EndOfFrame;
                        if (owner.currentCell.Card.StageProxy.Frames.CurrentPose != SdFrameRig.Pose.HIT) continue;
                        ownerHits++;
                        summonHits += FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Count(v => v.Owner == owner && v.Pose == SdFrameRig.Pose.HIT);
                    }
                    RecordMechanic("sm_owner_hit_independent_" + owner.ID, "주인 실제 피격은 소환수HIT로 복제하지 않음(대신맞기는 별도)",
                        $"ownerHitSamples={ownerHits}, summonHitSamples={summonHits}", ownerHits > 0 && summonHits == 0);
                }
                Unit oldest = clones[0];
                yield return SmShots("uchi_overflow", () => Summons.Create(uchi), .8f);
                RecordMechanic("sm_overflow", "기존 즉시 타격·가장 오래된1기 사망·기수3", Summons.Of(uchi).Count.ToString(),
                    oldest == null && Summons.Of(uchi).Count == 3 && trace.Any(t => t.phase == "overflow_strike") && trace.Any(t => t.phase == "reslot_end"));
                int hp = uchi.HpCurr;
                var guarded = Summons.Of(uchi).First();
                yield return SmShots("uchi_guard", () => uchi.TakeDamage(new DamageContext(enemy, uchi.HpMax, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.NormalAttack, DamageTag.SingleTarget, DamageTag.TrueDamage })), .65f);
                RecordMechanic("sm_guard_logic", "같은 호출에서 사망·피해0·분신1감소", $"hp={hp}/{uchi.HpCurr}, count={Summons.Of(uchi).Count}",
                    guarded == null && hp == uchi.HpCurr && Summons.Of(uchi).Count == 2);
                var guardStart = trace.Last(t => t.phase == "guard_start"); var guardHit = trace.Last(t => t.phase == "guard_hit");
                var guardEnd = trace.Last(t => t.phase == "depart" && t.visual == guardStart.visual);
                RecordMechanic("sm_guard_motion", "표시0.12초 주인앞72px 이동·HIT0.25초·앞정렬 유지", $"advance={guardHit.time-guardStart.time:R}, hit={guardEnd.time-guardHit.time:R}",
                    Mathf.Abs(guardHit.time-guardStart.time-.12f) <= 1f/60f+.002f && Mathf.Abs(guardEnd.time-guardHit.time-.25f) <= 1f/60f+.002f);
                RecordMechanic("sm_guard_front_coverage", "실제 대신맞기 앞정렬 관측", guardFrontSeen.ToString(), guardFrontSeen && guardFrontCorrect);

                Unit clone = Summons.Of(uchi).First();
                Coroutine shot = StartCoroutine(SmActionShots("uchi_turn", clone));
                yield return NordAction(clone, Kind.Normal, s => s.AdvanceAction(clone, 1));
                yield return shot;
                Summons.Create(uchi); Summons.Create(uchi); yield return new WaitForSeconds(.6f);
                shot = StartCoroutine(SmActionShots("uchi_ultimate", uchi));
                yield return NordAction(uchi, Kind.Ultimate, s => uchi.FillUltimateResource()); yield return shot;
                var ultimate = trace.Where(t => t.kind == "uchi_clone" && t.phase == "strike").ToArray();
                RecordMechanic("sm_ultimate_motion", "남은 분신 동시 타격·모두 소모", string.Join(",", ultimate.Select(t => t.frame)), Summons.Of(uchi).Count == 0 && ultimate.Length >= 3 && ultimate.TakeLast(3).Select(t => t.frame).Distinct().Count() == 1);

                bool externalObserved = false;
                void External(Unit owner, Unit summon, SummonSource source, object tag) { if (owner == uchi && source == SummonSource.External) externalObserved = true; }
                Summons.AnySummonCreated += External;
                Unit extra;
                try { extra = Summons.RequestExtra(uchi, AliveAllies().Single(u => u.ID == 65).ActiveUltimateCode); }
                finally { Summons.AnySummonCreated -= External; }
                yield return new WaitForSeconds(.65f);
                RecordMechanic("sm_external_swarm", "공통 외부생성 API도 같은 Swarm 표시", $"created={extra != null}, external={externalObserved}", extra != null && externalObserved && extra.GetComponent<SummonCardView>().StageVisual.IsSwarm);
                SmSwarmFormation(uchi, "external"); extra.Die(null); yield return null;

                var fenrir = Summons.Of(loki).Single();
                shot = StartCoroutine(SmActionShots("fenrir_turn", fenrir));
                yield return NordAction(fenrir, Kind.Normal, s => s.AdvanceAction(fenrir, 1)); yield return shot;
                ((LokiUnboundWolf)loki.ActivePassiveCodes.First(c => c is LokiUnboundWolf)).GrantFlex();
                shot = StartCoroutine(SmActionShots("fenrir_chase", fenrir));
                yield return NordAction(loki, Kind.Normal, s => s.AdvanceAction(loki, 1)); yield return shot;
                yield return new WaitForSeconds(.6f);
                RecordMechanic("sm_fenrir_return", "추격·자기차례→STRIKE→0.20초 유지→0.32초 복귀→IDLE", string.Join(",", trace.Where(t => t.kind == "fenrir").Select(t => t.phase)),
                    trace.Count(t => t.kind == "fenrir" && t.phase == "strike") >= 2 && trace.Count(t => t.kind == "fenrir" && t.phase == "return_idle") >= 2 && fenrir.GetComponent<SummonCardView>().StageVisual.Pose == SdFrameRig.Pose.IDLE);
                int siriusHitSamples = 0, siriusHitMismatches = 0;
                IEnumerator ObserveOwnerHit()
                {
                    float until = Time.time + .6f;
                    while (Time.time < until)
                    {
                        yield return EndOfFrame;
                        if (orion.currentCell.Card.StageProxy.Frames.CurrentPose != SdFrameRig.Pose.HIT) continue;
                        siriusHitSamples++; if (sirius.Pose != SdFrameRig.Pose.IDLE) siriusHitMismatches++;
                    }
                }
                var hitSampler = StartCoroutine(ObserveOwnerHit());
                yield return SmShots("orion_hit", () => orion.TakeDamage(new DamageContext(enemy, 1, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.NormalAttack, DamageTag.TrueDamage })), .6f);
                yield return hitSampler;
                RecordMechanic("sm_sirius_hit", "오리온 실제 HIT 동안 시리우스는 IDLE·주인 피격 복제 없음", $"samples={siriusHitSamples}, mismatches={siriusHitMismatches}",
                    siriusHitSamples > 0 && siriusHitMismatches == 0 && !trace.Any(t => t.kind == "sirius" && t.phase == "owner_hit"));
                shot = StartCoroutine(SmActionShots("orion_rain", orion));
                yield return CfRun(s => orion.ActivePassiveCodes.OfType<OrionSirius>().Single().Gain(5),
                    () => !orion.ActivePassiveCodes.OfType<OrionSirius>().Single().Pending && orion.GetCombatResource(OrionSirius.ResourceId) == 0);
                yield return shot;
                RecordMechanic("sm_sirius_rain", "자원5소비 화살비에서만 동반WINDUP·STRIKE", string.Join(",", trace.Where(t => t.kind == "sirius").Select(t => t.phase)),
                    trace.Any(t => t.kind == "sirius" && t.phase == "rain_windup") && trace.Any(t => t.kind == "sirius" && t.phase == "owner_strike"));
                foreach (var entry in impacts)
                    RecordMechanic("sm_impact_window_" + entry.actor, "분신0.24초/펜리르0.10초 기존타격창±1프레임", entry.delay.ToString("R"), Mathf.Abs(entry.delay - (entry.actor == 901 ? .24f : .10f)) <= 1f/60f+.002f);
                RecordMechanic("sm_impact_coverage", "실제 분신·펜리르 타격 관측", string.Join(",", impacts.Select(i => i.actor)), impacts.Any(i => i.actor == 901) && impacts.Any(i => i.actor == 902));
                // 판정은 현행 적을 그대로 사용하고, 실제 소환 정책만 이 fixture의 적에게 건다.
                Summons.Register(enemy, uchi.ActivePassiveCodes.OfType<UchiTaoist>().Single());
                var mirrored = Enumerable.Range(0, 3).Select(_ => Summons.Create(enemy)).ToArray();
                yield return new WaitForSeconds(.65f);
                SmSwarmFormation(enemy, "enemy"); yield return CaptureStill("enemy_swarm_three");
                foreach (var summon in mirrored) summon.Die(null); Summons.Unregister(enemy); yield return null;
                orion.Die(enemy); yield return null;
                RecordMechanic("sm_sirius_down", "오리온 실제 사망→시리우스DOWN", sirius == null ? "removed" : sirius.Pose.ToString(), sirius != null && sirius.Pose == SdFrameRig.Pose.DOWN);
                RecordMechanic("sm_trace", "실제 경로 모션 trace", string.Join("\n", trace.Select(t => $"{t.kind}|{t.phase}|{t.time:R}|{t.frame}|{t.pose}")), true);
                grid.OnRoundEnd(); yield return null;
                RecordMechanic("sm_cleanup", "라운드끝 소환수·표시 수명 정리", grid.summonList.Count.ToString(), grid.summonList.Count == 0 && !FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Any());
            }
            finally
            {
                StageSummonVisual.MotionObserved -= Observe; Unit.AnyDamageIncoming -= Damage;
                game.ActionScheduler.EndRound(); game.enabled = enabled; Time.captureDeltaTime = previous; Time.timeScale = 1;
            }
        }

        // 80/22/124가 포함된 5인 편성. 전열 3명/후열 2명으로 세 표시 위치를 왕복한다.
        private IEnumerator ScenarioSingleSummonFrontV11(VerifyScenarioRequest config)
        {
            var game = GameManager.Instance;
            bool enabled = game.enabled;
            float prior = Time.captureDeltaTime, speed = Time.timeScale;
            try
            {
                Time.captureDeltaTime = 1f / 60f; Time.timeScale = config.timeScale > 0 ? config.timeScale : 1f;
                yield return S8Prepare(config, startRound: true);
                var grid = GridManager.Instance;
                var owners = new[] { 80, 22, 124 }.Select(id => AliveAllies().Single(u => u.ID == id)).ToArray();
                var thoth = owners[2];
                yield return NordAction(thoth, Kind.Ultimate, a => thoth.FillUltimateResource());
                yield return new WaitForSeconds(.7f);
                var targets = AliveAllies().Select(u => u.currentCell).ToArray();
                var front = targets.Where(c => c.xPos == -1).OrderBy(c => c.yPos).ToArray();
                var rear = targets.Where(c => c.xPos == -2).OrderBy(c => c.yPos).ToArray();
                RecordMechanic("sm_v11_fixture", "전열3·후열2·로키/오리온/토트", $"front={front.Length}, rear={rear.Length}", front.Length == 3 && rear.Length == 2);
                if (front.Length != 3 || rear.Length != 2) yield break;
                foreach (var initial in owners)
                    SmSingleFrontCheck(FindObjectsByType<StageSummonVisual>().Single(v => v.Owner == initial), "initial_permutation");
                yield return CaptureStill($"v11_permutation_{string.Join("_", config.vfxPartyIds.Take(3))}_{config.timeScale}");
                foreach (var owner in owners)
                {
                    var visual = FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Single(v => v.Owner == owner);
                    var original = owner.currentCell;
                    foreach (var target in new[] { front[1], rear[0], rear[1], original })
                    {
                        if (owner.currentCell != target) grid.RelocateUnit(owner, target);
                        yield return grid.WaitForFieldLayoutSettled();
                        yield return new WaitForSeconds(.65f); yield return EndOfFrame;
                        SmSingleFrontCheck(visual, $"{target.xPos}_{target.yPos}");
                        yield return CaptureStill($"v11_front_{string.Join("_", config.vfxPartyIds.Take(3))}_{config.timeScale}_{visual.Kind}_{target.xPos}_{target.yPos}");
                    }
                    var enemy = AliveEnemies().First();
                    owner.TakeDamage(new DamageContext(enemy, 1, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }));
                    int samples = 0, copied = 0;
                    float until = Time.time + .45f;
                    while (Time.time < until)
                    {
                        yield return EndOfFrame;
                        if (owner.currentCell.Card.StageProxy.Frames.CurrentPose != SdFrameRig.Pose.HIT) continue;
                        samples++; if (visual.Pose == SdFrameRig.Pose.HIT) copied++;
                    }
                    RecordMechanic("sm_v11_hit_" + visual.Kind, "주인 실제HIT 동안 소환수 HIT 복제0", $"samples={samples}, copied={copied}", samples > 0 && copied == 0);
                }
                var scribe = Summons.Of(thoth).Single();
                var sv = scribe.GetComponent<SummonCardView>();
                var v = sv.StageVisual;
                Vector3 center = v.Feet + Vector3.up * (v.Height * .5f);
                var track = sv.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "SummonHealthTrack");
                float pixel = StageLayout.WorldHeight / 1080f * v.Depth;
                RecordMechanic("sm_v11_scribe_target", "대상 접점/작은체력판이 이동한 서기관을 추적·기존 대상 후보 유지",
                    $"target={Effects.Vfx.VfxSpace.WorldPoint(scribe)}, center={center}, bar={track.transform.position}",
                    Vector3.Distance(Effects.Vfx.VfxSpace.WorldPoint(scribe), center) < .001f &&
                    Vector3.Distance(track.transform.position, v.Feet + Vector3.up * (v.Height + 8f * pixel)) < .001f &&
                    CombatTargets.AliveEnemies(AliveEnemies().First()).Contains(scribe));
                scribe.TakeDamage(new DamageContext(AliveEnemies().First(), 7, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.TrueDamage }));
                yield return EndOfFrame;
                RecordMechanic("sm_v11_scribe_hit_independent", "서기관 실제HIT·주인IDLE·작은 체력판 갱신",
                    $"summon={v.Pose}, owner={thoth.currentCell.Card.StageProxy.Frames.CurrentPose}, health={sv.DisplayedHealthRatio:R}",
                    v.Pose == SdFrameRig.Pose.HIT && thoth.currentCell.Card.StageProxy.Frames.CurrentPose == SdFrameRig.Pose.IDLE &&
                    Mathf.Abs(sv.DisplayedHealthRatio - scribe.HpCurr / (float)scribe.HpMax) < .0001f);
            }
            finally
            {
                game.ActionScheduler.EndRound(); game.enabled = enabled;
                Time.captureDeltaTime = prior; Time.timeScale = speed;
            }
        }

        private Vector3 SmSingleFrontHome(StageSummonVisual v, bool lowered = false)
        {
            var camera = Camera.main;
            int facing = v.Owner.IsEnemy ? -1 : 1;
            Vector3 feet = camera.WorldToScreenPoint(v.Owner.currentCell.Card.StageProxy.transform.position);
            float ds = v.Depth * camera.pixelHeight / 1080f;
            float body = v.Owner.currentCell.Card.StageProxy.BodyWorldHeight * camera.pixelHeight / StageLayout.WorldHeight;
            float up = lowered ? -body * .35f : 10f * ds;
            if (!lowered && v.CompanionScale < 1f) up = body * .5f - v.Height * camera.pixelHeight / StageLayout.WorldHeight;
            up = Mathf.Min(up, body * .5f - v.Height * camera.pixelHeight / StageLayout.WorldHeight);
            Vector3 point = feet + new Vector3(facing * 150f * ds, up, 0f);
            float width = v.PlacementBounds.size.x * camera.pixelHeight / StageLayout.WorldHeight;
            float offset = camera.WorldToScreenPoint(v.PlacementBounds.center).x - camera.WorldToScreenPoint(v.Feet).x;
            float lo = camera.pixelRect.xMin + width * .5f + (facing > 0 ? 140 : 24) - offset;
            float hi = camera.pixelRect.xMax - width * .5f - (facing < 0 ? 140 : 24) - offset;
            float middle = camera.pixelRect.center.x;
            if (facing > 0) hi = Mathf.Min(hi, middle - width * .5f - offset);
            else lo = Mathf.Max(lo, middle + width * .5f - offset);
            point.x = Mathf.Clamp(point.x, lo, hi);
            return camera.ScreenToWorldPoint(point);
        }

        private void SmSingleFrontCheck(StageSummonVisual v, string tag)
        {
            var figure = v.Owner.currentCell.Card.StageProxy;
            float pixel = StageLayout.WorldHeight / 1080f;
            if (v.Kind == "fenrir")
            {
                Vector3 home = figure.transform.position + new Vector3(v.Owner.IsEnemy ? 70f : -70f, 20f, 0f) * pixel * v.Depth;
                RecordMechanic("sm_v12_fenrir_home", "Root 허용 범위 뒤70·위20·.62, 로키 앞+1", $"tag={tag}, expected={home}, feet={v.Feet}, scale={v.Height / figure.BodyWorldHeight:R}",
                    !v.IsFrontCompanion && Vector3.Distance(home, v.Feet) / pixel <= 1f &&
                    Mathf.Abs(v.Height / figure.BodyWorldHeight - .62f) < .0001f &&
                    v.Renderer.sortingLayerID == figure.Lower.sortingLayerID && v.Renderer.sortingOrder == figure.Lower.sortingOrder + 1);
                SmOcclusion(v, tag);
                return;
            }
            float scale = v.Kind == "sirius" ? .60f : .50f;
            var expected = SmSingleFrontHome(v) + Vector3.right * v.RearClearanceX;
            RecordMechanic("sm_v11_front_" + v.Kind, "앞150·위10/충돌 시 위 칸만 아래0.35몸높이·허리선 제한·주인+1·크기", $"tag={tag}, expected={expected}, feet={v.Feet}, scale={v.CompanionScale:R}",
                v.IsFrontCompanion && Mathf.Min(Vector3.Distance(expected, v.Feet), Vector3.Distance(SmSingleFrontHome(v, true) + Vector3.right * v.RearClearanceX, v.Feet)) / pixel <= 1f &&
                Mathf.Abs(v.RearClearanceX) < figure.BodyWorldHeight &&
                (v.Feet.x - figure.transform.position.x) * (v.Owner.IsEnemy ? -1 : 1) > 0 &&
                (Mathf.Abs(v.CompanionScale - 1f) < .0001f || Mathf.Abs(v.CompanionScale - .9f) < .0001f) &&
                Mathf.Abs(v.Height / figure.BodyWorldHeight - scale * v.CompanionScale) < .0001f &&
                v.Feet.y + v.Height <= figure.transform.position.y + figure.BodyWorldHeight * .5f + .0001f &&
                v.Renderer.sortingLayerID == figure.Lower.sortingLayerID && v.Renderer.sortingOrder == figure.Lower.sortingOrder + 1);
            foreach (var lower in AliveAllies().Concat(AliveEnemies()).Where(u => u != v.Owner && u.IsEnemy == v.Owner.IsEnemy &&
                u.currentCell.xPos == v.Owner.currentCell.xPos && u.currentCell.Card.StageProxy.transform.position.y < figure.transform.position.y))
                RecordMechanic("sm_v11_lower_head_" + v.Kind, "同열 아래 아군의 머리보다 적 쪽", $"tag={tag}, ally={lower.ID}",
                    (v.Feet.x - lower.currentCell.Card.StageProxy.Frames.HeadWorld.x) * (v.Owner.IsEnemy ? -1 : 1) > 0f);
            SmOcclusion(v, tag);
            foreach (var other in FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Where(o => o != v && o.Renderer.enabled && o.Owner.IsEnemy == v.Owner.IsEnemy))
            {
                Bounds body = v.PlacementBounds, otherBody = other.PlacementBounds;
                float dx = Mathf.Abs(body.center.x - otherBody.center.x);
                float dy = Mathf.Abs(v.Feet.y + v.Height * .5f - other.Feet.y - other.Height * .5f);
                RecordMechanic("sm_v11_pair_" + v.Kind + "_" + other.Kind, "다른 주인 포함 소환수 밀집0(alpha 몸 경계)", $"tag={tag}, dx={dx/pixel:R}, dy={dy/pixel:R}, minX={.6f * Mathf.Max(body.size.x, otherBody.size.x)/pixel:R}, minY={.5f * Mathf.Max(v.Height, other.Height)/pixel:R}",
                    dx + .00001f >= .6f * Mathf.Max(body.size.x, otherBody.size.x) ||
                    dy + .00001f >= .5f * Mathf.Max(v.Height, other.Height));
            }
        }

        private IEnumerator ScenarioSummonStageAuthors(VerifyScenarioRequest config)
        {
            bool enabled = GameManager.Instance.enabled;
            float previous = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            yield return S8Prepare(config, startRound: true);
            var game = GameManager.Instance; var grid = GridManager.Instance;
            var isis = AliveAllies().Single(u => u.ID == 125);
            var thoth = AliveAllies().Single(u => u.ID == 124);
            var enemy = AliveEnemies().First();
            var observations = new HashSet<string>();
            var impacts = new List<string>();
            bool correctImpact = true;
            void Observe(StageSummonVisual v, string phase)
            {
                if (v.Owner != isis && v.Owner != thoth) return;
                // 생성 직후 slot 알림은 프레임 배율/펼침 적용 전이다. 실제 보이는 선택 렌더러만 촬영한다.
                if (!v.Renderer.enabled || v.Renderer.color.a < .8f || Mathf.Abs(v.Renderer.transform.lossyScale.y) < .0001f) return;
                observations.Add(v.Kind + "_" + v.Pose);
                SdRenderSelected(v.Renderer, v.Kind, v.Pose, "summon_frames");
            }
            void Impact(Unit target, DamageContext context)
            {
                var actor = game.ActionScheduler.ExecutingUnit;
                if (actor == null || actor.SummonOwner != isis || context.Attacker != isis) return;
                var visual = actor.GetComponent<SummonCardView>().StageVisual;
                correctImpact &= visual.Pose == SdFrameRig.Pose.STRIKE;
                impacts.Add($"{actor.SummonKind}|{Time.time:R}|{game.ActionScheduler.CurrentActionId}|{visual.Pose}");
            }
            StageSummonVisual.MotionObserved += Observe; Unit.AnyDamageIncoming += Impact;
            Time.timeScale = config.timeScale > 0 ? config.timeScale : 1;
            try
            {
                yield return new WaitForSeconds(.65f);
                var guardians = Summons.Of(isis).OrderBy(s => s.ID).ToArray();
                RecordMechanic("sm_isis_three", "매듭/눈/날개3기·각각 전용5프레임·피격불가·체력판없음", string.Join(",", guardians.Select(s => s.ID)),
                    guardians.Length == 3 && guardians.All(s => s.IsUntargetable && !s.GetComponent<SummonCardView>().HasHealthDisplay && s.GetComponent<SummonCardView>().StageVisual != null));
                foreach (var s in guardians)
                {
                    var v = s.GetComponent<SummonCardView>().StageVisual;
                    Observe(v, "idle");
                    int i = s.ID - 904;
                    float px = StageLayout.WorldHeight / 1080f, ds = v.Depth;
                    Vector3 expected = isis.currentCell.Card.StageProxy.transform.position +
                        new Vector3(new[] {105f,-170f,-255f}[i], new[] {26f,56f,8f}[i], 0) * px * ds;
                    expected = SmExpectedHome(v,i);
                    RecordMechanic("sm_isis_slot_" + i, "주인깊이×둘레슬롯·가장자리 제한/대체·30px부유·.45·아군SD위", $"errorPx={Vector3.Distance(expected,v.Feet)/px:R}, scale={v.Height/isis.currentCell.Card.StageProxy.BodyWorldHeight:R}",
                        Vector3.Distance(expected,v.Feet)/px <= 1 && Mathf.Abs(v.Height/isis.currentCell.Card.StageProxy.BodyWorldHeight-.45f)<.0001f &&
                        SmSpiritSorting(v) && v.Renderer.sortingOrder == StageSummonVisual.SpiritSortingOrder+i);
                    SmOcclusion(v, "isis");
                    SmRenderUnused(v, SdFrameRig.Pose.HIT);
                }
                yield return CaptureStill("isis_three_guards");
                RecordMechanic("sm_isis_badge","프로필 배지: 수호령 ×3",SmBadge(isis),SmBadge(isis)=="수호령 ×3");
                foreach (var s in guardians)
                {
                    yield return SmPausedAction(s.SummonKind, s, Kind.Normal, a => a.AdvanceAction(s, 1), s.SummonKind);
                    RecordMechanic("sm_isis_sequence_" + s.ID, "실제 일반 WINDUP→타격STRIKE→IDLE", string.Join(",", observations.Where(x => x.StartsWith(s.SummonKind))),
                        new[] {"WINDUP","STRIKE","IDLE"}.All(p => observations.Contains(s.SummonKind+"_"+p)));
                }
                RecordMechanic("sm_isis_impact", "수호령3종 실제 타격 순간 STRIKE·타격시각 변경없음", string.Join("\n", impacts), impacts.Count >= 3 && correctImpact);
                yield return NordAction(thoth, Kind.Ultimate, a => thoth.FillUltimateResource());
                var scribe = Summons.Of(thoth).Single();
                yield return new WaitForSeconds(.65f); yield return EndOfFrame;
                var view = scribe.GetComponent<SummonCardView>(); var visual = view.StageVisual;
                Observe(visual, "idle");
                float pixel = StageLayout.WorldHeight / 1080f;
                Vector3 home = SmSingleFrontHome(visual);
                RecordMechanic("sm_scribe_companion", "서기관은 앞128/아래12·.50·앞정렬·피격가능/대상/작은체력판", $"errorPx={Vector3.Distance(home,visual.Feet)/pixel:R}, hp={view.DisplayedHealthRatio:R}",
                    !scribe.IsUntargetable && CombatTargets.AliveEnemies(enemy).Contains(scribe) && view.HasHealthDisplay && Mathf.Abs(view.DisplayedHealthRatio-1)<.0001f &&
                    Vector3.Distance(home,visual.Feet)/pixel<=1 && Mathf.Abs(visual.Height/thoth.currentCell.Card.StageProxy.BodyWorldHeight-.50f)<.0001f &&
                    visual.Renderer.sortingOrder == thoth.currentCell.Card.PortraitRenderer.sortingOrder+1);
                SmOcclusion(visual, "scribe");
                RecordMechanic("sm_scribe_badge","프로필 배지: 서기관",SmBadge(thoth),SmBadge(thoth)=="서기관");
                yield return CaptureStill("thoth_scribe");
                yield return SmPausedAction("scribe", scribe, Kind.Normal, a => a.AdvanceAction(scribe,1), scribe.SummonKind);
                RecordMechanic("sm_scribe_sequence", "실제 치유 일반행동 WINDUP→STRIKE→IDLE", string.Join(",", observations.Where(x=>x.StartsWith(scribe.SummonKind))),
                    new[] {"WINDUP","STRIKE","IDLE"}.All(p=>observations.Contains(scribe.SummonKind+"_"+p)));
                scribe.TakeDamage(new DamageContext(enemy,7,BaseEnums.CodeType.Normal,new List<int>{DamageTag.TrueDamage}));
                yield return EndOfFrame;
                RecordMechanic("sm_scribe_hit_health", "서기관 실제피격 HIT·체력비율 갱신", $"pose={visual.Pose}, health={view.DisplayedHealthRatio:R}, actual={scribe.HpCurr}/{scribe.HpMax}",
                    visual.Pose == SdFrameRig.Pose.HIT && Mathf.Abs(view.DisplayedHealthRatio-scribe.HpCurr/(float)scribe.HpMax)<.0001f);
                yield return new WaitForSeconds(.45f);
                scribe.Die(enemy); yield return EndOfFrame;
                RecordMechanic("sm_scribe_down", "서기관 실제사망 DOWN·작은체력판 정리", visual == null ? "removed" : visual.Pose.ToString(), observations.Contains("thoth_scribe_DOWN"));
                isis.Die(enemy); yield return EndOfFrame;
                for (int i = 0; i < guardians.Length; i++)
                    RecordMechanic("sm_isis_owner_death_"+(904+i), "주인사망으로 수호령 실제사망 DOWN", guardians[i].isActive.ToString(),
                        !guardians[i].isActive && observations.Contains("isis_guardian_"+i+"_DOWN"));
                yield return new WaitForSeconds(.65f);
                // 현행 적의 판정/데이터는 유지하고 실제 수호단 정책과 표시 fixture만 연결한다.
                var enemyIsis = enemy;
                var enemyPolicy = new IsisThreefoldGuard(new PassiveCodeContext { Caster = enemyIsis });
                enemyPolicy.CastCode();
                grid.RefreshFieldLayout(); yield return grid.WaitForFieldLayoutSettled();
                enemyIsis.currentCell.Card.StageProxy.Configure(Helpers.SpriteResource.LoadStanding("ISIS_STANDING"),enemyIsis,enemyIsis.currentCell.Card.transform);
                yield return new WaitForSeconds(.65f);
                foreach (var guardian in Summons.Of(enemyIsis))
                {
                    var v = guardian.GetComponent<SummonCardView>().StageVisual;
                    int index = guardian.ID-904;
                    Vector3 expected = enemyIsis.currentCell.Card.StageProxy.transform.position +
                        new Vector3(-new[]{105f,-170f,-255f}[index],new[]{26f,56f,8f}[index],0)*pixel*v.Depth;
                    expected = SmExpectedHome(v,index);
                    RecordMechanic("sm_enemy_isis_"+index,"적 수호령 좌우반전·둘레슬롯·앞정렬", $"errorPx={Vector3.Distance(expected,v.Feet)/pixel:R}, flip={v.Renderer.flipX}",
                        v.Renderer.flipX && Vector3.Distance(expected,v.Feet)/pixel<=1 && SmSpiritSorting(v) && v.Renderer.sortingOrder==StageSummonVisual.SpiritSortingOrder+index);
                    SmOcclusion(v,"enemy_isis");
                }
                yield return CaptureStill("enemy_isis_three");
                enemyPolicy.StopCode();
                grid.OnRoundEnd(); yield return null;
                RecordMechanic("sm_authors_cleanup", "라운드끝 소환/표시/체력판 수명정리", grid.summonList.Count.ToString(), grid.summonList.Count == 0 && !FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Any());
            }
            finally
            {
                StageSummonVisual.MotionObserved -= Observe; Unit.AnyDamageIncoming -= Impact;
                game.ActionScheduler.EndRound(); game.enabled = enabled; Time.captureDeltaTime = previous; Time.timeScale = 1;
            }
        }

        private IEnumerator ScenarioSummonStageSpeed(VerifyScenarioRequest config)
        {
            bool enabled = GameManager.Instance.enabled;
            float priorDelta = Time.captureDeltaTime, priorScale = Time.timeScale;
            Time.captureDeltaTime = 1f / 60f;
            yield return S8Prepare(config);
            var game = GameManager.Instance; var grid = GridManager.Instance;
            var orion = AliveAllies().Single(u => u.ID == 22);
            var loki = AliveAllies().Single(u => u.ID == 80);
            var uchi = AliveAllies().Single(u => u.ID == 180);
            var enemy = AliveEnemies().First();
            var rig = orion.currentCell.Card.StageProxy.Frames;
            var sirius = StageSummonVisual.Sirius(orion);
            bool rain = false, sampling = true;
            int samples = 0, mismatches = 0;
            var seen = new HashSet<string>();
            var sync = new List<string>();
            void Start(Unit actor, Kind kind, string label) { if (actor == orion) rain = label == "시리우스 · 화살비"; }
            void Finish(Unit actor, Kind kind) { if (actor == orion) rain = false; }
            ActionScheduler.AnyActionStarted += Start; ActionScheduler.AnyActionFinished += Finish;
            IEnumerator Sample()
            {
                while (sampling)
                {
                    yield return EndOfFrame;
                    if (sirius == null || rig == null) continue;
                    var expected = rig.CurrentPose == SdFrameRig.Pose.DOWN || (rain && rig.CurrentPose is SdFrameRig.Pose.WINDUP or SdFrameRig.Pose.STRIKE) ? rig.CurrentPose : SdFrameRig.Pose.IDLE;
                    samples++; if (expected != sirius.Pose) mismatches++;
                    seen.Add((rain ? "rain_" : "other_") + rig.CurrentPose);
                    sync.Add($"{Time.frameCount}|{Time.time:R}|rain={rain}|owner={rig.CurrentPose}|sirius={sirius.Pose}|expected={expected}");
                }
            }
            var sampler = StartCoroutine(Sample());
            Time.timeScale = config.timeScale > 0 ? config.timeScale : 5;
            try
            {
                yield return new WaitForSeconds(.6f);
                var clone = Summons.Create(uchi); yield return new WaitForSeconds(.4f);
                yield return SmPausedAction("clone", clone, Kind.Normal, s => s.AdvanceAction(clone, 1), "uchi_clone");
                var fenrir = Summons.Of(loki).Single();
                yield return SmPausedAction("fenrir", fenrir, Kind.Normal, s => s.AdvanceAction(fenrir, 1), "fenrir");
                Summons.Create(uchi); yield return new WaitForSeconds(.4f);
                uchi.TakeDamage(new DamageContext(enemy, uchi.HpMax, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.NormalAttack, DamageTag.TrueDamage }));
                yield return EndOfFrame;
                yield return SmPauseSnapshot("guard");
                yield return new WaitForSeconds(.6f);
                yield return NordAction(orion, Kind.Normal, s => s.AdvanceAction(orion, 1));
                yield return NordAction(orion, Kind.Ultimate, s => orion.FillUltimateResource());
                orion.TakeDamage(new DamageContext(enemy, 1, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.NormalAttack, DamageTag.TrueDamage }));
                yield return EndOfFrame;
                yield return SmPauseSnapshot("sirius_hit");
                yield return new WaitForSeconds(.55f);
                yield return SmPausedAction("sirius_rain", orion, Kind.Additional,
                    s => orion.ActivePassiveCodes.OfType<OrionSirius>().Single().Gain(5), "sirius");
                yield return new WaitForSeconds(.65f);
                orion.Die(enemy); yield return EndOfFrame; yield return null;
                sampling = false; yield return sampler;
                RecordMechanic("sm_sirius_sync_" + Time.timeScale, "매프레임: DOWN·화살비 WINDUP/STRIKE만 동일·주인HIT 포함 기타IDLE", $"samples={samples}, mismatches={mismatches}, seen={string.Join(",", seen)}", samples > 0 && mismatches == 0 &&
                    new[] { "other_IDLE", "other_WINDUP", "other_STRIKE", "other_HIT", "other_DOWN", "rain_WINDUP", "rain_STRIKE" }.All(seen.Contains));
                RecordMechanic("sm_sirius_sync_trace", "실제 포즈 동기화 전표본", string.Join("\n", sync), true);
                yield return CaptureStill("speed_pause_regression");
                grid.OnRoundEnd(); yield return null;
                RecordMechanic("sm_speed_cleanup", "5배 검사후 소환·표시 정리", grid.summonList.Count.ToString(), grid.summonList.Count == 0 && !FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None).Any());
            }
            finally
            {
                sampling = false; StopCoroutine(sampler);
                ActionScheduler.AnyActionStarted -= Start; ActionScheduler.AnyActionFinished -= Finish;
                game.ActionScheduler.EndRound(); game.enabled = enabled; Time.captureDeltaTime = priorDelta; Time.timeScale = priorScale;
            }
        }

        private IEnumerator SmPausedAction(string tag, Unit actor, Kind kind, Action<ActionScheduler> setup, string visualKind)
        {
            bool windup = false, complete = false;
            void Observe(StageSummonVisual v, string phase)
            {
                if (v.Kind == visualKind && (phase == "windup" || phase == "rain_windup")) windup = true;
            }
            StageSummonVisual.MotionObserved += Observe;
            IEnumerator Action() { yield return NordAction(actor, kind, setup); complete = true; }
            var action = StartCoroutine(Action());
            try
            {
                float deadline = Time.realtimeSinceStartup + 30;
                while (!windup && !complete && Time.realtimeSinceStartup < deadline) yield return null;
                RecordMechanic("sm_pause_coverage_" + tag, "실제 표시 WINDUP 관측", windup.ToString(), windup);
                if (windup) { yield return EndOfFrame; yield return SmPauseSnapshot(tag); }
                yield return action;
            }
            finally { StageSummonVisual.MotionObserved -= Observe; }
        }

        private IEnumerator SmPauseSnapshot(string tag)
        {
            float speed = Time.timeScale;
            Time.timeScale = 0;
            yield return EndOfFrame;
            var visuals = FindObjectsByType<StageSummonVisual>(FindObjectsSortMode.None);
            var states = visuals.Select(v => (v.Feet, v.Pose, v.Renderer.sprite, v.Renderer.color, v.Renderer.transform.localScale)).ToArray();
            float time = Time.time;
            int actions = GameManager.Instance.ActionScheduler.ActionCount;
            yield return WaitUnscaled(.30f); yield return EndOfFrame;
            bool same = visuals.Length > 0 && Mathf.Abs(time - Time.time) < .0001f && actions == GameManager.Instance.ActionScheduler.ActionCount;
            for (int i = 0; i < visuals.Length; i++)
                same &= visuals[i] != null && states[i] == (visuals[i].Feet, visuals[i].Pose, visuals[i].Renderer.sprite, visuals[i].Renderer.color, visuals[i].Renderer.transform.localScale);
            RecordMechanic("sm_pause_" + tag + "_" + speed, "0.30초 일시정지: 표시좌표·포즈·프레임·색·배율·시간·행동수 불변", $"visuals={visuals.Length}, same={same}", same);
            Time.timeScale = speed;
        }

        // PM 승인 표시 fixture. 실제 선택 렌더러의 프레임만 잠시 교체하고 즉시 복원한다.
        // 행동/피격/사망의 증거와 분리하며 Unit·모션 포즈·콜백에는 관여하지 않는다.
        private void SmRenderUnused(StageSummonVisual visual, SdFrameRig.Pose pose)
        {
            var renderer = visual.Renderer;
            Sprite original = renderer.sprite;
            var data = DataManager.LoadStageFigureDataList().figures.Single(p => p.sprite == visual.Kind).sd;
            try
            {
                renderer.sprite = Resources.Load<Sprite>($"Sprite/SD/{data.prefix}_{pose}");
                SdRenderSelected(renderer, visual.Kind, pose, "summon_unused_display");
                RecordMechanic("sm_unused_display_" + visual.Kind + "_" + pose, "표시 확인(전투 미사용)·실제 선택 렌더러 프레임", renderer.sprite?.name,
                    renderer.sprite != null && renderer.sprite.name == data.prefix + "_" + pose);
            }
            finally { renderer.sprite = original; }
        }

        private IEnumerator SmShots(string tag, Action action, float seconds)
        {
            action(); float start = Time.time;
            foreach (float at in new[] { .03f, .13f, .28f, .48f })
            {
                while (Time.time - start < at) yield return null;
                yield return CaptureStill(tag);
            }
            while (Time.time - start < seconds) yield return null;
        }
        private IEnumerator SmActionShots(string tag, Unit actor)
        {
            bool started = false;
            void Begin(Unit u, Kind k, string label) { if (u == actor) started = true; }
            ActionScheduler.AnyActionStarted += Begin;
            try
            {
                float deadline = Time.realtimeSinceStartup + 20;
                while (!started && Time.realtimeSinceStartup < deadline) yield return null;
                if (started) yield return SmShots(tag, () => { }, .65f);
            }
            finally { ActionScheduler.AnyActionStarted -= Begin; }
        }

        private void SmSwarmFormation(Unit owner, string tag)
        {
            float[] front = { 100, -165, -250 }, up = { -6, 28, -24 };
            var summons = Summons.Of(owner);
            for (int i = 0; i < summons.Count; i++)
            {
                var visual = summons[i].GetComponent<SummonCardView>().StageVisual;
                float px = StageLayout.WorldHeight / 1080f, ds = owner.currentCell.transform.lossyScale.y;
                var view = owner.currentCell.Card;
                Vector3 expected = view.StageProxy.transform.position + new Vector3((owner.IsEnemy ? -1 : 1)*front[i], up[i], 0)*px*ds;
                expected = SmExpectedHome(visual,i);
                float error = Vector3.Distance(expected, visual.Feet)/px;
                RecordMechanic($"sm_swarm_{tag}_{i}", "오래된 순서의 앞 슬롯·주인+1+i·.55·진영 반전",
                    $"errorPx={error:R}, order={visual.Renderer.sortingOrder}/{view.PortraitRenderer.sortingOrder}, scale={visual.Height/view.StageProxy.BodyWorldHeight:R}, flip={visual.Renderer.flipX}",
                    visual.IsSwarm && error <= 1 && visual.Renderer.sortingOrder == view.PortraitRenderer.sortingOrder+1+i &&
                    Mathf.Abs(visual.Height/view.StageProxy.BodyWorldHeight-.55f)<.0001f && visual.Renderer.flipX == owner.IsEnemy);
                SmOcclusion(visual, tag+"_"+i);
            }
        }

        private static string SmBadge(Unit owner) => owner.currentCell.Card.GetComponentsInChildren<TextMeshPro>(true).Single(t=>t.name=="StageSummonBadge").text;

        private bool SmSpiritSorting(StageSummonVisual visual)
        {
            if (!visual.Kind.StartsWith("isis_guardian_")) return true;
            int layer = SortingLayer.GetLayerValueFromID(visual.Renderer.sortingLayerID);
            return AliveAllies().Concat(AliveEnemies()).Where(u=>u.IsEnemy==visual.Owner.IsEnemy && u.currentCell!=null).All(u=>
            {
                var r=u.currentCell.Card.PortraitRenderer;
                int other=SortingLayer.GetLayerValueFromID(r.sortingLayerID);
                return layer>other || layer==other && visual.Renderer.sortingOrder>r.sortingOrder;
            }) && layer < SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("CombatVfx"));
        }

        // 실제 표시 박스/카메라 픽셀에서 독립 계산한다. 명목 슬롯 상수는 기존 기대값 그대로다.
        private Vector3 SmExpectedHome(StageSummonVisual visual,int index)
        {
            var camera=Camera.main;var owner=visual.Owner;
            var siblings=Summons.Of(owner).Select(s=>s.GetComponent<SummonCardView>().StageVisual).ToArray();
              var homes=new Vector3[siblings.Length];var widths=new float[siblings.Length];var heights=new float[siblings.Length];var centers=new Vector2[siblings.Length];
            var foot=camera.WorldToScreenPoint(owner.currentCell.Card.StageProxy.transform.position);
            float ds=visual.Depth*camera.pixelHeight/1080f;int facing=owner.IsEnemy?-1:1;
            for(int i=0;i<siblings.Length;i++)
            {
                var v=siblings[i];bool spirit=v.Kind.StartsWith("isis_guardian_");
                var min=camera.WorldToScreenPoint(v.Renderer.bounds.min);var max=camera.WorldToScreenPoint(v.Renderer.bounds.max);
                widths[i]=max.x-min.x;
                  heights[i]=v.Height*camera.pixelHeight/StageLayout.WorldHeight;
                float offset=camera.WorldToScreenPoint(v.Renderer.bounds.center).x-camera.WorldToScreenPoint(v.Feet).x;
                  centers[i]=new Vector2(offset,heights[i]*.5f);
                Vector3 Clamp(Vector3 p)
                {
                      float lo=widths[i]*.5f+(facing>0?140:24)-offset,hi=Screen.width-widths[i]*.5f-(facing<0?140:24)-offset;
                    if(facing>0)hi=Mathf.Min(hi,Screen.width*.5f-widths[i]*.5f-offset);
                    else lo=Mathf.Max(lo,Screen.width*.5f+widths[i]*.5f-offset);
                    p.x=Mathf.Clamp(p.x,lo,hi);return p;
                }
                float dx=(spirit?new[]{105,-170,-255}:new[]{100,-165,-250})[i];
                float dy=(spirit?new[]{26,56,8}:new[]{-6,28,-24})[i];
                var nominal=foot+new Vector3(facing*dx,dy,0)*ds;
                  homes[i]=Clamp(nominal);bool pulled=Mathf.Abs(homes[i].x-nominal.x)>.0001f;
                  bool Crowded(Vector3 p)
                  {
                      for(int j=0;j<i;j++)
                      {
                          Vector2 d=(Vector2)(p-homes[j])+centers[i]-centers[j];
                          if(Mathf.Abs(d.x)<.6f*Mathf.Max(widths[i],widths[j])&&Mathf.Abs(d.y)<.5f*Mathf.Max(heights[i],heights[j]))return true;
                      }
                      return false;
                  }
                  bool horizontalClear=true;
                  for(int j=0;j<i;j++)horizontalClear&=Mathf.Abs(homes[i].x+centers[i].x-homes[j].x-centers[j].x)>=.6f*Mathf.Max(widths[i],widths[j]);
                  if((pulled&&!horizontalClear)||Crowded(homes[i]))
                  {
                        var a=foot+new Vector3(facing*215,spirit?54:24,0)*ds;
                        var resolved=Clamp(a);bool blocked=Mathf.Abs(resolved.x-a.x)>.0001f||Crowded(resolved);
                        homes[i]=blocked?Clamp(foot+new Vector3(facing*215,spirit?-10:-40,0)*ds):resolved;
                  }
            }
            return camera.ScreenToWorldPoint(homes[index]);
        }

        private IEnumerator ScenarioSummonLayoutFix(VerifyScenarioRequest config)
        {
            bool enabled=GameManager.Instance.enabled;float prior=Time.captureDeltaTime;
            Time.captureDeltaTime=1f/60f;
            yield return S8Prepare(config,startRound:true);
            var grid=GridManager.Instance;var isis=AliveAllies().Single(u=>u.ID==125);var uchi=AliveAllies().Single(u=>u.ID==180);
            try
            {
                bool clone=config.trigger.Contains("uchi");var owner=clone?uchi:isis;
                int column=config.trigger.Contains("center")?-1:-2;
                int row=config.trigger.Contains("center")?2:config.trigger.Contains("near")?3:1;
                var target=AliveAllies().First(u=>u.currentCell.xPos==column&&u.currentCell.yPos==row).currentCell;
                if(owner.currentCell!=target)grid.RelocateUnit(owner,target);
                if(clone)for(int i=0;i<3;i++)Summons.Create(uchi);
                yield return grid.WaitForFieldLayoutSettled();yield return new WaitForSeconds(.7f);yield return EndOfFrame;
                RecordMechanic("sm_layout_fixture","실제 편성/재배치 API의 지정 위치",$"{owner.ID}:{owner.currentCell.xPos},{owner.currentCell.yPos}",owner.currentCell.xPos==column&&owner.currentCell.yPos==row);
                foreach(var s in Summons.Of(owner))
                {
                    var v=s.GetComponent<SummonCardView>().StageVisual;int i=Summons.Of(owner).IndexOf(s);
                    var expected=SmExpectedHome(v,i);float px=StageLayout.WorldHeight/1080f;
                    var a=Camera.main.WorldToScreenPoint(v.Renderer.bounds.min);var b=Camera.main.WorldToScreenPoint(v.Renderer.bounds.max);
                      RecordMechanic("sm_layout_home_"+i,"아군 왼쪽/적 오른쪽 몸박스 140px 대역 회피·두 단계 대체·중앙선 유지",$"errorPx={Vector3.Distance(expected,v.Feet)/px:R}, left={a.x:R}, right={Screen.width-b.x:R}, y={1080-Camera.main.WorldToScreenPoint(v.Feet).y:R}",
                          Vector3.Distance(expected,v.Feet)/px<=1&&a.x>=23.99f&&b.x<=Screen.width-23.99f&&(owner.IsEnemy?Screen.width-b.x:a.x)>139.99f);
                    RecordMechanic("sm_layout_sort_"+i,clone?"분신 주인+1+i 유지":"수호령은 아군SD 전체 위·HUD/숫자 아래",$"{v.Renderer.sortingLayerName}:{v.Renderer.sortingOrder}",clone?v.Renderer.sortingOrder==owner.currentCell.Card.PortraitRenderer.sortingOrder+1+i:SmSpiritSorting(v));
                    SmOcclusion(v,config.trigger);
                }
                RecordMechanic("sm_layout_badge","프로필 배지 문구",SmBadge(owner),SmBadge(owner)==(clone?"분신 ×3":"수호령 ×3"));
                yield return CaptureStill(config.trigger);
                // 현재 카메라에서 왼쪽으로 밀린 표시 원점만 즉시 질의·복원한다. 칸/판정/시계는 건드리지 않는다.
                var transform=owner.currentCell.Card.StageProxy.transform;var original=transform.position;
                try
                {
                    float shift=Camera.main.WorldToScreenPoint(original).x-140f;
                    transform.position+=Vector3.left*(shift*StageLayout.WorldHeight/1080f);
                      var v=Summons.Of(owner)[1].GetComponent<SummonCardView>().StageVisual;
                      var p=v.Home(1);var origin=transform.position;float dx=(p.x-origin.x)/(StageLayout.WorldHeight/1080f*v.Depth);
                    float dy=(p.y-origin.y)/(StageLayout.WorldHeight/1080f*v.Depth);
                      RecordMechanic("sm_layout_fallback_query","140px까지 당겨도 간격 부족이면 A(+215,-24)",$"xOffset={dx:R}, up={dy:R}",Mathf.Abs(dx-215)<.001f&&Mathf.Abs(dy-(clone?24:54))<.001f);
                      if(!clone)
                      {
                          v=Summons.Of(owner)[2].GetComponent<SummonCardView>().StageVisual;
                          p=v.Home(2);dx=(p.x-origin.x)/(StageLayout.WorldHeight/1080f*v.Depth);dy=(p.y-origin.y)/(StageLayout.WorldHeight/1080f*v.Depth);
                          RecordMechanic("sm_layout_second_fallback_query","A 자리가 밀집하면 B(+215,+40)",$"xOffset={dx:R}, up={dy:R}",Mathf.Abs(dx-215)<.001f&&Mathf.Abs(dy+10)<.001f);
                      }
                }
                finally{transform.position=original;}
                grid.OnRoundEnd();yield return null;
            }
            finally{GameManager.Instance.ActionScheduler.EndRound();GameManager.Instance.enabled=enabled;Time.captureDeltaTime=prior;Time.timeScale=1;}
        }

        private void SmOcclusion(StageSummonVisual visual, string tag = "front")
        {
            Camera camera = Camera.main; var owner = visual.Owner.currentCell.Card;
            Vector3 facePoint = camera.WorldToScreenPoint(owner.StageProxy.Frames != null ? owner.StageProxy.Frames.HeadWorld : owner.StageProxy.HeadWorldBounds.center);
            Rect face = new Rect(facePoint.x - 4, facePoint.y - 4, 8, 8), hud = owner.StageHudHitRect(camera);
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool[] enabled = renderers.Select(r => r.enabled).ToArray(), canvasEnabled = canvases.Select(c => c.enabled).ToArray();
            var target = camera.targetTexture; var active = RenderTexture.active; var clear = camera.clearFlags; var background = camera.backgroundColor;
            var output = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var read = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false, true);
            try
            {
                foreach (var r in renderers) r.enabled = false; foreach (var c in canvases) c.enabled = false;
                visual.Renderer.enabled = true; camera.targetTexture = output; camera.clearFlags = CameraClearFlags.SolidColor;
                Color32[] Render(Color color) { camera.backgroundColor = color; camera.Render(); RenderTexture.active = output; read.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); return read.GetPixels32(); }
                Color32[] black = Render(Color.black), white = Render(Color.white);
                int Count(Rect rect)
                {
                    int count = 0;
                    for (int y = Mathf.Max(0, Mathf.CeilToInt(rect.yMin)); y < Mathf.Min(Screen.height, rect.yMax); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(rect.xMin)); x < Mathf.Min(Screen.width, rect.xMax); x++)
                    { int p = y * Screen.width + x; float alpha = 1f - ((white[p].r-black[p].r)+(white[p].g-black[p].g)+(white[p].b-black[p].b))/(3f*255f); if (alpha > .35f) count++; }
                    return count;
                }
                int f = Count(face), h = Count(hud);
                RecordMechanic("sm_overlap_" + visual.Kind, "실제 그림의 주인얼굴·하단HUD 중첩0px", $"tag={tag}, face={f}, hud={h}, rect={face}/{hud}", f == 0 && h == 0);
                int otherFaces = 0;
                float otherFaceArea = 0f, maxOtherFaceRatio = 0f;
                bool otherFacesWithinLimit = true;
                var otherFaceSamples = new List<string>();
                foreach (var unit in AliveAllies().Concat(AliveEnemies()).Where(u => u != visual.Owner && u.IsEnemy == visual.Owner.IsEnemy &&
                    u.currentCell != null && u.currentCell.xPos == visual.Owner.currentCell.xPos))
                {
                    var figure = unit.currentCell.Card.StageProxy;
                    Vector3 point = camera.WorldToScreenPoint(figure.Frames != null ? figure.Frames.HeadWorld : figure.HeadWorldBounds.center);
                    Rect roi = new Rect(point.x - 4, point.y - 4, 8, 8);
                    int covered = Count(roi);
                    float area = roi.width * roi.height;
                    float ratio = area > 0f ? covered / area : float.PositiveInfinity;
                    otherFaces += covered; otherFaceArea += area;
                    maxOtherFaceRatio = Mathf.Max(maxOtherFaceRatio, ratio);
                    otherFacesWithinLimit &= area > 0f && ratio <= .01f;
                    otherFaceSamples.Add($"ally={unit.ID}, pixels={covered}, roiArea={area:R}, ratio={ratio:R}, percent={ratio * 100f:R}%");
                }
                RecordMechanic("sm_other_face_" + visual.Kind, "아군 얼굴 ROI 면적의 1% 이하 alpha 중첩",
                    $"tag={tag}, pixels={otherFaces}, totalRoiArea={otherFaceArea:R}, maxRatio={maxOtherFaceRatio:R}, maxPercent={maxOtherFaceRatio * 100f:R}%|{string.Join("|", otherFaceSamples)}", otherFacesWithinLimit);
                if (visual.IsFrontCompanion || visual.Kind == "fenrir")
                    foreach (var ally in AliveAllies().Concat(AliveEnemies()).Where(u => u.IsEnemy == visual.Owner.IsEnemy && u.currentCell != null))
                    {
                        var figure = ally.currentCell.Card.StageProxy;
                        Vector3 center = camera.WorldToScreenPoint(figure.Frames.HeadWorld);
                        float body = figure.BodyWorldHeight * camera.pixelHeight / StageLayout.WorldHeight;
                        // Face ROI verified against five IDLE sources. HeadBounds includes hats, hair and shoulders.
                        Rect roi = new Rect(center.x - body * .15f, center.y - body * .13f, body * .30f, body * .26f);
                        int covered = Count(roi);
                        float area = roi.width * roi.height;
                        float ratio = area > 0f ? covered / area : float.PositiveInfinity;
                        if (visual.Kind == "fenrir")
                        {
                            var faceRenderer = figure.Frames.Renderer;
                            int petLayer = SortingLayer.GetLayerValueFromID(visual.Renderer.sortingLayerID);
                            int faceLayer = SortingLayer.GetLayerValueFromID(faceRenderer.sortingLayerID);
                            bool drawnAbove = petLayer > faceLayer || petLayer == faceLayer && visual.Renderer.sortingOrder > faceRenderer.sortingOrder;
                            int visibleCover = drawnAbove ? covered : 0;
                            RecordMechanic("sm_v12_fenrir_face_visible_" + ally.ID, "실제 그리기 순서에서 얼굴 ROI 가림0px",
                                $"tag={tag}, geometricPx={covered}, drawnAbove={drawnAbove}, visibleCover={visibleCover}, petOrder={visual.Renderer.sortingOrder}, faceOrder={faceRenderer.sortingOrder}", visibleCover == 0);
                            continue;
                        }
                        RecordMechanic("sm_v11_face_box_" + visual.Kind + "_" + ally.ID, "아군 얼굴 ROI 면적의 1% 이하 alpha 중첩",
                            $"tag={tag}, pixels={covered}, roiArea={area:R}, ratio={ratio:R}, percent={ratio * 100f:R}%", area > 0f && ratio <= .01f);
                    }
                if (visual.Kind == "fenrir")
                {
                    // 몸 bounds의 근접만으로 통과시키지 않는다. 실제 두 그림의 불투명 픽셀을 대조한다.
                    visual.Renderer.enabled = false;
                    var loki = owner.StageProxy.Frames.Renderer;
                    loki.enabled = true;
                    Color32[] lokiBlack = Render(Color.black), lokiWhite = Render(Color.white);
                    loki.enabled = false; visual.Renderer.enabled = true;
                    Vector3 feet = camera.WorldToScreenPoint(owner.StageProxy.transform.position);
                    float body = owner.StageProxy.BodyWorldHeight * camera.pixelHeight / StageLayout.WorldHeight;
                    float left = feet.x - body * (visual.Owner.IsEnemy ? .05f : .6f);
                    Rect lowerBack = new Rect(left, feet.y, body * .65f, body * .5f);
                    bool Visible(Color32[] b, Color32[] w, int p) =>
                        1f - ((w[p].r - b[p].r) + (w[p].g - b[p].g) + (w[p].b - b[p].b)) / (3f * 255f) > .35f;
                    int overlap = 0;
                    for (int y = Mathf.Max(0, Mathf.CeilToInt(lowerBack.yMin)); y < Mathf.Min(Screen.height, lowerBack.yMax); y++)
                        for (int x = Mathf.Max(0, Mathf.CeilToInt(lowerBack.xMin)); x < Mathf.Min(Screen.width, lowerBack.xMax); x++)
                        {
                            int p = y * Screen.width + x;
                            if (Visible(black, white, p) && Visible(lokiBlack, lokiWhite, p)) overlap++;
                        }
                    RecordMechanic("sm_v12_fenrir_loki_body_overlap", "로키 허리 아래·뒤 몸과 실제 alpha 겹침≥8×ds²px", 
                        $"tag={tag}, overlapPx={overlap}, ds={visual.Depth:R}, lowerBack={lowerBack}", overlap >= 8f * visual.Depth * visual.Depth);
                }
                if (visual.IsSurround || visual.IsFrontCompanion)
                {
                      var boxLeft=camera.WorldToScreenPoint(visual.Renderer.bounds.min).x;
                      var boxRight=camera.WorldToScreenPoint(visual.Renderer.bounds.max).x;
                      float outerMargin=visual.Owner.IsEnemy?Screen.width-boxRight:boxLeft;
                      RecordMechanic("sm_panel_band_"+visual.Kind,"다수형 몸박스의 아군 왼쪽/적 오른쪽 140px 대역 회피",$"tag={tag}, outerMargin={outerMargin:R}",outerMargin>139.99f);
                      var siblings=Summons.Of(visual.Owner);int self=siblings.IndexOf(visual.Summon);
                      for(int j=0;j<self;j++)
                      {
                          var other=siblings[j].GetComponent<SummonCardView>().StageVisual;
                          float dx=Mathf.Abs(visual.Renderer.bounds.center.x-other.Renderer.bounds.center.x);
                          float dy=Mathf.Abs(visual.Feet.y+visual.Height*.5f-other.Feet.y-other.Height*.5f);
                          float minX=.6f*Mathf.Max(visual.Renderer.bounds.size.x,other.Renderer.bounds.size.x),minY=.5f*Mathf.Max(visual.Height,other.Height);
                          RecordMechanic("sm_pair_spacing_"+visual.Kind+"_"+j,"몸박스 중심 가로≥몸폭0.6배 또는 세로≥발~top 몸높이0.5배",$"tag={tag}, dx={dx*1080f/StageLayout.WorldHeight:R}, minX={minX*1080f/StageLayout.WorldHeight:R}, dy={dy*1080f/StageLayout.WorldHeight:R}, minY={minY*1080f/StageLayout.WorldHeight:R}",dx+.000001f>=minX||dy+.000001f>=minY);
                      }
                    // 화면 밖 알파도 읽을 수 있게 같은 픽셀 밀도로 사방200px를 더 렌더한다.
                    SmScreenBounds(visual, tag, camera);
                    // 대기 배치만 검사한다. 공격 돌진은 중앙선을 넘어 실제 적 앞까지 이동하는 것이 정상이다.
                    bool Opaque(Color32[] b, Color32[] w, int p) => 1f-((w[p].r-b[p].r)+(w[p].g-b[p].g)+(w[p].b-b[p].b))/(3f*255f) > .35f;
                    visual.Renderer.enabled = false;
                    foreach (var unit in AliveAllies().Concat(AliveEnemies()).Where(u => u.IsEnemy != visual.Owner.IsEnemy && u.currentCell != null))
                    {
                        var figure = unit.currentCell.Card.StageProxy;
                        if (figure == null) continue;
                        foreach (var renderer in figure.GetComponentsInChildren<Renderer>())
                        {
                            int index = Array.IndexOf(renderers, renderer);
                            renderer.enabled = index >= 0 && enabled[index];
                        }
                    }
                    var oppositeBlack = Render(Color.black); var oppositeWhite = Render(Color.white);
                    int middle = 0, overlap = 0, pixels = 0;
                    for (int y = 0; y < Screen.height; y++)
                    for (int x = 0; x < Screen.width; x++)
                    {
                        int p = y*Screen.width+x;
                        if (!Opaque(black, white, p)) continue;
                        pixels++;
                        if (visual.Owner.IsEnemy ? x < Screen.width/2 : x >= Screen.width/2) middle++;
                        if (Opaque(oppositeBlack, oppositeWhite, p)) overlap++;
                    }
                    RecordMechanic("sm_swarm_safe_"+tag, "다수형 대기: 실제 알파 중앙선 침범0px·상대 SD 중첩0px",
                        $"owner={visual.Owner.ID}, enemy={visual.Owner.IsEnemy}, pose={visual.Pose}, pixels={pixels}, acrossCenter={middle}, oppositeOverlap={overlap}",
                        visual.Pose == SdFrameRig.Pose.IDLE && pixels > 0 && middle == 0 && overlap == 0);
                }
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = canvasEnabled[i];
                camera.targetTexture = target; camera.clearFlags = clear; camera.backgroundColor = background; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(output); Destroy(read);
            }
        }

        private void SmScreenBounds(StageSummonVisual visual, string tag, Camera source)
        {
            const int margin = 200;
            int width = Screen.width + margin * 2, height = Screen.height + margin * 2;
            var go = new GameObject("SummonBoundsCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            var output = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var read = new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            var active = RenderTexture.active;
            try
            {
                camera.CopyFrom(source); camera.enabled = false; camera.targetTexture = output;
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.orthographicSize = source.orthographicSize * height / Screen.height;
                camera.aspect = width/(float)height;
                Color32[] Render(Color color)
                {
                    camera.backgroundColor = color; camera.Render(); RenderTexture.active = output;
                    read.ReadPixels(new Rect(0,0,width,height),0,0); return read.GetPixels32();
                }
                var black = Render(Color.black); var white = Render(Color.white);
                int outside = 0, cardRow = 0, pixels = 0;
                float cardTop = AliveAllies().Concat(AliveEnemies()).Where(u=>u.currentCell!=null)
                    .Max(u=>u.currentCell.Card.StageHudHitRect(source).yMax);
                for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                {
                    int p = y*width+x;
                    if(1f-((white[p].r-black[p].r)+(white[p].g-black[p].g)+(white[p].b-black[p].b))/(3f*255f)<=.35f)continue;
                    pixels++;
                    if(x<margin||x>=margin+Screen.width||y<margin||y>=margin+Screen.height)outside++;
                    else if(y-margin<cardTop)cardRow++;
                }
                RecordMechanic("sm_screen_bounds_"+visual.Kind,"다수형 실제알파 화면밖0px·하단카드줄 침범0px", $"tag={tag}, pixels={pixels}, outside={outside}, cardRow={cardRow}, cardTop={cardTop:R}",
                    pixels>0 && outside==0 && cardRow==0);
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(output); Destroy(read); Destroy(go);
            }
        }
    }
}
#endif
