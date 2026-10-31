#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Effects.Vfx;
using Entities;
using Entities.View;
using Managers.Stage;
using Managers.UI.HUD;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private readonly string _sdCaptureSession = Guid.NewGuid().ToString("N");

        private IEnumerator ScenarioSdFrames(VerifyScenarioRequest config, bool actionsOnly = false)
        {
            bool enabled = GameManager.Instance.enabled;
            float prior = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            if (config.trigger == "temporary")
            {
                try { yield return SdTemporaryAssignments(config); }
                finally { GameManager.Instance.enabled = enabled; Time.captureDeltaTime = prior; Time.timeScale = 1f; }
                yield break;
            }
            yield return S8Prepare(config);
            var game = GameManager.Instance;
            var grid = GridManager.Instance;
            var roster = DataManager.LoadUnitDataList().units;
            var registered = DataManager.LoadStageFigureDataList().figures.Where(p => p.sd != null)
                .GroupBy(p => p.sd.prefix).Select(g => g.FirstOrDefault(p => p.sd.prefix == p.sprite.Replace("_STANDING", "_SD")) ?? g.First()).ToArray();
            var units = AliveAllies().OrderBy(u => Array.FindIndex(registered,
                p => p.sprite == roster.Single(d => d.id == u.ID).standing)).ToArray();
            var deathRigs = units.ToDictionary(u => u, u => u.currentCell.Card.StageProxy.Frames);
            var deathIds = units.ToDictionary(u => u, u => u.ID);
            var observedDeaths = new HashSet<int>();
            var seen = new HashSet<string>();
            var sequenceSteps = new Dictionary<int, int>();
            var actionSequences = new HashSet<int>();
            var normalSequences = new HashSet<int>();
            var normalActors = new HashSet<int>();
            void StartAction(Unit actor, ActionScheduler.ActionKind kind, string label)
            {
                if (actor == null) return;
                if (kind == ActionScheduler.ActionKind.Normal) normalActors.Add(actor.ID);
                else normalActors.Remove(actor.ID);
            }
            var pending = new Queue<(SdFrameRig rig, SdFrameRig.Pose pose)>();
            void Observe(SdFrameRig rig, SdFrameRig.Pose pose)
            {
                if (!units.Contains(rig.BoundUnit)) return;
                int id = rig.UnitId;
                sequenceSteps.TryGetValue(id, out int step);
                if (pose == SdFrameRig.Pose.WINDUP) sequenceSteps[id] = 1;
                else if (pose == SdFrameRig.Pose.STRIKE && step == 1) sequenceSteps[id] = 2;
                else if (pose == SdFrameRig.Pose.IDLE && step == 2)
                {
                    actionSequences.Add(id);
                    if (normalActors.Contains(id)) normalSequences.Add(id);
                    sequenceSteps[id] = 0;
                }
                else if (pose == SdFrameRig.Pose.HIT) sequenceSteps[id] = 0;
                pending.Enqueue((rig, pose));
                // 사망 사본/침식 전에 발행된 실제 DOWN을 같은 프레임의 Unity 카메라로 보존한다.
                if (pose == SdFrameRig.Pose.DOWN) SdRenderPose(rig, rig.Renderer, pose);
            }
            void Died(Unit unit, Unit attacker)
            {
                if (!deathRigs.TryGetValue(unit, out var rig) || rig == null) return;
                if (rig.CurrentPose == SdFrameRig.Pose.DOWN && FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Any(r => r.sprite != null && r.sprite.name == rig.Profile.prefix+"_DOWN")) observedDeaths.Add(deathIds[unit]);
                SdFeetCheck(rig,"death_event",true);
            }
            SdFrameRig.PoseChanged += Observe;
            ActionScheduler.AnyActionStarted += StartAction;
            Unit.AnyUnitDied += Died;
            try
            {
                var poses = Enum.GetValues(typeof(SdFrameRig.Pose)).Cast<SdFrameRig.Pose>().ToArray();
                RecordMechanic("sd_data_catalog", "97_stage_figures의 전용 SD별 전체 프레임 로드",
                    $"characters={registered.Length}, frames={registered.Length * poses.Length}|" + string.Join(",", registered.Select(p => p.sd.prefix)),
                    registered.Length > 0 && registered.All(p => poses.All(pose => Resources.Load<Sprite>("Sprite/SD/" + p.sd.prefix + "_" + pose) != null)));
                foreach (var profile in registered) SdSourceProfileCheck(profile.sd, profile.sd.prefix);
                RecordMechanic("sd_environment", "열린 에디터 IPC·Stage·5명", Application.dataPath + "|" + Application.unityVersion,
                    VisualModeState.Current == VisualMode.Stage && units.Length == 5);
                foreach (var unit in units)
                {
                    var view = unit.currentCell.Card; var figure = view.StageProxy; var rig = figure?.Frames;
                    RecordMechanic("sd_registered_" + unit.ID, "단일 SD·표시 렌더러/chest·손/머리 앵커", rig?.Renderer.sprite.name,
                        rig != null && !figure.Head.enabled && view.PortraitRenderer == rig.Renderer &&
                        Vector3.Distance(unit.currentCell.CardCenter, rig.ChestWorld) < .0001f && VfxSpace.CastPoint(unit, null) == rig.HandWorld);
                    if (rig == null) continue;
                    SdSourceCheck(rig);
                    yield return SdCapture(rig, SdFrameRig.Pose.IDLE, seen);
                }
                RecordStageFigureChecks("sd", Camera.main, grid.heroList.Concat(grid.enemyList).Where(u => u.currentCell != null).Select(u => u.currentCell).Distinct().ToList());
                RecordMechanic("sd_proxy_fallback", "미등록 적은 기존 프록시", string.Join(",", AliveEnemies().Select(u => u.currentCell.Card.StageProxy.SpriteKey)),
                    AliveEnemies().All(u => u.currentCell.Card.StageProxy.Frames == null));
                if (!actionsOnly)
                {
                yield return EndOfFrame;
                var treasury = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.name == "PartyTreasury");
                var left = units.Max(u => u.currentCell.Card.StageHudHitRect(Camera.main).xMax);
                var right = AliveEnemies().Min(u => u.currentCell.Card.StageHudHitRect(Camera.main).xMin);
                RecordMechanic("sd_d8_hidden", "재보 없는 편성: 패널 숨김·양쪽 줄 간격14px", $"active={treasury.gameObject.activeSelf}, gap={right-left}",
                    !TreasuryPointStrip.ShouldShow && !treasury.gameObject.activeSelf && Mathf.Abs(right-left-14f)<1f);
                yield return CaptureStill("battle_5v5_no_treasury");
                }

                // 실제 스케줄러/고유 코드 흐름에서 시전·정점을 채집한다. 포즈 직접 지정은 하지 않는다.
                // 사망 방지 캐릭터는 배속별 새 편성에서 검사한다. 자원/효과를 지우거나 판정을 바꾸지 않는다.
                var speeds = config.trigger == "isolated_speed" || actionsOnly ? new[] { config.timeScale } : new[] { 1f, 5f };
                foreach (float speed in speeds)
                {
                    Time.timeScale = speed;
                    game.ActionScheduler.BeginRound();
                    foreach (var unit in units) unit.FillUltimateResource();
                    bool paused = false, cutin = false;
                    int finished = 0; void Finish(Unit u, ActionScheduler.ActionKind k) => finished++;
                    ActionScheduler.AnyActionFinished += Finish;
                    try
                    {
                        float deadline = Time.realtimeSinceStartup + 100f;
                        while (finished < 35 && Time.realtimeSinceStartup < deadline)
                        {
                            game.ActionScheduler.Tick(Time.deltaTime);
                            yield return EndOfFrame;
                            foreach (var u in units.Where(u => u.isActive)) SdFeetCheck(u.currentCell.Card.StageProxy.Frames, "action_" + speed, false);
                            while (pending.Count > 0)
                            {
                                var item = pending.Dequeue();
                                if (item.rig != null && item.rig.Ready && item.rig.CurrentPose == item.pose && item.pose != SdFrameRig.Pose.DOWN)
                                    yield return SdCapture(item.rig, item.pose, seen);
                            }
                            var windup = units.Select(u => u.currentCell?.Card.StageProxy?.Frames).FirstOrDefault(r => r != null && r.CurrentPose == SdFrameRig.Pose.WINDUP);
                            if (!paused && windup != null)
                            {
                                paused = true; var pose = windup.CurrentPose; var sprite = windup.Renderer.sprite;
                                float start = Time.time;
                                Time.timeScale = 0f; yield return WaitUnscaled(.3f);
                                RecordMechanic("sd_pause_" + speed, "1/5배에서 0.3초 정지: 포즈·스프라이트·scaled 시간 불변", $"{pose}/{windup.CurrentPose}|{start}/{Time.time}",
                                    pose == windup.CurrentPose && sprite == windup.Renderer.sprite && Mathf.Abs(Time.time-start)<.0001f);
                                Time.timeScale = speed;
                            }
                            foreach (var u in units.Where(UltimateCutIn.IsShowing))
                            {
                                cutin = true;
                                RecordMechanic("sd_cutin_" + speed, "초월기 컷인 중 시전자 WINDUP·컷인은 스탠딩", u.UnitName,
                                    u.currentCell.Card.StageProxy.Frames.CurrentPose == SdFrameRig.Pose.WINDUP);
                            }
                            if (finished >= 15 && units.All(u => seen.Contains(u.ID+"_WINDUP") && seen.Contains(u.ID+"_STRIKE")) && paused && cutin) break;
                        }
                        RecordMechanic("sd_action_coverage_" + speed, "실제 행동·포즈·정지·초월기 관측", $"finished={finished}, paused={paused}, cutin={cutin}", finished >= 15 && paused && cutin);
                    }
                    finally { ActionScheduler.AnyActionFinished -= Finish; game.ActionScheduler.EndRound(); }
                    foreach (var u in units) u.ActiveUltimateCode?.StopCode();
                    yield return new WaitForSeconds(.8f);
                    foreach (var support in units.Where(u => (u.ID is 124 or 125 or 162 or 200) && !normalSequences.Contains(u.ID)))
                    {
                        bool complete = false;
                        IEnumerator Normal()
                        {
                            yield return NordAction(support, ActionScheduler.ActionKind.Normal, scheduler =>
                            {
                                foreach (var u in units) u.ClearUltimateResource();
                                if (support.ID == 200) { PartyTreasury.BeginBattle(); PartyTreasury.Charge(support,1); }
                                scheduler.AdvanceAction(support,1);
                            });
                            complete = true;
                        }
                        var action = StartCoroutine(Normal());
                        while (!complete)
                        {
                            yield return EndOfFrame;
                            while (pending.Count > 0)
                            {
                                var item = pending.Dequeue();
                                if (item.rig != null && item.rig.Ready && item.rig.CurrentPose == item.pose)
                                    yield return SdCapture(item.rig,item.pose,seen);
                            }
                        }
                        yield return action;
                        RecordMechanic("sd_support_fixture_"+support.ID,"자연 관측 누락 시 실제 일반행동 지정·만사 무사만 재보 전투 초기화/재보1 공급",support.UnitName,normalSequences.Contains(support.ID));
                    }
                }
                Time.timeScale = 1f;
                foreach (var unit in units)
                {
                    RecordMechanic("sd_action_sequence_" + unit.ID, "실제 WINDUP→STRIKE→IDLE 관측", unit.UnitName, actionSequences.Contains(unit.ID));
                    if (unit.ID is 124 or 125 or 162 or 200)
                        RecordMechanic("sd_support_normal_" + unit.ID, "치유/지원 캐릭터 실제 일반행동 WINDUP→STRIKE→IDLE", unit.UnitName, normalSequences.Contains(unit.ID));
                }
                Unit attacker = AliveEnemies().First();
                foreach (var unit in units)
                {
                    var rig = unit.currentCell.Card.StageProxy.Frames;
                    unit.TakeDamage(new DamageContext(attacker, 7, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }, false));
                    yield return EndOfFrame;
                    yield return SdCapture(rig, SdFrameRig.Pose.HIT, seen);
                    yield return new WaitForSeconds(.25f);
                    unit.TakeDamage(new DamageContext(attacker, 7, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }, false));
                    yield return new WaitForSeconds(.25f);
                    RecordMechanic("sd_hit_extend_" + unit.ID, "연타 마지막 피해부터0.42초 유지", rig.CurrentPose.ToString(), rig.CurrentPose == SdFrameRig.Pose.HIT);
                    yield return new WaitForSeconds(.22f);
                    RecordMechanic("sd_hit_idle_" + unit.ID, "마지막 타0.42초 뒤IDLE", rig.CurrentPose.ToString(), rig.CurrentPose == SdFrameRig.Pose.IDLE);
                }
                var controlled = units.First(); var controlledRig = controlled.currentCell.Card.StageProxy.Frames;
                controlled.ControlStarts(new ControlContext(attacker, 2));
                controlled.TakeDamage(new DamageContext(attacker, 7, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }, false));
                yield return EndOfFrame;
                RecordMechanic("sd_control_idle", "제어 중IDLE 고정", controlledRig.CurrentPose.ToString(), controlledRig.CurrentPose == SdFrameRig.Pose.IDLE);
                controlled.ControlEnds();

                if (!actionsOnly)
                {
                // 적에게도 같은 SD 등록 키를 사용하는 fixture: 실제 강인도 피해/격파, 이어 실제 사망.
                var provider = grid.SpawnUnit(-2, 4, false, 25, playEntry: false);
                provider.CastPassiveCode();
                var enemySd = grid.SpawnUnit(2, 4, true, 1001, playEntry: false);
                YvBoostEnemy(enemySd); enemySd.GrantToughness(1, units.First());
                grid.RefreshFieldLayout(); yield return grid.WaitForFieldLayoutSettled(); yield return EndOfFrame;
                // 현재 적 SD 데이터는 없다. 표시 fixture만 세이 원화로 구성하고 판정 유닛은 늑대 그대로 둔다.
                enemySd.currentCell.Card.StageProxy.Configure(Helpers.SpriteResource.LoadStanding("SEI_STANDING"), enemySd, enemySd.currentCell.Card.transform);
                var enemyRig = enemySd.currentCell.Card.StageProxy.Frames;
                bool broken = false; void Break(CombatDefeatContext c) { if (c.Grantor == enemySd) broken = true; }
                Toughness.Broken += Break;
                try { enemySd.TakeDamage(new DamageContext(units.First(), 7, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }, false)); }
                finally { Toughness.Broken -= Break; }
                yield return EndOfFrame;
                RecordMechanic("sd_break_no_down", "실제 강인도 격파: 생존·DOWN 없음·적 좌우반전", $"broken={broken}, pose={enemyRig?.CurrentPose}, flip={enemyRig?.Renderer.flipX}",
                    broken && enemySd.isActive && enemyRig != null && enemyRig.CurrentPose != SdFrameRig.Pose.DOWN && enemyRig.Renderer.flipX);
                var upper = enemySd.currentCell.Card.StageHudHitRect(Camera.main);
                RecordMechanic("sd_d9_upper", "증원 카드118×105·아랫줄과4px", upper.ToString(), Mathf.Abs(upper.width-118)<1 && Mathf.Abs(upper.height-105)<1 && Mathf.Abs(upper.y-172)<1);
                yield return CaptureStill("reinforcement_105px");
                var treasuryAlly = grid.SpawnUnit(-1, 4, false, 65, playEntry: false);
                treasuryAlly.CastPassiveCode(); PartyTreasury.BeginBattle(); PartyTreasury.Charge(treasuryAlly, 4);
                yield return EndOfFrame; yield return null; yield return EndOfFrame;
                var treasury = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.name == "PartyTreasury");
                var left = units.Where(u=>u.currentCell!=null).Max(u=>u.currentCell.Card.StageHudHitRect(Camera.main).xMax);
                var right = AliveEnemies().Where(u=>u!=enemySd).Min(u=>u.currentCell.Card.StageHudHitRect(Camera.main).xMin);
                RecordMechanic("sd_d8_visible", "재보 아군과Active: 패널 표시·중앙164px 확보", $"active={treasury.gameObject.activeSelf}, gap={right-left}, available={PartyTreasury.Available}",
                    TreasuryPointStrip.ShouldShow && treasury.gameObject.activeSelf && Mathf.Abs(right-left-164f)<1f && PartyTreasury.Available == 4);
                yield return CaptureStill("treasury_visible");
                }

                foreach (var unit in units)
                {
                    var rig = deathRigs[unit];
                    int unitId = deathIds[unit];
                    if (!unit.isActive)
                    {
                        // 에비스의 실제 피해 분담으로 동료도 같은 치사 피해에서 사망한다. 풀 반환 전 DOWN/퇴장 사본을 확인했다.
                        RecordMechanic("sd_death_shared_"+unitId,"같은 실제 피해에서 동시 사망·피해 순간 DOWN/퇴장 사본 관측",$"observed={observedDeaths.Contains(unitId)}",observedDeaths.Contains(unitId));
                        RecordMechanic("sd_death_"+unitId,"실제 사망: 원본DOWN·퇴장 사본DOWN","피해 순간 관측",observedDeaths.Contains(unitId));
                        if (observedDeaths.Contains(unitId)) seen.Add(unitId+"_DOWN");
                        continue;
                    }
                    int initialClones = Summons.Of(unit).Count(s => s.SummonKind == "uchi_clone");
                    int maximum = initialClones + 3;
                    int attempts = 0, prevented = 0, guarded = 0;
                    bool downExitObserved = false;
                    while (unit.isActive && unit.HpCurr > 0 && attempts < maximum)
                    {
                        attempts++;
                        int hp = unit.HpCurr, shield = unit.ShieldCurr;
                        int clones = Summons.Of(unit).Count(s => s.SummonKind == "uchi_clone");
                        bool applied = false, survived = false, substitute = false;
                        int remaining = clones, hpAfter = hp, shieldAfter = shield, actionId = -1;
                        var damagePose = rig.CurrentPose;
                        void ApplyLethal()
                        {
                            actionId = game.ActionScheduler.CurrentActionId;
                            unit.TakeDamage(new DamageContext(attacker, unit.HpMax * 10, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }, false));
                            survived = unit.isActive && unit.HpCurr > 0;
                            remaining = Summons.Of(unit).Count(summon => summon.SummonKind == "uchi_clone");
                            hpAfter = unit.HpCurr; shieldAfter = unit.ShieldCurr; damagePose = rig.CurrentPose;
                            substitute = survived && remaining < clones && hp == hpAfter && shield == shieldAfter;
                            if (!survived) downExitObserved = damagePose == SdFrameRig.Pose.DOWN &&
                                FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Any(r => r.sprite != null && r.sprite.name == rig.Profile.prefix+"_DOWN");
                            applied = true;
                        }
                        // 대신맞기만 별도 적 행동 ID가 필요하다. 나머지는 검증된 v3 즉시 피해 fixture를 유지한다.
                        if (initialClones > 0)
                            yield return CfRun(s => s.EnqueueAdditional(attacker, "sd_death_fixture", "SD 사망 fixture", ApplyLethal), () => applied);
                        else ApplyLethal();
                        if (substitute) guarded += clones - remaining;
                        else if (survived) prevented++;
                        RecordMechanic($"sd_death_attempt_{unitId}_{attempts}", "분신 대신맞기는 피해0·IDLE, 실제 피격 생존HIT·실제 사망DOWN",
                            $"actionId={actionId}, survived={survived}, substitute={substitute}, clones={clones}->{remaining}, hp={hp}->{hpAfter}, shield={shield}->{shieldAfter}, pose={damagePose}",
                            applied && damagePose == (substitute ? SdFrameRig.Pose.IDLE : survived ? SdFrameRig.Pose.HIT : SdFrameRig.Pose.DOWN));
                        if (substitute) yield return new WaitForSeconds(.45f);
                        else if (survived) yield return EndOfFrame;
                        if (substitute && remaining == 0)
                        {
                            unit.TakeDamage(new DamageContext(attacker, 7, BaseEnums.CodeType.Normal, new List<int> { DamageTag.TrueDamage }, false));
                            RecordMechanic("sd_guard_exhausted_hit_" + unitId, "분신 소모 후 실제 피해는 주인HIT", $"hp={unit.HpCurr}, pose={rig.CurrentPose}", rig.CurrentPose == SdFrameRig.Pose.HIT);
                            yield return new WaitForSeconds(.45f);
                        }
                    }
                    RecordMechanic("sd_death_fixture_" + unitId, "최대(초기분신수+3)회 치사 피해·대신맞기/사망 방지 소모 횟수 기록",
                        $"attempts={attempts}, maximum={maximum}, initialClones={initialClones}, guarded={guarded}, prevented={prevented}, alive={unit.isActive}, hp={unit.HpCurr}", !unit.isActive);
                    RecordMechanic("sd_death_"+unitId, "실제 사망: 원본DOWN·퇴장 사본DOWN", $"{rig.CurrentPose}, exitDownAtDamage={downExitObserved}",
                        !unit.isActive && rig.CurrentPose == SdFrameRig.Pose.DOWN && downExitObserved);
                    yield return new WaitForSeconds(.09f);
                    yield return EndOfFrame;
                    yield return SdCapture(rig, SdFrameRig.Pose.DOWN, seen);
                    yield return new WaitForSeconds(.7f);
                }
                RecordMechanic("sd_sheet_coverage", $"현재 편성{units.Length}명×{poses.Length}포즈 실제 흐름 촬영", string.Join(",", seen.OrderBy(s=>s)), seen.Count == units.Length * poses.Length);
            }
            finally
            {
                SdFrameRig.PoseChanged -= Observe;
                ActionScheduler.AnyActionStarted -= StartAction;
                Unit.AnyUnitDied -= Died;
                game.ActionScheduler.EndRound(); game.gameState = BaseEnums.GameState.Preparation; game.enabled = enabled;
                Time.captureDeltaTime = prior; Time.timeScale = 1f;
            }
        }

        private IEnumerator SdTemporaryAssignments(VerifyScenarioRequest config)
        {
            yield return S8Prepare(config);
            var grid = GridManager.Instance;
            var profiles = DataManager.LoadStageFigureDataList().figures.Where(p => p.sd != null).ToDictionary(p => p.sprite);
            var roster = DataManager.LoadUnitDataList().units.Where(d => !string.IsNullOrWhiteSpace(d.standing) &&
                profiles.TryGetValue(d.standing, out var p) && p.sd.prefix != d.standing.Replace("_STANDING", "_SD")).ToArray();
            void Check(Unit unit)
            {
                var view = unit.currentCell.Card;
                var figure = view.StageProxy;
                var rig = figure?.Frames;
                var expected = profiles[DataManager.LoadUnitDataList().units.Single(d => d.id == unit.ID).standing].sd;
                var frames = Enum.GetValues(typeof(SdFrameRig.Pose)).Cast<SdFrameRig.Pose>()
                    .Select(pose => Resources.Load<Sprite>("Sprite/SD/" + expected.prefix + "_" + pose)).ToArray();
                RecordMechanic("sd_temporary_" + unit.ID, "donor 5프레임·동일 측정값·단일 실제 렌더러·정상 셰이더·프록시 없음",
                    $"{unit.UnitName}|{expected.prefix}|{rig?.Renderer.sprite.name}|frames={frames.Count(s => s != null)}",
                    rig != null && !figure.Head.enabled && view.PortraitRenderer == rig.Renderer && rig.Renderer.enabled &&
                    rig.Renderer.sprite.name == expected.prefix + "_IDLE" && JsonUtility.ToJson(rig.Profile) == JsonUtility.ToJson(expected) &&
                    frames.All(s => s != null && s.texture != null) && rig.Renderer.sharedMaterial.shader.isSupported &&
                    rig.Renderer.sharedMaterial.shader.name == "NeverTheLast/StageFigureSlice");
                if (rig != null) SdRenderPose(rig, rig.Renderer, rig.CurrentPose);
            }
            yield return EndOfFrame;
            foreach (var unit in AliveAllies()) Check(unit);
            yield return CaptureStill("temporary_mixed_battle_5v5");
            for (int offset = 0; offset < roster.Length; offset += 5)
            {
                foreach (var unit in grid.heroList.ToArray()) grid.RetireUnit(unit);
                yield return null;
                for (int i = 0; i < 5 && offset + i < roster.Length; i++)
                    grid.SpawnUnit(i < 3 ? -1 : -2, i < 3 ? i + 1 : i - 2, false, roster[offset + i].id, playEntry: false);
                grid.RefreshFieldLayout();
                yield return grid.WaitForFieldLayoutSettled();
                yield return EndOfFrame;
                foreach (var unit in AliveAllies()) Check(unit);
            }
            RecordMechanic("sd_temporary_catalog", "10_units 아군 중 전용 SD 없는 전체 캐릭터 확인", string.Join(",", roster.Select(d => d.id)), roster.Length > 0);
        }

        private void SdSourceCheck(SdFrameRig rig)
        {
            SdSourceProfileCheck(rig.Profile, rig.BoundUnit.ID.ToString());
            SdFeetCheck(rig, "initial", true);
        }

        private void SdSourceProfileCheck(SdFrameData data, string key)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (SdFrameRig.Pose pose in Enum.GetValues(typeof(SdFrameRig.Pose)))
                {
                texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources/Sprite/SD", data.prefix+"_"+pose+".png")));
                var pixels = texture.GetPixels32(); int bottom = 0;
                // report_v6의 수르트 STRIKE 칼끝은 발보다20px 아래다. 발 영역(x<850)만 잰다.
                int maxX = data.prefix == "SURTR_SD" && pose == SdFrameRig.Pose.STRIKE ? 850 : texture.width;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < maxX; x++)
                    if (pixels[y*texture.width+x].a >= 129) bottom = Mathf.Max(bottom, texture.height-1-y);
                RecordMechanic("sd_source_feet_"+key+"_"+pose, "Unity에서 원본RGBA alpha≥129 발 하단 재측정(report_v6)", $"{texture.width}×{texture.height}, measured={bottom}, data={data.feetY}, x<{maxX}, errorPx={Mathf.Abs(bottom-data.feetY):R}",
                    texture.width == 1024 && texture.height == 1280 && Mathf.Abs(bottom-data.feetY)<=1);
                }
            }
            finally { Destroy(texture); }
#if UNITY_EDITOR
            foreach (SdFrameRig.Pose pose in Enum.GetValues(typeof(SdFrameRig.Pose)))
            {
                string path = "Assets/Resources/Sprite/SD/" + data.prefix + "_" + pose + ".png";
                var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                var settings = new UnityEditor.TextureImporterSettings(); importer.ReadTextureSettings(settings);
                var platform = importer.GetPlatformTextureSettings("Standalone");
                RecordMechanic("sd_import_"+key+"_"+pose, "Sprite·Center·FullRect·알파·밉맵끔·1024·BC7 HQ100", path,
                    Resources.Load<Sprite>("Sprite/SD/"+data.prefix+"_"+pose) != null && importer.textureType == UnityEditor.TextureImporterType.Sprite &&
                    importer.alphaIsTransparency && !importer.mipmapEnabled && importer.maxTextureSize == 1024 && settings.spriteAlignment == (int)SpriteAlignment.Center &&
                    settings.spriteMeshType == SpriteMeshType.FullRect && platform.overridden && platform.format == UnityEditor.TextureImporterFormat.BC7 && platform.compressionQuality == 100);
            }
#endif
        }

        private void SdFeetCheck(SdFrameRig rig, string tag, bool record)
        {
            if (rig == null) return;
            var sprite = rig.Renderer.sprite;
            Vector3 point = new((rig.Profile.faceX/1024f-.5f)*sprite.bounds.size.x,
                (.5f-rig.Profile.feetY/1280f)*sprite.bounds.size.y, 0f);
            if (rig.Renderer.flipX) point.x = -point.x;
            Vector3 foot = rig.Renderer.transform.TransformPoint(point);
            float error = Vector2.Distance(Camera.main.WorldToScreenPoint(foot), Camera.main.WorldToScreenPoint(rig.transform.position));
            if (record || error > 1f) RecordMechanic("sd_feet_"+tag+"_"+rig.UnitId+"_"+rig.CurrentPose,
                "프레임의 데이터 발 좌표=유닛 표시 원점(기존 몸체 모션 제외)±1px", error.ToString("F6"), error <= 1f);
        }

        private IEnumerator SdCapture(SdFrameRig rig, SdFrameRig.Pose pose, HashSet<string> seen)
        {
            string key = rig.UnitId+"_"+pose;
            if (seen.Contains(key) || rig.CurrentPose != pose) yield break;
            seen.Add(key);
            if (pose != SdFrameRig.Pose.DOWN) SdRenderPose(rig, rig.Renderer, pose);
            SdFeetCheck(rig, "capture", true);
            var bounds = rig.Renderer.bounds;
            if (pose == SdFrameRig.Pose.DOWN)
            {
                var ghost = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.sprite != null && r.sprite.name == rig.Profile.prefix+"_DOWN");
                if (ghost != null) bounds = ghost.bounds;
            }
            Vector3 min = Camera.main.WorldToScreenPoint(bounds.min), max = Camera.main.WorldToScreenPoint(bounds.max);
            RecordMechanic("sd_capture_"+key, "실제 Unity 화면 크롭 영역(좌하단xy)", $"{min.x:F3},{min.y:F3},{max.x-min.x:F3},{max.y-min.y:F3}", true);
            Capture("pose_"+key);
        }

        // 실제 행동에서 선택된 렌더러를 단독 촬영한다. 포즈/재질/색을 지정하거나 원본 PNG를 합성하지 않는다.
        private void SdRenderPose(SdFrameRig rig, SpriteRenderer renderer, SdFrameRig.Pose pose)
            => SdRenderSelected(renderer, rig.UnitId.ToString(), pose);

        private void SdRenderSelected(SpriteRenderer renderer, string key, SdFrameRig.Pose pose, string category = "sd_frames")
        {
            string directory = Path.Combine(_requestDirectory, "diagnostics", category, _sdCaptureSession);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"{key}_{pose}.png");
            if (File.Exists(path)) return;
            var go = new GameObject("SdVerifyCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = go.AddComponent<Camera>();
            int layer = renderer.gameObject.layer;
            RenderTexture active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(400, 500, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(400, 500, TextureFormat.RGBA32, false);
            try
            {
                camera.CopyFrom(Camera.main); camera.enabled = false;
                camera.targetTexture = target; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f,.085f,.14f,1f);
                camera.orthographic = true;
                camera.orthographicSize = renderer.sprite.bounds.size.y * Mathf.Abs(renderer.transform.lossyScale.y) * .53f;
                camera.aspect = .8f;
                camera.transform.rotation = renderer.transform.rotation;
                camera.transform.position = renderer.bounds.center - camera.transform.forward * 20f;
                renderer.gameObject.layer = 31;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,400,500),0,0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                RecordMechanic("sd_render_"+key+"_"+pose, "실제 포즈 렌더러 단독 Unity 촬영(위치·몸체 모션은 카메라가 추적)", path, true);
            }
            finally
            {
                renderer.gameObject.layer = layer; RenderTexture.active = active;
                camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
                Destroy(pixels); Destroy(go);
            }
        }
    }
}
#endif
