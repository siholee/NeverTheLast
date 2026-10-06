#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseClasses;
using Core;
using Effects;
using Effects.Vfx;
using Entities;
using Entities.View;
using Managers.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 파일 요청을 받은 에디터가 Play 모드로 들어오면 게임 API만으로 검증 장면을 만든다.
    /// 사용자 저장은 <see cref="DebugMode.BeginSession"/>이 격리하고, 모든 캡처가 끝나면 Play를 스스로 닫는다.
    /// </summary>
    public sealed partial class VerifyHarnessRunner : MonoBehaviour
    {
        private const int SeiId = 1;
        private const int ShiId = 2;
        private const int SabahId = 3;
        private const int JeanId = 7;
        private const int PygmalionId = 20;
        private const int HimikoId = 44;
        private const int ChandraId = 60;
        private const int BastetId = 122;
        private static readonly int[] PartyFiveIds = { SeiId, ShiId, ChandraId, HimikoId, PygmalionId };
        private static readonly int[] PartyFiveSabahIds = { SabahId, ShiId, ChandraId, HimikoId, PygmalionId };
        private static readonly int[] PartyFiveBastetIds = { BastetId, ShiId, ChandraId, HimikoId, PygmalionId };
        private static readonly int[] PartyFiveJeanIds = { JeanId, ShiId, ChandraId, HimikoId, PygmalionId };
        private static readonly int[] PartyFiveJeanBastetIds = { JeanId, BastetId, ChandraId, HimikoId, PygmalionId };
        private static readonly WaitForEndOfFrame EndOfFrame = new();

        private VerifyRequest _request;
        private VerifyRunResult _result;
        private string _requestDirectory;
        private string _currentScenario;
        private int _captureSequence;
        private int _eventSerial;
        private string _latestVisualEvent = "frame";
        private string _activeLineup;
        private readonly List<(int Serial, string Tag)> _events = new();
        private readonly List<FieldPointerInput> _disabledPointerInputs = new();
        private readonly HashSet<string> _dumpedPortraitScenarios = new();
        private bool _waitMatched;
        private bool _cursorWasVisible;
        private ActionScheduler.ActionKind _lastActionKind;
        private string _lastActionLabel;
        private Combat.ActionAnnouncementKind _lastAnnouncementKind;
        private string _lastAnnouncementName;
        private readonly List<(Combat.ActionAnnouncementKind Kind, string Name)> _announcements = new();

        private readonly struct StatusPreviewStep
        {
            public readonly string Label;
            public readonly string EventTag;
            public readonly string PartId;
            public readonly bool Beneficial;

            public StatusPreviewStep(string label, string eventTag, string partId, bool beneficial)
            {
                Label = label;
                EventTag = eventTag;
                PartId = partId;
                Beneficial = beneficial;
            }
        }

        private sealed class JeanAttackProbe
        {
            public bool Completed;
            public bool OriginalApplied;
            public bool CanceledBeforeOriginal;
            public bool FollowUpStarted;
            public Unit FinalTarget;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            string json = UnityEditor.SessionState.GetString(VerifyHarnessSession.RequestJsonKey, "");
            if (string.IsNullOrWhiteSpace(json)) return;
            if (FindFirstObjectByType<VerifyHarnessRunner>() != null) return;

            // GameManager.Start의 새 런 초기화보다 먼저 저장소를 격리해야 실제 세이브 삭제를 막을 수 있다.
            DebugMode.BeginSession();
            var root = new GameObject("VerifyHarnessRunner");
            DontDestroyOnLoad(root);
            root.AddComponent<VerifyHarnessRunner>();
#endif
        }

        private void Awake()
        {
            _cursorWasVisible = Cursor.visible;
            Cursor.visible = false;
            VerifyEvents.Raised += OnVerifyEvent;
            ActionScheduler.AnyActionStarted += OnActionStarted;
            Combat.ActionAnnouncer.Announced += OnActionAnnounced;
            AudioManager.SfxEventRecorded += OnSfxEvent;
            VfxComposer.SlotPlayed += OnVfxSlotPlayed;
            Combat.AttackTargetRouter.TargetRedirected += OnTargetRedirected;
            Unit.AnyDamageIncoming += V13Damage;
            Unit.AnyHealed += V13Heal;
            Unit.AnyUnitDied += V13Death;
        }

        private void OnDestroy()
        {
            Cursor.visible = _cursorWasVisible;
            Managers.UI.Core.UITooltip.HideAny();
            foreach (FieldPointerInput pointer in _disabledPointerInputs)
                if (pointer != null) pointer.enabled = true;
            VerifyEvents.Raised -= OnVerifyEvent;
            ActionScheduler.AnyActionStarted -= OnActionStarted;
            Combat.ActionAnnouncer.Announced -= OnActionAnnounced;
            AudioManager.SfxEventRecorded -= OnSfxEvent;
            VfxComposer.SlotPlayed -= OnVfxSlotPlayed;
            Combat.AttackTargetRouter.TargetRedirected -= OnTargetRedirected;
            Unit.AnyDamageIncoming -= V13Damage;
            Unit.AnyHealed -= V13Heal;
            Unit.AnyUnitDied -= V13Death;
            if (_v13MaskMaterial != null) Destroy(_v13MaskMaterial);
            BattlefieldBackdrop.SetVerificationBackgroundOverride(null);
            SkillFx.PlaybackMode = SkillFxPlaybackMode.Final;
            VfxStyleSettings.Reset();
        }

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            string json = UnityEditor.SessionState.GetString(VerifyHarnessSession.RequestJsonKey, "");
            _requestDirectory = UnityEditor.SessionState.GetString(VerifyHarnessSession.RequestDirectoryKey, "");
            _request = JsonUtility.FromJson<VerifyRequest>(json) ?? new VerifyRequest();
            NormalizeScenarioList();

            _result = new VerifyRunResult
            {
                success = true,
                startedAtUtc = DateTime.UtcNow.ToString("O"),
                resolution = $"{Screen.width}x{Screen.height}",
            };

            yield return ExecuteGuarded(RunAll());
            yield return WaitForCapturedFiles(5f);

            DebugMode.ResetAll();
            DebugMode.SuiteRunning = false;
            BattlefieldBackdrop.SetVerificationBackgroundOverride(null);
            HovlFx.ClearTransient();
            if (AudioManager.Instance != null)
            {
                // 검증 세션 전용 DDoL 오디오가 씬 종료 잔여 오브젝트로 집계되지 않게 먼저 수명을 끝낸다.
                Destroy(AudioManager.Instance.gameObject);
                yield return null;
            }
            _result.finishedAtUtc = DateTime.UtcNow.ToString("O");
            _result.success &= _result.scenarios.All(item => item.success);
            _result.message = _result.success
                ? $"{_result.scenarios.Count}개 시나리오를 완료했다."
                : "하나 이상의 시나리오가 실패했다.";

            WriteRuntimeResult();
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            yield break;
#endif
        }

        private IEnumerator RunAll()
        {
            yield return EnsureGameScene();
            yield return WaitForGameReady(20f);
            if (GameManager.Instance?.RoundManager == null || GridManager.Instance == null)
            {
                AddScenarioFailure("bootstrap", "Game 씬 매니저가 제한 시간 안에 준비되지 않았다.");
                yield break;
            }

            DebugMode.BeginSession();
            DebugMode.SuiteRunning = true;
            yield return BuildFreshRun(ResolveLineup(null));
            DisablePointerHover();
            if (!AliveAllies().Any())
            {
                AddScenarioFailure("bootstrap", "기본 검증 편성을 만들지 못했다.");
                yield break;
            }

            foreach (VerifyScenarioRequest scenario in _request.scenarios)
            {
                yield return ExecuteScenario(scenario);
            }

            // 마지막 캡처 직후 Play Mode가 닫히면 수명이 짧은 VFX가 씬 정리 오류로 집계될 수 있다.
            // 판정을 먼저 멈추고 실제 연출 수명만큼 비가산 시간으로 정리한다.
            GameManager.Instance?.DebugResetBattle();
            HovlFx.ClearTransient();
            yield return WaitUnscaled(1.1f);
        }

        private void NormalizeScenarioList()
        {
            _request.scenarios ??= new List<VerifyScenarioRequest>();
            if (_request.scenarioNames != null)
            {
                foreach (string name in _request.scenarioNames.Where(value => !string.IsNullOrWhiteSpace(value)))
                    _request.scenarios.Add(new VerifyScenarioRequest { name = name });
            }
        }

        private void DisablePointerHover()
        {
            _disabledPointerInputs.Clear();
            foreach (FieldPointerInput pointer in FindObjectsByType<FieldPointerInput>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (pointer == null || !pointer.enabled) continue;
                pointer.enabled = false;
                _disabledPointerInputs.Add(pointer);
            }
            Managers.UI.Core.UITooltip.HideAny();
        }

        private IEnumerator WaitForGameReady(float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            int stableFrames = 0;
            GameManager stableGame = null;
            GridManager stableGrid = null;
            CharacterSelectionManager stableSelection = null;
            RunManager stableRun = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                GameManager game = GameManager.Instance;
                GridManager grid = GridManager.Instance;
                CharacterSelectionManager selection = CharacterSelectionManager.Instance;
                RunManager run = RunManager.Instance;
                bool ready = SceneManager.GetActiveScene().name == "Game" && game != null &&
                             game.RoundManager != null && game.unitDataList?.units != null &&
                             grid != null && selection != null && run != null;
                if (ready && game == stableGame && grid == stableGrid &&
                    selection == stableSelection && run == stableRun)
                {
                    stableFrames++;
                    if (stableFrames >= 5) yield break;
                }
                else
                {
                    stableFrames = ready ? 1 : 0;
                    stableGame = game;
                    stableGrid = grid;
                    stableSelection = selection;
                    stableRun = run;
                }
                yield return null;
            }
        }

        private static IEnumerator EnsureGameScene()
        {
            if (SceneManager.GetActiveScene().name == "Game") yield break;
            GameStartIntent.Current = GameStartIntent.Intent.NewGame;
            GameManager.LoadBattleScene();
            yield return null;
        }

        private IEnumerator BuildFreshRun(string lineup)
        {
            yield return WaitForGameReady(8f);
            GameManager game = GameManager.Instance;
            CharacterSelectionManager selection = CharacterSelectionManager.Instance;
            RunManager run = RunManager.Instance;
            if (game?.RoundManager == null || GridManager.Instance == null || selection == null || run == null)
            {
                FailLineupBuild("새 런을 만들 매니저가 안정적으로 준비되지 않았다.");
                yield break;
            }

            game.DebugResetBattle();
            run.StartRun();
            selection.ClearLineup();
            int[] ids = lineup switch
            {
                "solo1" => new[] { SeiId },
                "party5_sabah" => PartyFiveSabahIds,
                "party5_bastet" => PartyFiveBastetIds,
                "party5_jean" => PartyFiveJeanIds,
                "party5_jean_bastet" => PartyFiveJeanBastetIds,
                "party5_lokapala" => PartyFiveLokapalaIds,
                "party5_summon" => PartyFiveSummonIds,
                "party5_yama_varuna" => PartyFiveYamaVarunaIds,
                "party5_vedic_trio" => new[] { 64, 62, 67, 66, 63 },
                "party5_reward_ui" => PartyFiveRewardUiIds,
                "duo_agni_surya" => new[] { AgniId, SuryaId },
                "duo_shi_sabah" => new[] { ShiId, SabahId },
                _ => PartyFiveIds,
            };
            foreach (int id in ids)
            {
                if (!selection.AddHero(id))
                {
                    FailLineupBuild($"캐릭터 ID {id}를 편성하지 못했다.");
                    yield break;
                }
            }
            selection.ConfirmSelection();
            yield return WaitForState(BaseEnums.GameState.Preparation, 8f);
            yield return WaitUnscaled(1.15f);
            _activeLineup = lineup;
        }

        private IEnumerator EnsureLineup(VerifyScenarioRequest config)
        {
            string lineup = ResolveLineup(config);
            if (lineup != "party5" && lineup != "party5_sabah" && lineup != "party5_bastet" &&
                lineup != "party5_jean" && lineup != "party5_jean_bastet" && lineup != "solo1" &&
                lineup != "party5_lokapala" && lineup != "party5_summon" && lineup != "party5_yama_varuna" && lineup != "party5_vedic_trio" &&
                lineup != "party5_reward_ui" && lineup != "duo_agni_surya" && lineup != "duo_shi_sabah")
            {
                FailCurrent($"알 수 없는 lineup: {lineup}");
                yield break;
            }
            if (_activeLineup == lineup) yield break;
            yield return BuildFreshRun(lineup);
        }

        private IEnumerator ExecuteScenario(VerifyScenarioRequest scenario)
        {
            string name = scenario?.name?.Trim().ToLowerInvariant();
            _currentScenario = string.IsNullOrWhiteSpace(name) ? "unknown" : name;
            _captureSequence = 0;
            _announcements.Clear();
            SkillFx.PlaybackMode = ResolveVfxVariant(scenario);
            VfxStyleSettings.Active = ResolveVfxStyle(scenario);
            int beforeCaptures = _result.captures.Count;
            var scenarioResult = new VerifyScenarioResult
            {
                name = _currentScenario,
                success = true,
                timeScale = scenario != null && scenario.timeScale > 0f ? scenario.timeScale : 1f,
                startedRealtime = Time.realtimeSinceStartup,
            };
            _result.scenarios.Add(scenarioResult);

            yield return EnsureLineup(scenario);
            if (!scenarioResult.success)
            {
                scenarioResult.message ??= "편성을 만들지 못했다.";
                _result.success = false;
                yield break;
            }

            IEnumerator body = _currentScenario switch
            {
                var visualName when visualName.StartsWith("v13yv_", StringComparison.Ordinal) => ScenarioYvVisual(scenario),
                "guide_rework" => ScenarioGuideRework(scenario),
                "effect_hit_core" => ScenarioEffectHitCore(scenario),
                "release_save" => ScenarioReleaseSave(scenario),
                "release_shield" => ScenarioReleaseShield(scenario),
                "release_campaign" => ScenarioReleaseCampaign(scenario),
                "release_event" => ScenarioReleaseEvent(scenario),
                "release_loss" => ScenarioReleaseLoss(scenario),
                "ui_overhaul" => ScenarioUIOverhaul(scenario),
                "ui_overhaul_menu" => ScenarioUIOverhaulMenu(scenario),
                "reward_ui_forms" => ScenarioRewardUiForms(scenario),
                var rewardUiName when IsRewardUiScenario(rewardUiName) => ScenarioUIOverhaul(scenario),
                "prep" => ScenarioPrep(scenario),
                "combat_1v1" => ScenarioCombat(scenario, CombatStage(scenario, 6, 1), "combat"),
                "combat_1v2" => ScenarioCombat(scenario, CombatStage(scenario, 6, 2), "combat"),
                "battle_banner" => ScenarioBattleBanner(scenario),
                "action_order" => ScenarioActionOrder(scenario),
                "boss" => ScenarioCombat(scenario, 10, "boss"),
                "sei_normal" => ScenarioSeiNormal(scenario),
                "sei_normal_legacy" => ScenarioSeiNormal(scenario),
                "sei_normal_handpaint" => ScenarioSeiNormal(scenario),
                "rising_spike" => ScenarioRisingSpike(scenario),
                "shi_normal" => ScenarioShiNormal(scenario),
                "shi_normal_legacy" => ScenarioShiNormal(scenario),
                "shi_normal_handpaint" => ScenarioShiNormal(scenario),
                "shi_bell_3" => ScenarioShiBell(scenario, 3),
                "shi_bell_6" => ScenarioShiBell(scenario, 6),
                "shi_bell_9" => ScenarioShiBell(scenario, 9),
                "shi_trait_execute" => ScenarioShiTraitExecute(scenario),
                "shi_reaction" => ScenarioShiReaction(scenario),
                "sabah_normal" => ScenarioHeroNormal(scenario, SabahId, "사바흐", "sabah_normal", 1.72f),
                "sabah_normal_legacy" => ScenarioHeroNormal(scenario, SabahId, "사바흐", "sabah_normal", 1.72f),
                "sabah_normal_handpaint" => ScenarioHeroNormal(scenario, SabahId, "사바흐", "sabah_normal", 1.72f),
                "sabah_lightning_slash" => ScenarioSabahLightningSlash(scenario),
                "sabah_ult" => ScenarioHeroUltimate(scenario, SabahId, "사바흐", "sabah_ult", 2.55f),
                "himiko_normal" => ScenarioHeroNormal(scenario, HimikoId, "히미코", "himiko_normal"),
                "himiko_normal_legacy" => ScenarioHeroNormal(scenario, HimikoId, "히미코", "himiko_normal"),
                "himiko_normal_handpaint" => ScenarioHeroNormal(scenario, HimikoId, "히미코", "himiko_normal"),
                "pygmalion_normal" => ScenarioHeroNormal(scenario, PygmalionId, "피그말리온", "pygmalion_normal"),
                "pygmalion_normal_legacy" => ScenarioHeroNormal(scenario, PygmalionId, "피그말리온", "pygmalion_normal"),
                "pygmalion_normal_handpaint" => ScenarioHeroNormal(scenario, PygmalionId, "피그말리온", "pygmalion_normal"),
                "chandra_normal" => ScenarioHeroNormal(scenario, ChandraId, "찬드라", "chandra_normal"),
                "chandra_normal_legacy" => ScenarioHeroNormal(scenario, ChandraId, "찬드라", "chandra_normal"),
                "chandra_normal_handpaint" => ScenarioHeroNormal(scenario, ChandraId, "찬드라", "chandra_normal"),
                "chandra_passive" => ScenarioChandraPassive(scenario),
                "chandra_ult" => ScenarioChandraUltimate(scenario),
                "chandra_special" => ScenarioChandraSpecial(scenario),
                "shi_ult" => ScenarioShiBell(scenario, 9),
                "himiko_passive" => ScenarioHimikoPassive(scenario),
                "himiko_blaze_burst" => ScenarioHimikoBlazeBurst(scenario),
                "himiko_blaze_burst_legacy" => ScenarioHimikoBlazeBurst(scenario),
                "himiko_blaze_burst_handpaint" => ScenarioHimikoBlazeBurst(scenario),
                "himiko_ult" => ScenarioHeroUltimate(scenario, HimikoId, "히미코", "himiko_ult", 0.75f),
                "pygmalion_passive" => ScenarioPygmalionPassive(scenario),
                "pygmalion_ult" => ScenarioHeroUltimate(scenario, PygmalionId, "피그말리온", "pygmalion_ult", 0.75f),
                "bastet_normal" => ScenarioHeroNormal(scenario, BastetId, "바스테트", "bastet_normal", 1.85f),
                "bastet_guard_single" => ScenarioBastetGuard(scenario, false),
                "bastet_guard_multi" => ScenarioBastetGuard(scenario, true),
                "bastet_guard_death_cancel" => ScenarioBastetGuardDeathCancel(scenario),
                "bastet_counter_followup" => ScenarioBastetCounterFollowUp(scenario),
                "bastet_persistent_hud" => ScenarioBastetPersistentHud(scenario),
                "jean_normal" => ScenarioHeroNormal(scenario, JeanId, "잔", "jean_normal", 1.95f),
                "jean_counter_single" => ScenarioJeanCounter(scenario, false),
                "jean_counter_multi" => ScenarioJeanCounter(scenario, true),
                "jean_counter_kill_cancel" => ScenarioJeanCounterKillCancel(scenario),
                "jean_counter_control_cancel" => ScenarioJeanCounterControlCancel(scenario),
                "jean_bastet_guard_counter" => ScenarioJeanBastetGuardCounter(scenario),
                "jean_resources" => ScenarioJeanResources(scenario),
                "jean_ultimate" => ScenarioJeanUltimate(scenario),
                "contact" => ScenarioTriggeredBattle(scenario, CombatStage(scenario, 6, 1), "contact_action"),
                "void_bolt" => ScenarioTriggeredBattle(scenario, CombatStage(scenario, 6, 2), "void_bolt"),
                "enemy_wolf_normal" => ScenarioEnemyNormal(scenario, 1001, 6, "enemy_wolf_normal"),
                "enemy_marksman_normal" => ScenarioEnemyNormal(scenario, 1002, 6, "enemy_marksman_normal"),
                "enemy_vanguard_normal" => ScenarioEnemyNormal(scenario, 1003, 7, "enemy_vanguard_normal"),
                "enemy_crusher_normal" => ScenarioEnemyNormal(scenario, 2001, 6, "enemy_crusher_normal", true),
                "enemy_boss_normal" => ScenarioEnemyNormal(scenario, 3001, 6, "enemy_boss_normal", true),
                "enemy_smash_ult" => ScenarioEnemyUltimate(scenario, 1001, "enemy_smash_ult", false, 1.90f),
                "enemy_volley_ult" => ScenarioEnemyUltimate(scenario, 1002, "enemy_volley_ult", false, 2.00f),
                "enemy_ground_breaker_ult" => ScenarioEnemyUltimate(scenario, 2001, "enemy_ground_breaker_ult", true, 2.00f),
                "enemy_void_breath_ult" => ScenarioEnemyUltimate(scenario, 3001, "enemy_void_breath_ult", true, 2.35f),
                "void_boss_ult" => ScenarioEnemyBossUltimate(scenario),
                "ult" => ScenarioUltimate(scenario),
                "bigbang" => ScenarioBigBang(scenario),
                "death" => ScenarioDeath(scenario, false),
                "death_boss" => ScenarioDeath(scenario, true),
                "entry" => ScenarioEntry(scenario),
                "status_preview" => ScenarioStatusPreview(scenario),
                "reward_preview" => ScenarioRewardPreview(scenario),
                "result_preview" => ScenarioResultPreview(scenario),
                "common_sfx" => ScenarioCommonSfx(scenario),
                "ui_sfx" => ScenarioUiSfx(scenario),
                "backdrop_transition" => ScenarioBackdropTransition(scenario),
                "speed8" => ScenarioSpeed8(scenario),
                "layer_isolation" => ScenarioLayerIsolation(scenario),
                "five_party" => ScenarioFiveParty(scenario),
                "r12_core" => ScenarioR12Core(scenario),
                "r12_lokapala" => ScenarioR12Lokapala(scenario),
                "r12_summon" => ScenarioR12Summon(scenario),
                "yama_varuna_core" => ScenarioYamaVarunaCore(scenario),
                "v13trio_visual" or "v13trio_visual8" or "v13trio_visual_sparse_1920_4x" => ScenarioVedicTrioVisual(scenario),
                "trio_core" => ScenarioVedicTrioCore(scenario),
                var currentname when currentname.StartsWith("v13surya_hud_") => ScenarioSuryaHud(scenario),
                "trio_balance" => ScenarioVedicTrioBalance(scenario),
                var naturalName when naturalName.StartsWith("trio_natural_") => ScenarioVedicTrioNatural(scenario),
                "kubera_followup_core" => ScenarioKuberaFollowupCore(scenario),
                "kubera_followup_lokapala_ui" => ScenarioKuberaFollowupLokapalaUi(scenario),
                "v13a_indra" or "v13a_vayu" or "v13a_kubera" or "v13a_chandra" or "v13a_gate" or "v13b_anubis" or "v13b_uchi" or "v13b_party" => ScenarioV13A(scenario),
                _ => null,
            };

            if (body == null)
            {
                scenarioResult.success = false;
                scenarioResult.message = $"알 수 없는 시나리오: {_currentScenario}";
            }
            else
            {
                yield return ExecuteGuarded(body, exception =>
                {
                    scenarioResult.success = false;
                    scenarioResult.message = exception.Message;
                });
            }

            scenarioResult.captureCount = _result.captures.Count - beforeCaptures;
            scenarioResult.finishedRealtime = Time.realtimeSinceStartup;
            if (scenarioResult.captureCount == 0 && scenarioResult.success)
            {
                scenarioResult.success = false;
                scenarioResult.message = "캡처가 생성되지 않았다.";
            }
            if (string.IsNullOrEmpty(scenarioResult.message))
                scenarioResult.message = scenarioResult.success ? "완료" : "실패";
            _result.success &= scenarioResult.success;
        }

        private IEnumerator ScenarioPrep(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 1), false, false, false);
            yield return CaptureStill("prep_1v1");
            yield return PrepareStage(config, 2, false, false, false);
            yield return CaptureStill("prep_1v2");
        }

        private IEnumerator ScenarioCombat(VerifyScenarioRequest config, int defaultStage, string tag)
        {
            yield return PrepareStage(config, Stage(config, defaultStage), true, true, true);
            yield return WaitUnscaled(Warmup(config, 0.4f));
            yield return CaptureWindow(tag, CaptureSeconds(config), EveryFrames(config));
        }

        private IEnumerator ScenarioBattleBanner(VerifyScenarioRequest config)
        {
            // 준비 화면부터 0.5초를 남긴 뒤 실제 라운드를 열어, 개전 특성과 양측 행동 공지가
            // 같은 5:4 전투 흐름에서 쌓이고 밀려나는 모습을 검수한다.
            yield return PrepareStage(config, Stage(config, 6), false, true, true);
            bool started = false;
            yield return CaptureActionWindowRealtimeAtInterval("battle_banner",
                CaptureSeconds(config, 7.2f), 1f / 30f, () =>
                {
                    GameManager.Instance.StartRound();
                    started = true;
                });
            if (!started || GameManager.Instance.gameState != BaseEnums.GameState.RoundInProgress)
                FailCurrent("발동 배너 검증용 5:4 전투를 시작하지 못했다.");
        }

        private IEnumerator ScenarioActionOrder(VerifyScenarioRequest config)
        {
            // 서열 UI 한 영상에서 자연 행동, 앞당김·지연, 추가행동 삽입, 궁극기 끼어들기,
            // 사망 제거를 모두 확인한다. 전투 규칙에는 남지 않는 하네스 전용 조작이다.
            yield return PrepareStage(config, Stage(config, 6), false, true, true);
            float seconds = CaptureSeconds(config, 8.6f);
            const float interval = 1f / 30f;
            float startedAt = Time.realtimeSinceStartup;
            bool battleStarted = false;
            bool advanced = false;
            bool delayed = false;
            bool additional = false;
            bool ultimate = false;
            bool removed = false;
            bool pygmalionDelayedAgain = false;
            int sample = 0;
            while (Time.realtimeSinceStartup - startedAt <= seconds)
            {
                float targetTime = startedAt + sample * interval;
                while (Time.realtimeSinceStartup + 0.0001f < targetTime) yield return null;
                float elapsed = Time.realtimeSinceStartup - startedAt;
                ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;

                if (!battleStarted && elapsed >= 0.5f)
                {
                    GameManager.Instance.StartRound();
                    battleStarted = true;
                    // 행동서열 UI 검수 중 피그말리온의 기존 연출이 P11 VFX 표에 섞이지 않도록
                    // 유닛은 5인 편성에 그대로 둔 채 본 행동치만 뒤로 보낸다.
                    scheduler = GameManager.Instance?.ActionScheduler;
                    scheduler?.DelayAction(AliveAllies().FirstOrDefault(unit => unit.ID == PygmalionId), 0.95f);
                    VerifyEvents.Raise("action_order_start");
                }
                if (battleStarted && !advanced && elapsed >= 1.7f)
                {
                    Unit unit = AliveAllies().LastOrDefault();
                    scheduler?.AdvanceAction(unit, 0.78f);
                    advanced = true;
                    VerifyEvents.Raise("action_order_advance");
                }
                if (battleStarted && !delayed && elapsed >= 2.6f)
                {
                    Unit unit = AliveEnemies().FirstOrDefault();
                    scheduler?.DelayAction(unit, 0.58f);
                    delayed = true;
                    VerifyEvents.Raise("action_order_delay");
                }
                if (battleStarted && !additional && elapsed >= 3.4f)
                {
                    Unit unit = AliveAllies().Skip(1).FirstOrDefault();
                    scheduler?.EnqueueAdditional(unit, "verify_action_order", "검수 추가행동", () => { });
                    additional = true;
                    VerifyEvents.Raise("action_order_insert");
                }
                if (battleStarted && !pygmalionDelayedAgain && elapsed >= 4.1f)
                {
                    scheduler?.DelayAction(AliveAllies().FirstOrDefault(unit => unit.ID == PygmalionId), 0.95f);
                    pygmalionDelayedAgain = true;
                }
                if (battleStarted && !ultimate && elapsed >= 4.6f)
                {
                    AliveAllies().FirstOrDefault()?.FillUltimateResource();
                    ultimate = true;
                    VerifyEvents.Raise("action_order_ultimate");
                }
                if (battleStarted && !removed && elapsed >= 6.3f)
                {
                    AliveEnemies().LastOrDefault()?.DeactivateUnit();
                    removed = true;
                    VerifyEvents.Raise("action_order_remove");
                }

                yield return EndOfFrame;
                Capture("action_order");
                sample++;
            }

            if (!battleStarted || GameManager.Instance.gameState != BaseEnums.GameState.RoundInProgress)
                FailCurrent("행동서열 검증용 5:4 전투를 시작하지 못했다.");
        }

        private IEnumerator ScenarioTriggeredBattle(VerifyScenarioRequest config, int defaultStage, string defaultTrigger)
        {
            int marker = _eventSerial;
            yield return PrepareStage(config, Stage(config, defaultStage), true, true, true);
            string trigger = string.IsNullOrWhiteSpace(config?.trigger) ? defaultTrigger : config.trigger.Trim().ToLowerInvariant();
            yield return WaitForEvent(trigger, marker, Timeout(config));
            if (!_waitMatched)
            {
                FailCurrent($"'{trigger}' 이벤트가 {Timeout(config):0.#}초 안에 오지 않았다.");
                yield break;
            }
            yield return CaptureWindow(trigger, CaptureSeconds(config), EveryFrames(config));
        }

        private IEnumerator ScenarioSeiNormal(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            Unit sei = AliveAllies().FirstOrDefault(unit => unit.ID == SeiId);
            if (sei == null)
            {
                FailCurrent("세이를 찾지 못했다.");
                yield break;
            }

            // 전투 HUD와 5:다수 배치는 실제 경로로 만든 뒤 자동 행동만 멈춘다.
            // 세이 연발의 작은 투사체·분산 착탄을 다른 유닛 연출 없이 판독하기 위한 검수 전용 정지다.
            FreezeBattleForManualCapture();
            yield return CaptureActionWindowAtInterval("sei_normal",
                CaptureSeconds(config, 1.70f), 1f / 60f, () =>
                {
                    sei.CastNormalCode();
                    VerifyEvents.Raise("sei_normal");
                });
        }

        private IEnumerator ScenarioEnemyNormal(VerifyScenarioRequest config, int enemyId, int stage,
            string eventTag, bool replaceOneEnemy = false)
        {
            yield return PrepareStage(config, CombatStage(config, stage, stage), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);

            Unit enemy;
            if (replaceOneEnemy)
            {
                // 엘리트/보스 단독 프레임은 크기·방향 판단을 흐리므로 5:4 시험장 편성의 사수 한 장만 바꾼다.
                enemy = AliveEnemies().FirstOrDefault(candidate => candidate.ID == 1002)
                        ?? AliveEnemies().FirstOrDefault();
                enemy?.InitializeUnit(true, enemyId);
            }
            else
            {
                enemy = AliveEnemies().FirstOrDefault(candidate => candidate.ID == enemyId);
            }

            if (enemy == null || AliveAllies().Count != 5 || AliveEnemies().Count != 4)
            {
                FailCurrent($"적 {enemyId}의 5:4 검수 편성을 만들지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowRealtimeAtInterval(eventTag, CaptureSeconds(config, 1.85f),
                1f / 30f, () =>
                {
                    VerifyEvents.Raise(eventTag);
                    enemy.CastNormalCode();
                });
            yield return WaitForCasting(enemy, 2f);
        }

        private IEnumerator ScenarioEnemyUltimate(VerifyScenarioRequest config, int enemyId,
            string eventTag, bool replaceOneEnemy, float captureFallback)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 6), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);

            Unit enemy = AliveEnemies().FirstOrDefault(candidate => candidate.ID == enemyId);
            if (enemy == null && replaceOneEnemy)
            {
                enemy = AliveEnemies().FirstOrDefault(candidate => candidate.ID == 1002)
                        ?? AliveEnemies().FirstOrDefault();
                enemy?.InitializeUnit(true, enemyId);
            }

            if (enemy == null || AliveAllies().Count != 5 || AliveEnemies().Count != 4)
            {
                FailCurrent($"적 궁극기 {enemyId}의 5:4 검수 편성을 만들지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowRealtimeAtInterval(eventTag,
                CaptureSeconds(config, captureFallback), 1f / 30f, () =>
                {
                    VerifyEvents.Raise(eventTag);
                    enemy.FillUltimateResource();
                    enemy.CastUltimateCode();
                });
            yield return WaitForCasting(enemy, 2.5f);
        }

        private IEnumerator ScenarioRisingSpike(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            Unit sei = AliveAllies().FirstOrDefault(unit => unit.ID == SeiId);
            Unit target = AliveEnemies().FirstOrDefault();
            if (sei == null || target == null)
            {
                FailCurrent("보관 결정창 프리셋의 세이 또는 대상을 찾지 못했다.");
                yield break;
            }

            VerifyEvents.Raise("rising_spike");
            VfxRunner.Run(Effects.Vfx.Presets.RisingSpikeFx.Play(sei, target, () => { }, 1f));
            yield return CaptureWindowAtInterval("rising_spike", CaptureSeconds(config, 0.85f), 1f / 60f);
        }

        private static void FreezeBattleForManualCapture()
        {
            GameManager game = GameManager.Instance;
            game.gameState = BaseEnums.GameState.Preparation;
            game.RoundManager.StopRound();
            game.ActionScheduler.EndRound();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                unit.StopAllCoroutines();
                unit.isCasting = false;
            }
        }

        private IEnumerator ScenarioShiNormal(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit shi = AliveAllies().FirstOrDefault(unit => unit.ID == ShiId);
            if (shi == null || AliveAllies().Count != 5 || AliveEnemies().Count != 4)
            {
                FailCurrent("일반 멸참의 5:4 검수 편성을 만들지 못했다.");
                yield break;
            }

            int before = shi.GetCombatResource(Codes.Ultimate.ShiFinalBell.ResourceId);
            yield return CaptureActionWindowRealtimeAtInterval("shi_normal",
                CaptureSeconds(config, 2.05f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("shi_normal");
                    shi.CastNormalCode();
                });
            yield return WaitForCasting(shi, 2f);
            RecordMechanic("모든 멸참 사용 시 만종 +1", "1", 
                shi.GetCombatResource(Codes.Ultimate.ShiFinalBell.ResourceId).ToString(),
                shi.GetCombatResource(Codes.Ultimate.ShiFinalBell.ResourceId) == before + 1);
        }

        private IEnumerator ScenarioShiBell(VerifyScenarioRequest config, int tier)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit shi = AliveAllies().FirstOrDefault(unit => unit.ID == ShiId);
            var bell = shi?.ActiveUltimateCode as Codes.Ultimate.ShiFinalBell;
            if (shi == null || bell == null || AliveAllies().Count != 5 || AliveEnemies().Count != 4)
            {
                FailCurrent($"{tier}타 만종의 5:4 검수 편성을 만들지 못했다.");
                yield break;
            }

            bell.DebugSetStack(tier - 1);
            List<Unit> bellTargets = AliveEnemies();
            Dictionary<Unit, int> hpBefore = bellTargets.ToDictionary(unit => unit, unit => unit.HpCurr);
            shi.currentNormalTarget = bellTargets.FirstOrDefault();
            string tag = $"shi_bell_{tier}";
            yield return CaptureActionWindowRealtimeAtInterval(tag,
                CaptureSeconds(config, 2.55f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise(tag);
                    shi.CastNormalCode();
                });
            yield return WaitForCasting(shi, 2f);
            int expectedStack = tier == 9 ? 0 : tier;
            int actualStack = shi.GetCombatResource(Codes.Ultimate.ShiFinalBell.ResourceId);
            RecordMechanic($"{tier}타 만종 중첩 순환", expectedStack.ToString(), actualStack.ToString(),
                actualStack == expectedStack);
            int damaged = bellTargets.Count(unit => unit != null && unit.HpCurr < hpBefore[unit]);
            int expectedDamaged = tier == 3 ? 1 : bellTargets.Count;
            RecordMechanic($"{tier}타 만종 피해 범위", tier == 3 ? "멸참 대상 1명" : $"생존 적 전체 {expectedDamaged}명",
                $"피해 대상 {damaged}명", damaged == expectedDamaged);
            RecordMechanic($"{tier}타 만종 행동서열 분류", "궁극기(초월기) — 만종",
                $"{Combat.ActionAnnouncement.LabelFor(_lastAnnouncementKind)} — {_lastAnnouncementName}",
                _lastAnnouncementKind == Combat.ActionAnnouncementKind.Ultimate &&
                string.Equals(_lastAnnouncementName, "만종", StringComparison.Ordinal));
        }

        private IEnumerator ScenarioShiTraitExecute(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit shi = AliveAllies().FirstOrDefault(unit => unit.ID == ShiId);
            Unit ally = AliveAllies().FirstOrDefault(unit => unit != shi);
            Unit target = AliveEnemies().FirstOrDefault();
            var slash = shi?.ActiveNormalCode as Codes.Normal.ShiGhostSlash;
            var passive = shi?.ActivePassiveCodes.OfType<Codes.Passive.ShiFinalVerse>().FirstOrDefault();
            if (shi == null || ally == null || target == null || slash == null || passive == null ||
                AliveAllies().Count != 5 || AliveEnemies().Count != 4)
            {
                FailCurrent("멸망의 시의 5:4 검수 편성을 만들지 못했다.");
                yield break;
            }

            target.ModifyHp(Mathf.Min(target.HpMax, slash.ExecutionEstimate(target) + 1));
            yield return CaptureActionWindowRealtimeAtInterval("shi_trait_execute",
                CaptureSeconds(config, 2.50f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("shi_trait_execute");
                    target.TakeDamage(new DamageContext(ally, 1, BaseEnums.CodeType.Normal,
                        new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack }));
                    bool queued = passive.DebugHasExecuteQueued;
                    bool ran = GameManager.Instance.ActionScheduler.DebugRunNextQueued();
                    RecordMechanic("아군 공격 후 처치각 예약", "예약됨", queued ? "예약됨" : "미예약", queued);
                    RecordMechanic("멸망의 시 행동 분류", "Special(4)",
                        $"{_lastActionKind}({(int)_lastActionKind})", ran && _lastActionKind == ActionScheduler.ActionKind.Special);
                });
            yield return WaitForCasting(shi, 2f);
            RecordMechanic("멸망의 시 행동서열 표시", "특수행동 — 멸참",
                string.Join(", ", _announcements.Select(item =>
                    $"{Combat.ActionAnnouncement.LabelFor(item.Kind)} — {item.Name}")),
                _announcements.Any(item => item.Kind == Combat.ActionAnnouncementKind.Special &&
                    string.Equals(item.Name, "멸참", StringComparison.Ordinal)));
        }

        private IEnumerator ScenarioShiReaction(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit shi = AliveAllies().FirstOrDefault(unit => unit.ID == ShiId);
            List<Unit> enemies = AliveEnemies().Take(2).ToList();
            var passive = shi?.ActivePassiveCodes.OfType<Codes.Passive.ShiFinalVerse>().FirstOrDefault();
            if (shi == null || passive == null || enemies.Count < 2 ||
                AliveAllies().Count != 5 || AliveEnemies().Count != 4)
            {
                FailCurrent("멸참 재행동의 5:4 검수 편성을 만들지 못했다.");
                yield break;
            }

            enemies[0].ModifyHp(1);
            enemies[1].ModifyHp(1);
            shi.currentNormalTarget = enemies[0];
            yield return CaptureActionWindowRealtimeAtInterval("shi_reaction",
                CaptureSeconds(config, 4.05f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("shi_reaction");
                    StartCoroutine(RunShiReactionCheck(shi, passive));
                });
            yield return WaitForCasting(shi, 2f);
        }

        private IEnumerator RunShiReactionCheck(Unit shi, Codes.Passive.ShiFinalVerse passive)
        {
            shi.CastNormalCode();
            while (shi != null && shi.isCasting) yield return null;
            bool queued = passive.DebugHasRepriseQueued;
            bool ran = GameManager.Instance.ActionScheduler.DebugRunNextQueued();
            RecordMechanic("멸참 처치 후 재행동", "Special 1회 예약", queued && ran ? "Special 1회 예약" : "예약 실패",
                queued && ran && _lastActionKind == ActionScheduler.ActionKind.Special);
            while (shi != null && shi.isCasting) yield return null;
            int remaining = GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.Special);
            RecordMechanic("재행동 처치 연쇄 제한", "추가 예약 0", $"추가 예약 {remaining}",
                remaining == 0 && !passive.DebugHasRepriseQueued);
            RecordMechanic("재행동 행동서열 표시", "특수행동 — 멸참",
                $"{Combat.ActionAnnouncement.LabelFor(_lastAnnouncementKind)} — {_lastAnnouncementName}",
                _lastAnnouncementKind == Combat.ActionAnnouncementKind.Special &&
                string.Equals(_lastAnnouncementName, "멸참", StringComparison.Ordinal));
        }

        private IEnumerator ScenarioHeroNormal(VerifyScenarioRequest config, int unitId,
            string unitName, string eventTag, float defaultCaptureSeconds = 0.95f)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit unit = AliveAllies().FirstOrDefault(candidate => candidate.ID == unitId);
            if (unit == null)
            {
                FailCurrent($"{unitName}를 찾지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowAtInterval(eventTag,
                CaptureSeconds(config, defaultCaptureSeconds + 0.5f), 1f / 60f, () =>
                {
                    VerifyEvents.Raise(eventTag);
                    unit.CastNormalCode();
                });
            yield return WaitForCasting(unit, 2f);
        }

        private IEnumerator ScenarioJeanCounter(VerifyScenarioRequest config, bool multi)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit jean = AliveAllies().FirstOrDefault(unit => unit.ID == JeanId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            List<Unit> allies = AliveAllies();
            if (jean == null || attacker == null || allies.Count != 5 || AliveEnemies().Count < 3)
            {
                FailCurrent("잔 선제 반격의 5:다수 검수 편성을 만들지 못했다.");
                yield break;
            }

            int before = jean.GetCombatResource(Codes.Passive.JeanIronSanction.ResourceId);
            int counterDamageEvents = 0;
            bool compoundTags = false;
            Action<DamageResolvedContext> observe = context =>
            {
                if (context?.Attacker != jean || context.DamageContext == null ||
                    !context.DamageContext.HasTag(DamageTag.CounterAttack)) return;
                counterDamageEvents++;
                compoundTags = context.DamageContext.HasTag(DamageTag.NormalAttack) &&
                               context.DamageContext.HasTag(DamageTag.AdditionalAttack) &&
                               context.DamageContext.HasTag(DamageTag.CounterAttack);
            };
            Unit.AnyDamageDealt += observe;
            var probe = new JeanAttackProbe();
            List<Unit> selected = multi
                ? new List<Unit> { jean, allies.First(unit => unit.ID == ShiId), allies.First(unit => unit.ID == HimikoId) }
                : new List<Unit> { jean };
            string tag = multi ? "jean_counter_multi" : "jean_counter_single";
            yield return CaptureActionWindowRealtimeAtInterval(tag,
                CaptureSeconds(config, multi ? 3.15f : 2.85f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise(tag);
                    VfxRunner.Run(DebugJeanDirectAttack(attacker, selected, multi, tag, probe));
                });
            yield return WaitForJeanProbe(probe, 2f);
            Unit.AnyDamageDealt -= observe;

            int after = jean.GetCombatResource(Codes.Passive.JeanIronSanction.ResourceId);
            RecordMechanic(multi ? "다중 공격당 선제 반격 횟수" : "단일 공격 선제 반격 횟수",
                "1회 / 자원 -1", $"피해 이벤트 {counterDamageEvents}회 / {before}->{after}",
                counterDamageEvents == 1 && after == before - 1);
            RecordMechanic("철권제재 복합 태그", "일반행동+추가공격+반격",
                compoundTags ? "세 태그 모두 확인" : "태그 누락", compoundTags);
            RecordMechanic("철권제재 행동서열 표시", "패시브(특성) — 철권제재",
                string.Join(", ", _announcements.Select(item =>
                    $"{Combat.ActionAnnouncement.LabelFor(item.Kind)} — {item.Name}")),
                _announcements.Count(item => item.Kind == Combat.ActionAnnouncementKind.Passive &&
                    string.Equals(item.Name, "철권제재", StringComparison.Ordinal)) == 1);
            RecordMechanic("원래 공격 후속", "반격 뒤 1회 진행",
                probe.OriginalApplied ? "진행" : "취소", probe.OriginalApplied && !probe.CanceledBeforeOriginal);
        }

        private IEnumerator ScenarioJeanCounterKillCancel(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit jean = AliveAllies().FirstOrDefault(unit => unit.ID == JeanId);
            Unit bastet = AliveAllies().FirstOrDefault(unit => unit.ID == BastetId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            if (jean == null || bastet == null || attacker == null)
            {
                FailCurrent("선제 처치 취소 시험에 잔·바스테트·적이 모두 필요하다.");
                yield break;
            }

            attacker.ModifyHp(1);
            var probe = new JeanAttackProbe();
            yield return CaptureActionWindowRealtimeAtInterval("jean_counter_kill_cancel",
                CaptureSeconds(config, 2.85f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("jean_counter_kill_cancel");
                    VfxRunner.Run(DebugJeanDirectAttack(attacker, new[] { jean }, false,
                        "jean_kill_cancel", probe));
                });
            yield return WaitForJeanProbe(probe, 2f);
            int queued = GameManager.Instance.ActionScheduler.DebugQueuedCount(ActionScheduler.ActionKind.PriorityAdditional);
            RecordMechanic("선제 처치 원래 공격 취소", "취소", probe.CanceledBeforeOriginal ? "취소" : "진행",
                probe.CanceledBeforeOriginal && !probe.OriginalApplied && !attacker.isActive);
            RecordMechanic("선제 처치 U122 추격 취소", "예약 0", $"예약 {queued}", queued == 0);
        }

        private IEnumerator ScenarioJeanCounterControlCancel(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit jean = AliveAllies().FirstOrDefault(unit => unit.ID == JeanId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            if (jean == null || attacker == null)
            {
                FailCurrent("선제 제어 취소 시험의 잔 또는 적을 찾지 못했다.");
                yield break;
            }

            var probe = new JeanAttackProbe();
            yield return CaptureActionWindowRealtimeAtInterval("jean_counter_control_cancel",
                CaptureSeconds(config, 2.85f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("jean_counter_control_cancel");
                    VfxRunner.Run(DebugJeanControlledCancel(attacker, jean, probe));
                });
            yield return WaitForJeanProbe(probe, 2f);
            RecordMechanic("선제 개입 후 공격자 제어 취소", "원래 공격 취소",
                probe.CanceledBeforeOriginal ? "취소" : "진행",
                attacker.isControlled && probe.CanceledBeforeOriginal && !probe.OriginalApplied);
        }

        private IEnumerator ScenarioJeanBastetGuardCounter(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, false);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit jean = AliveAllies().FirstOrDefault(unit => unit.ID == JeanId);
            Unit bastet = AliveAllies().FirstOrDefault(unit => unit.ID == BastetId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            if (jean == null || bastet == null || attacker == null)
            {
                FailCurrent("경호→선제 반격 시험에 잔·바스테트·적이 모두 필요하다.");
                yield break;
            }

            int before = jean.GetCombatResource(Codes.Passive.JeanIronSanction.ResourceId);
            var probe = new JeanAttackProbe();
            yield return CaptureActionWindowRealtimeAtInterval("jean_bastet_guard_counter",
                CaptureSeconds(config, 4.10f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("jean_bastet_guard_counter");
                    VfxRunner.Run(DebugJeanDirectAttack(attacker, new[] { bastet }, false,
                        "jean_bastet_guard", probe, true));
                });
            yield return WaitForJeanProbe(probe, 2f);
            yield return WaitForCasting(bastet, 2f);
            int after = jean.GetCombatResource(Codes.Passive.JeanIronSanction.ResourceId);
            RecordMechanic("경호 교체 뒤 선제 개입", "바스테트→잔, 자원 -1",
                $"최종 {probe.FinalTarget?.UnitName ?? "없음"}, {before}->{after}",
                probe.FinalTarget == jean && after == before - 1);
            RecordMechanic("경호 반격 뒤 U122 추격", "원래 공격 뒤 추격 실행",
                probe.FollowUpStarted ? "실행" : "미실행", probe.OriginalApplied && probe.FollowUpStarted);
        }

        private IEnumerator ScenarioJeanResources(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit jean = AliveAllies().FirstOrDefault(unit => unit.ID == JeanId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            if (jean == null || attacker == null)
            {
                FailCurrent("철권제재 횟수 시험의 잔 또는 적을 찾지 못했다.");
                yield break;
            }

            string resource = Codes.Passive.JeanIronSanction.ResourceId;
            int maximum = jean.GetCombatResourceMaximum(resource);
            RecordMechanic("철권제재 기본 횟수 데이터", "3(하드캡 아님)", maximum.ToString(), maximum == 3);
            for (int i = 0; i < maximum; i++)
            {
                var probe = new JeanAttackProbe();
                yield return DebugJeanDirectAttack(attacker, new[] { jean }, false, $"jean_deplete_{i}", probe);
            }
            int empty = jean.GetCombatResource(resource);
            RecordMechanic("철권제재 횟수 소진", "0", empty.ToString(), empty == 0);
            VerifyEvents.Raise("jean_resource_zero");
            yield return CaptureWindowAtRealtimeInterval("jean_resource_zero", 0.75f, 1f / 30f);

            jean.currentNormalTarget = attacker;
            jean.CastNormalCode();
            yield return WaitForCasting(jean, 3f);
            int refilled = jean.GetCombatResource(resource);
            RecordMechanic("잔 일반행동 해결 시 횟수 회복", maximum.ToString(), refilled.ToString(), refilled == maximum);

            jean.SetCombatResourceMaximum(resource, 12, true);
            jean.AddCombatResource(resource, 12);
            VerifyEvents.Raise("jean_resource_12");
            yield return CaptureWindowAtRealtimeInterval("jean_resource_12", 0.85f, 1f / 30f);
            RecordMechanic("철권제재 두 자리 숫자 배지", "12", jean.GetCombatResource(resource).ToString(),
                jean.GetCombatResource(resource) == 12);
        }

        private IEnumerator ScenarioJeanUltimate(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return WaitUnscaled(0.85f);
            Unit jean = AliveAllies().FirstOrDefault(unit => unit.ID == JeanId);
            Unit enemy = AliveEnemies().FirstOrDefault();
            if (jean == null || enemy == null)
            {
                FailCurrent("텐션 오르는데! 시험의 잔 또는 적을 찾지 못했다.");
                yield break;
            }

            int priorityBefore = jean.Priority;
            yield return CaptureActionWindowRealtimeAtInterval("jean_ultimate",
                CaptureSeconds(config, 3.20f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("jean_ultimate");
                    jean.FillUltimateResource();
                    jean.CastUltimateCode();
                });
            yield return WaitForCasting(jean, 2f);
            Entities.Status.UnitStatus status = jean.ActiveStatuses.FirstOrDefault(item =>
                string.Equals(item.Key, "jean_tension_rising", StringComparison.Ordinal));
            if (status == null)
            {
                FailCurrent("텐션 오르는데! 상태가 적용되지 않았다.");
                yield break;
            }

            var additional = new DamageContext(jean, 100, BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.ContactAttack });
            var contact = new DamageContext(enemy, 100, BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack });
            var ranged = new DamageContext(enemy, 100, BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack });
            float outgoing = status.Effects.Aggregate(1f, (value, effect) => value *
                effect.EffectObject.OutgoingDamageModifier(jean, enemy, additional));
            float contactReceiving = status.Effects.Aggregate(1f, (value, effect) => value *
                effect.EffectObject.ReceivingDamageModifier(jean, contact));
            float rangedReceiving = status.Effects.Aggregate(1f, (value, effect) => value *
                effect.EffectObject.ReceivingDamageModifier(jean, ranged));
            RecordMechanic("텐션 타겟 우선도", "+1", $"{priorityBefore}->{jean.Priority}",
                jean.Priority == Mathf.Min(3, priorityBefore + 1));
            RecordMechanic("텐션 추가공격 피해", "x1.20", $"x{outgoing:0.00}", Mathf.Abs(outgoing - 1.20f) < 0.001f);
            RecordMechanic("텐션 접촉 피해 경감", "x0.80", $"x{contactReceiving:0.00}",
                Mathf.Abs(contactReceiving - 0.80f) < 0.001f);
            RecordMechanic("텐션 비접촉 피해 경감", "x0.90", $"x{rangedReceiving:0.00}",
                Mathf.Abs(rangedReceiving - 0.90f) < 0.001f);
            int[] turns = new int[4];
            turns[0] = status.RemainingTurns;
            for (int i = 1; i < turns.Length; i++)
            {
                status.OnOwnerTurn();
                turns[i] = status.RemainingTurns;
            }
            RecordMechanic("텐션 잔 자신의 3턴", "3→2→1→0", string.Join("→", turns),
                turns.SequenceEqual(new[] { 3, 2, 1, 0 }));
        }

        private static IEnumerator DebugJeanDirectAttack(Unit attacker, IReadOnlyList<Unit> selected, bool multi,
            string attackKey, JeanAttackProbe probe, bool runFollowUp = false)
        {
            var tags = new List<int>
            {
                multi ? DamageTag.MultiTarget : DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.ContactAttack,
            };
            Combat.AttackTargetResolution resolution = Combat.AttackTargetRouter.Prepare(attacker, selected,
                BaseEnums.CodeType.Normal, tags, attackKey);
            yield return resolution.ResolvePreAttack();
            probe.FinalTarget = resolution.Targets.FirstOrDefault();
            if (!resolution.CanContinue)
            {
                probe.CanceledBeforeOriginal = true;
                probe.Completed = true;
                yield break;
            }
            if (!Combat.Targeting.Announce(attacker, resolution.Targets))
            {
                probe.CanceledBeforeOriginal = true;
                probe.Completed = true;
                yield break;
            }

            foreach (Unit target in resolution.Targets.Where(unit => unit != null && unit.isActive).Distinct())
            {
                target.TakeDamage(new DamageContext(attacker, 1, BaseEnums.CodeType.Normal, new List<int>(tags)));
                probe.OriginalApplied = true;
            }
            if (runFollowUp)
                probe.FollowUpStarted = GameManager.Instance.ActionScheduler.DebugRunNextQueued();
            probe.Completed = true;
        }

        private static IEnumerator DebugJeanControlledCancel(Unit attacker, Unit jean, JeanAttackProbe probe)
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack };
            Combat.AttackTargetResolution resolution = Combat.AttackTargetRouter.Prepare(attacker, new[] { jean },
                BaseEnums.CodeType.Normal, tags, "jean_control_cancel");
            yield return resolution.ResolvePreAttack();
            probe.FinalTarget = resolution.Targets.FirstOrDefault();
            // 잔의 장래 제어 부가효과나 동료 연계가 반격 정점에 적을 멈췄을 때의 라우터 계약을 독립 검증한다.
            attacker.ApplyControlAtLeast(jean, 1);
            probe.CanceledBeforeOriginal = !resolution.CanContinue;
            probe.Completed = true;
        }

        private static IEnumerator WaitForJeanProbe(JeanAttackProbe probe, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (probe != null && !probe.Completed && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private IEnumerator ScenarioBastetGuard(VerifyScenarioRequest config, bool multi)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, false, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit bastet = AliveAllies().FirstOrDefault(unit => unit.ID == BastetId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            List<Unit> allies = AliveAllies();
            if (bastet == null || attacker == null || allies.Count != 5)
            {
                FailCurrent("바스테트 경호 5:4 편성을 만들지 못했다.");
                yield break;
            }

            List<Unit> selected = multi
                ? new List<Unit> { bastet, allies.First(unit => unit.ID == ShiId), allies.First(unit => unit.ID == HimikoId) }
                : new List<Unit> { bastet };
            string tag = multi ? "bastet_guard_multi" : "bastet_guard_single";
            yield return CaptureActionWindowRealtimeAtInterval(tag,
                CaptureSeconds(config, multi ? 2.25f : 2.0f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise(tag);
                    VfxRunner.Run(DebugDirectAttack(attacker, selected, multi));
                });
        }

        private static IEnumerator DebugDirectAttack(Unit attacker, IReadOnlyList<Unit> selected, bool multi)
        {
            var tags = new List<int>
            {
                multi ? DamageTag.MultiTarget : DamageTag.SingleTarget,
                DamageTag.NormalAttack,
                DamageTag.ContactAttack,
            };
            Combat.AttackTargetResolution resolution = Combat.AttackTargetRouter.Prepare(attacker, selected,
                BaseEnums.CodeType.Normal, tags, multi ? "경호 3인 시험" : "경호 단일 시험");
            yield return resolution.ResolvePreAttack();
            List<Unit> targets = resolution.Targets;
            if (!resolution.CanContinue) yield break;
            if (targets.Count == 0 || !Combat.Targeting.Announce(attacker, targets)) yield break;

            if (!multi)
            {
                Unit target = targets[0];
                yield return EnemyNormalFx.OriginalNormal(attacker, target, () =>
                    target.TakeDamage(new DamageContext(attacker, 1, BaseEnums.CodeType.Normal,
                        new List<int>(tags))));
                yield break;
            }

            VfxComposer.PlaySlot("enemy.void_marksman.shot", "cast", 0, attacker, targets[0],
                VfxSpace.WorldPoint(attacker), VfxSpace.SortingLayer(attacker));
            yield return new WaitForSeconds(0.25f);
            int pending = 0;
            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive))
            {
                Unit captured = target;
                pending++;
                VfxRunner.Run(CompleteHarnessRoutine(
                    VfxComposer.PlayHitSlot("enemy.void_marksman.shot", 0, attacker, captured, () =>
                        captured.TakeDamage(new DamageContext(attacker, 1, BaseEnums.CodeType.Normal,
                            new List<int>(tags)))), () => pending--));
            }
            while (pending > 0) yield return null;
        }

        private IEnumerator ScenarioBastetGuardDeathCancel(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, false, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit bastet = AliveAllies().FirstOrDefault(unit => unit.ID == BastetId);
            Unit attacker = AliveEnemies().FirstOrDefault();
            if (bastet == null || attacker == null)
            {
                FailCurrent("경호자 사망 취소 시험의 바스테트 또는 적을 찾지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowRealtimeAtInterval("bastet_guard_death_cancel",
                CaptureSeconds(config, 2.1f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("bastet_guard_death_cancel");
                    VfxRunner.Run(DebugGuardDeathCancel(attacker, bastet));
                });
        }

        private static IEnumerator DebugGuardDeathCancel(Unit attacker, Unit bastet)
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.ContactAttack };
            Combat.AttackTargetResolution resolution = Combat.AttackTargetRouter.Prepare(attacker,
                new[] { bastet }, BaseEnums.CodeType.Ultimate, tags, "경호 다단 사망 시험");
            yield return resolution.ResolvePreAttack();
            List<Unit> targets = resolution.Targets;
            if (!resolution.CanContinue) yield break;
            if (targets.Count == 0 || !Combat.Targeting.Announce(attacker, targets)) yield break;
            Unit guard = targets[0];
            if (guard.HpCurr > 1)
                guard.TakeDamage(new DamageContext(attacker, guard.HpCurr - 1, BaseEnums.CodeType.Effect,
                    new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage }));
            yield return new WaitForSeconds(0.30f);

            for (int hit = 0; hit < 3; hit++)
            {
                if (guard == null || !guard.isActive)
                {
                    VerifyEvents.Raise("bastet_remaining_hits_canceled");
                    yield break;
                }
                string hitKey = EnemyNormalFx.UsesGroundRake(attacker)
                    ? "enemy.void_wolf.charge"
                    : EnemyNormalFx.UsesHeavyStrike(attacker)
                        ? "enemy.void_crusher.strike"
                        : "enemy.void_vanguard.charge";
                yield return VfxComposer.PlayHitSlot(hitKey, 0, attacker, guard, () =>
                    guard.TakeDamage(new DamageContext(attacker, 10, BaseEnums.CodeType.Ultimate,
                        new List<int>(tags))));
                yield return new WaitForSeconds(0.08f);
            }
        }

        private IEnumerator ScenarioBastetCounterFollowUp(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, false, false);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit bastet = AliveAllies().FirstOrDefault(unit => unit.ID == BastetId);
            // 앞 시나리오에서 특정 영웅이 사망해도 반격 더미 검증 자체는 독립적으로 계속한다.
            Unit counterDummy = AliveAllies().FirstOrDefault(unit => unit != bastet);
            Unit target = AliveEnemies().FirstOrDefault();
            if (bastet == null || counterDummy == null || target == null)
            {
                FailCurrent("반격 더미 추가행동 시험 편성을 만들지 못했다.");
                yield break;
            }
            yield return CaptureActionWindowRealtimeAtInterval("bastet_counter_followup",
                CaptureSeconds(config, 2.4f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("bastet_counter_dummy");
                    target.TakeDamage(new DamageContext(counterDummy, 1, BaseEnums.CodeType.Normal,
                        new List<int> { DamageTag.SingleTarget, DamageTag.CounterAttack, DamageTag.ContactAttack }));
                    target.TakeDamage(new DamageContext(counterDummy, 1, BaseEnums.CodeType.Normal,
                        new List<int> { DamageTag.SingleTarget, DamageTag.CounterAttack, DamageTag.ContactAttack }));
                    GameManager.Instance.ActionScheduler.DebugRunNextQueued();
                });
            yield return WaitForCasting(bastet, 2f);
        }

        private IEnumerator ScenarioBastetPersistentHud(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit bastet = AliveAllies().FirstOrDefault(unit => unit.ID == BastetId);
            if (bastet == null || bastet.UltimateResourceType != BaseEnums.UltimateResourceType.Passive ||
                bastet.ManaCurr != bastet.ManaMax || bastet.CanCastUltimateCode())
            {
                FailCurrent("상시형 초월기 자원 또는 HUD 계약이 맞지 않는다.");
                yield break;
            }
            VerifyEvents.Raise("bastet_persistent_hud");
            yield return CaptureWindowAtRealtimeInterval("bastet_persistent_hud",
                CaptureSeconds(config, 1.15f), 1f / 30f);
        }

        private static IEnumerator CompleteHarnessRoutine(IEnumerator routine, Action done)
        {
            yield return routine;
            done?.Invoke();
        }

        private IEnumerator ScenarioSabahLightningSlash(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit sabah = AliveAllies().FirstOrDefault(candidate => candidate.ID == SabahId);
            if (sabah == null)
            {
                FailCurrent("사바흐를 찾지 못했다. lineup을 party5_sabah로 지정해야 한다.");
                yield break;
            }

            // 앞의 두 행동은 정상 코드 경로로 횟수만 쌓고, 세 번째 다중 대상 연출만 캡처한다.
            for (int i = 0; i < 2; i++)
            {
                sabah.CastNormalCode();
                yield return WaitForCasting(sabah, 2f);
                yield return new WaitForSeconds(0.08f);
            }
            yield return CaptureActionWindowAtInterval("sabah_lightning_slash",
                CaptureSeconds(config, 3.05f), 1f / 60f, () =>
                {
                    VerifyEvents.Raise("sabah_lightning_slash");
                    sabah.CastNormalCode();
                });
            yield return WaitForCasting(sabah, 3f);
        }

        private IEnumerator ScenarioChandraUltimate(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            Unit chandra = AliveAllies().FirstOrDefault(unit => unit.ID == ChandraId);
            if (chandra == null)
            {
                FailCurrent("찬드라를 찾지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowAtInterval("chandra_ult",
                CaptureSeconds(config, 3.45f), 1f / 60f, () =>
                {
                    VerifyEvents.Raise("chandra_ult");
                    chandra.FillUltimateResource();
                    chandra.CastUltimateCode();
                });
            yield return WaitForCasting(chandra, 3f);
        }

        private IEnumerator ScenarioHeroUltimate(VerifyScenarioRequest config, int unitId,
            string unitName, string eventTag, float defaultCaptureSeconds)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit unit = AliveAllies().FirstOrDefault(candidate => candidate.ID == unitId);
            if (unit == null)
            {
                FailCurrent($"{unitName}를 찾지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowRealtimeAtInterval(eventTag,
                CaptureSeconds(config, Mathf.Max(2.0f, defaultCaptureSeconds)), 1f / 30f, () =>
                {
                    VerifyEvents.Raise(eventTag);
                    unit.FillUltimateResource();
                    unit.CastUltimateCode();
                });
            yield return WaitForCasting(unit, 2f);
        }

        private IEnumerator ScenarioChandraPassive(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, false, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            Unit chandra = AliveAllies().FirstOrDefault(unit => unit.ID == ChandraId);
            Unit enemy = AliveEnemies().FirstOrDefault();
            var passive = chandra?.ActivePassiveCodes.OfType<Codes.Passive.ChandraNishakara>().FirstOrDefault();
            if (chandra == null || enemy == null || passive == null)
            {
                FailCurrent("찬드라·대상·니샤카라 특성 중 하나를 찾지 못했다.");
                yield break;
            }

            passive.GainMoonlight();
            yield return CaptureActionWindowRealtimeAtInterval("chandra_passive",
                CaptureSeconds(config, 2.0f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("chandra_passive");
                    chandra.TakeDamage(new DamageContext(enemy,
                        Mathf.Max(1, Mathf.CeilToInt(chandra.HpMax * 0.76f)), BaseEnums.CodeType.Normal,
                        new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.TrueDamage }));
                });
        }

        private IEnumerator ScenarioHimikoPassive(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            Unit himiko = AliveAllies().FirstOrDefault(unit => unit.ID == HimikoId);
            Unit target = AliveEnemies().FirstOrDefault();
            var passive = himiko?.ActivePassiveCodes.OfType<Codes.Passive.HimikoBlaze>().FirstOrDefault();
            if (himiko == null || target == null || passive == null)
            {
                FailCurrent("히미코·대상·맹화 특성 중 하나를 찾지 못했다.");
                yield break;
            }

            VerifyEvents.Raise("himiko_passive");
            passive.ApplyBlaze(target);
            passive.ApplyBlaze(target);
            passive.ApplyBlaze(target);
            yield return CaptureWindowAtInterval("himiko_passive", CaptureSeconds(config, 0.65f), 1f / 60f);
        }

        private IEnumerator ScenarioHimikoBlazeBurst(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            // 준비 중 이미 시작된 아군 행동 잔상이 끝난 뒤 표식만 보여 주어 폭발 인과를 오염시키지 않는다.
            yield return new WaitForSeconds(0.90f);
            Unit himiko = AliveAllies().FirstOrDefault(unit => unit.ID == HimikoId);
            Unit target = AliveEnemies().FirstOrDefault();
            var passive = himiko?.ActivePassiveCodes.OfType<Codes.Passive.HimikoBlaze>().FirstOrDefault();
            if (himiko == null || target == null || passive == null)
            {
                FailCurrent("히미코·대상·맹화 특성 중 하나를 찾지 못했다.");
                yield break;
            }

            // 두 표식을 먼저 보여 준 뒤 캡처 도중 세 번째를 넣어 표식→점화→폭발 인과를 한 창에서 읽는다.
            passive.ApplyBlaze(target);
            passive.ApplyBlaze(target);
            IEnumerator TriggerBurst()
            {
                // 영상 계약은 화면상 1배속의 실제 경과 시간 기준이므로 정확히 0.5초 프리롤을 둔다.
                yield return new WaitForSecondsRealtime(0.50f);
                VerifyEvents.Raise("himiko_blaze_burst");
                passive.ApplyBlaze(target);
            }

            VfxRunner.Run(TriggerBurst());
            yield return CaptureWindowAtInterval("himiko_blaze_burst",
                CaptureSeconds(config, 0.92f), 1f / 60f);
            if (!target.ActiveStatuses.Any(status => status.StatusName == "맹화 폭발"))
                FailCurrent("맹화 3중첩이 받는 피해 증가 디버프로 전환되지 않았다.");
        }

        private IEnumerator ScenarioPygmalionPassive(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            Unit pygmalion = AliveAllies().FirstOrDefault(unit => unit.ID == PygmalionId);
            if (pygmalion == null)
            {
                FailCurrent("피그말리온을 찾지 못했다.");
                yield break;
            }

            yield return CaptureActionWindowRealtimeAtInterval("pygmalion_passive",
                CaptureSeconds(config, 2.0f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("pygmalion_passive");
                    pygmalion.CastPassiveCode();
                });
        }

        private IEnumerator ScenarioEnemyBossUltimate(VerifyScenarioRequest config)
        {
            yield return ScenarioEnemyUltimate(config, 3001, "void_boss_ult", true, 2.35f);
        }

        private IEnumerator ScenarioChandraSpecial(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            yield return new WaitForSeconds(0.85f);
            FreezeBattleForManualCapture();
            Unit chandra = AliveAllies().FirstOrDefault(unit => unit.ID == ChandraId);
            if (chandra == null)
            {
                FailCurrent("찬드라를 찾지 못했다.");
                yield break;
            }

            // 현 로스터에는 문을 여는 유닛이 없으므로 검수에서만 SP 슬롯을 직접 호출한다.
            // 종료 뒤 묶음 정리를 실행해 치명타 임시 상태가 다음 시나리오로 새지 않게 한다.
            Combat.SpecialAction.Reset();
            yield return CaptureActionWindowAtInterval("chandra_special",
                CaptureSeconds(config, 6.50f), 1f / 60f, () =>
                {
                    VerifyEvents.Raise("chandra_special");
                    chandra.CastSpecialCode();
                });
            yield return WaitForCasting(chandra, 3f);
            Combat.SpecialAction.Reset();
        }

        private IEnumerator ScenarioUltimate(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 1), true, true, true);
            FreezeBattleForManualCapture();
            yield return new WaitForSeconds(0.85f);
            Unit sei = AliveAllies().FirstOrDefault(unit => unit.ID == SeiId);
            if (sei == null)
            {
                FailCurrent("세이를 찾지 못했다.");
                yield break;
            }

            // 다른 캐릭터 시나리오와 같은 실제 0.5초 프리롤을 남겨 종합 영상의 시작 계약을 맞춘다.
            yield return CaptureActionWindowAtInterval("ultimate",
                CaptureSeconds(config, 2.20f), 1f / 60f, () =>
                {
                    VerifyEvents.Raise("ultimate");
                    sei.FillUltimateResource();
                    sei.CastUltimateCode();
                });
            yield return WaitForCasting(sei, 2f);
        }

        private IEnumerator ScenarioBigBang(VerifyScenarioRequest config)
        {
            // 실제 추가행동 본체를 프리롤 뒤 직접 시작해 첫 파쇄부터 후속 파편·보호막 봉합까지 한 창에 남긴다.
            yield return PrepareStage(config, Stage(config, 6), true, false, true);
            FreezeBattleForManualCapture();
            Unit sei = AliveAllies().FirstOrDefault(unit => unit.ID == SeiId);
            var passive = sei?.ActivePassiveCodes.OfType<Codes.Passive.SeiBigBang>().FirstOrDefault();
            if (sei == null || passive == null)
            {
                FailCurrent("세이 또는 빅뱅 특성을 찾지 못했다.");
                yield break;
            }

            // 후속 보호막의 전후를 읽을 수 있게 시작 전 기준 보호막을 전원에게 둔다.
            foreach (Unit ally in AliveAllies()) ally.AddShield(ally.HpMax * 100, sei);
            GeoFx.StoneShield(sei, 1.5f);
            bool started = false;
            yield return CaptureActionWindowRealtimeAtInterval("bigbang",
                CaptureSeconds(config, 5.6f), 1f / 30f, () =>
                {
                    VerifyEvents.Raise("bigbang");
                    started = passive.DebugReleaseExecutable();
                });
            if (!started) FailCurrent("빅뱅 추가행동 본체를 시작하지 못했다.");
            yield return WaitForCasting(sei, 3f);
        }

        private IEnumerator ScenarioDeath(VerifyScenarioRequest config, bool boss)
        {
            // 아군만 무적으로 두고 실제 피해 입구로 처치한다. 사망 판정·칸 해제·결과 지연을 우회하지 않아
            // 퇴장 사본이 실전과 같은 순서로 만들어지는지를 연속 프레임에서 확인할 수 있다.
            yield return PrepareStage(config, boss ? Stage(config, 10) : CombatStage(config, 6, 1),
                false, true, false);

            Unit attacker = AliveAllies().FirstOrDefault();
            Unit target = boss
                ? AliveEnemies().FirstOrDefault(unit => string.Equals(unit.UnitTier, "boss",
                    StringComparison.OrdinalIgnoreCase))
                : AliveEnemies().FirstOrDefault(unit => !string.Equals(unit.UnitTier, "boss",
                    StringComparison.OrdinalIgnoreCase));
            if (attacker == null || target == null)
            {
                FailCurrent(boss ? "사망 검증용 보스를 찾지 못했다." : "사망 검증용 일반 적을 찾지 못했다.");
                yield break;
            }

            GameManager.Instance.StartRound();
            yield return WaitForState(BaseEnums.GameState.RoundInProgress, 5f);
            if (GameManager.Instance.gameState != BaseEnums.GameState.RoundInProgress)
            {
                FailCurrent("퇴장 검증 전 전투 상태로 전환되지 않았다.");
                yield break;
            }

            // 전투 HUD와 라운드 시작 훅은 실제 경로로 만든 뒤, 검수 중 자동 행동·자동 정산만 멈춘다.
            // 피해·사망·처치·비활성화는 아래 실제 Unit API가 그대로 수행한다.
            GameManager.Instance.gameState = BaseEnums.GameState.Preparation;
            GameManager.Instance.RoundManager.StopRound();
            GameManager.Instance.ActionScheduler.EndRound();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                unit.StopAllCoroutines();
                unit.isCasting = false;
            }

            _latestVisualEvent = boss ? "death_boss" : "death";
            target.TakeDamage(new DamageContext(attacker, int.MaxValue / 4, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget,
                    DamageTag.PassiveAttack,
                    DamageTag.TrueDamage,
                }));
            yield return CaptureWindowAtInterval(boss ? "death_boss" : "death",
                CaptureSeconds(config, boss ? 1.05f : 0.82f), 0.03f);
        }

        private IEnumerator ScenarioFiveParty(VerifyScenarioRequest config)
        {
            if (_activeLineup != "party5") yield return BuildFreshRun("party5");
            yield return PrepareStage(config, Stage(config, 6), false, false, false);

            List<Unit> allies = AliveAllies();
            Unit enemy = AliveEnemies().FirstOrDefault();
            Unit shi = allies.FirstOrDefault(unit => unit.UnitName == "시");
            Unit sabah = allies.FirstOrDefault(unit => unit.UnitName == "사바흐");
            Unit chandra = allies.FirstOrDefault(unit => unit.UnitName == "찬드라");
            Unit himiko = allies.FirstOrDefault(unit => unit.UnitName == "히미코");
            Unit pygmalion = allies.FirstOrDefault(unit => unit.UnitName == "피그말리온");
            if (allies.Count != 5 || enemy == null || sabah == null || shi == null ||
                chandra == null || himiko == null || pygmalion == null)
            {
                FailCurrent($"5인 편성 또는 적을 만들지 못했다. allies={allies.Count}");
                yield break;
            }

            yield return CaptureStill("five_party");
            enemy.AddShield(50000, enemy);
            himiko.CastPassiveCode();
            pygmalion.CastPassiveCode();

            himiko.CastNormalCode();
            yield return CaptureWindow("himiko_ember", CaptureSeconds(config, 0.72f), EveryFrames(config, 2));
            yield return WaitForCasting(himiko, 2f);

            pygmalion.CastNormalCode();
            yield return CaptureWindow("pygmalion_ember", CaptureSeconds(config, 0.72f), EveryFrames(config, 2));
            yield return WaitForCasting(pygmalion, 2f);

            chandra.CastNormalCode();
            yield return CaptureWindow("chandra_sachi", CaptureSeconds(config, 0.74f), EveryFrames(config, 2));
            yield return WaitForCasting(chandra, 2f);

            shi.CastNormalCode();
            yield return CaptureWindow("shi_ghost_slash", CaptureSeconds(config, 0.76f), EveryFrames(config, 2));
            yield return WaitForCasting(shi, 2f);

            var blaze = himiko.ActivePassiveCodes.OfType<Codes.Passive.HimikoBlaze>().FirstOrDefault();
            blaze?.ApplyBlaze(enemy);
            blaze?.ApplyBlaze(enemy);
            blaze?.ApplyBlaze(enemy);
            if (!enemy.ActiveStatuses.Any(status => status.StatusName == "맹화 폭발"))
                FailCurrent("맹화 3중첩이 받는 피해 증가 디버프로 전환되지 않았다.");
            yield return CaptureWindow("himiko_blaze_burst", 0.48f, EveryFrames(config, 2));

            int priorityBefore = pygmalion.Priority;
            pygmalion.FillUltimateResource();
            pygmalion.CastUltimateCode();
            yield return CaptureWindow("pygmalion_thorns", 0.62f, EveryFrames(config, 2));
            yield return WaitForCasting(pygmalion, 2f);
            if (pygmalion.Priority != Mathf.Min(3, priorityBefore + 1))
                FailCurrent("날카로운 가시의 타겟 우선도 +1이 반영되지 않았다.");

            int hpBefore = pygmalion.HpCurr;
            pygmalion.TakeDamage(new DamageContext(enemy, 200, BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack }));
            if (!enemy.HasStatus(1)) FailCurrent("접촉 공격자에게 맹독이 부여되지 않았다.");
            if (pygmalion.HpCurr >= hpBefore) FailCurrent("날카로운 가시 피격 검증에서 피해가 처리되지 않았다.");

            himiko.FillUltimateResource();
            himiko.CastUltimateCode();
            yield return CaptureWindow("himiko_focus", 0.62f, EveryFrames(config, 2));
            yield return WaitForCasting(himiko, 2f);
            if (!allies.All(unit => unit.ActiveStatuses.Any(status => status.StatusName == "맹화의 시야")))
                FailCurrent("히미코 집중의 아군 전체 방어 무시 상태가 누락됐다.");
        }

        private IEnumerator ScenarioEntry(VerifyScenarioRequest config)
        {
            DebugMode.ResetAll();
            ApplyOverrides(config, false, false, 1f);
            int marker = _eventSerial;
            GameManager.Instance.DebugLoadStage(Stage(config, 2));
            SetPartyLevel(config);
            yield return WaitForEvent("entry", marker, Timeout(config));
            if (!_waitMatched)
            {
                FailCurrent("등장 연출이 제한 시간 안에 시작되지 않았다.");
                yield break;
            }
            yield return CaptureWindow("entry", CaptureSeconds(config, 1.1f), EveryFrames(config));
        }

        private IEnumerator ScenarioStatusPreview(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 6), false, false, false);
            VerifyEvents.Raise("status_preview");
            List<Unit> allies = GridManager.Instance?.heroList?
                .Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList() ?? new List<Unit>();
            List<Unit> enemies = GridManager.Instance?.enemyList?
                .Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList() ?? new List<Unit>();
            if (allies.Count == 0 || enemies.Count == 0)
            {
                FailCurrent("상태 연출 미리보기에 쓸 아군과 적이 없다.");
                yield break;
            }

            StatusPreviewStep[] steps =
            {
                new("버프 부여", "buff", "hit.support.buff", true),
                new("정화", "cleanse", "hit.support.cleanse", true),
                new("회복", "heal", "hit.support.heal", true),
                new("보호막", "shield", "hit.support.shield", true),
                new("디버프", "debuff", "affected.debuff.fall", false),
                new("둔화", "slow", "affected.debuff.slow", false),
                new("표식", "mark", "affected.debuff.mark", false),
                new("기절", "stun", "affected.control.stun", false),
                new("속박", "bind", "affected.control.bind", false),
                new("화상 틱", "burn", "affected.dot.burn", false),
                new("감전 틱", "shock", "affected.dot.shock", false),
            };

            Text label = CreateStatusPreviewLabel(out GameObject labelRoot);
            bool sequenceFinished = false;
            StartCoroutine(PlayStatusPreviewSequence(steps, allies[0], enemies[0], label,
                () => sequenceFinished = true));
            yield return CaptureWindowAtRealtimeInterval("status_preview",
                CaptureSeconds(config, 12.2f), 0.03f);
            // 캡처 종료와 마지막 0.7초 정리 대기가 같은 시각에 맞물리면 라벨을 먼저 지워
            // 실행 중 코루틴이 파괴된 Text를 만질 수 있다. 시퀀스의 짧은 꼬리를 끝낸 뒤 정리한다.
            float finishDeadline = Time.realtimeSinceStartup + 1.5f;
            while (!sequenceFinished && Time.realtimeSinceStartup < finishDeadline)
                yield return null;
            if (!sequenceFinished) FailCurrent("상태 연출 순차 미리보기가 캡처 시간 안에 끝나지 않았다.");
            if (labelRoot != null) Destroy(labelRoot);
        }

        private IEnumerator PlayStatusPreviewSequence(IReadOnlyList<StatusPreviewStep> steps,
            Unit ally, Unit enemy, Text label, Action onComplete)
        {
            const string key = "debug_vfx_sequence";
            yield return new WaitForSecondsRealtime(0.50f);
            for (int i = 0; i < steps.Count; i++)
            {
                StatusPreviewStep step = steps[i];
                Unit target = step.Beneficial ? ally : enemy;
                label.text = $"상태 미리보기  ·  {step.Label}";
                VerifyEvents.Raise("status_preview_" + step.EventTag);
                DebugOverlay.PreviewSingleStatusVfx(target, 992000 + i, key, step.Label,
                    step.Beneficial ? BaseEnums.StatusCategory.Positive : BaseEnums.StatusCategory.Negative,
                    step.Beneficial, step.PartId);
                yield return new WaitForSecondsRealtime(0.90f);
                target.RemoveStatusByKey(key);
                label.text = string.Empty;
                yield return new WaitForSecondsRealtime(0.10f);
            }
            yield return new WaitForSecondsRealtime(0.70f);
            onComplete?.Invoke();
        }

        private static Text CreateStatusPreviewLabel(out GameObject root)
        {
            root = new GameObject("StatusPreviewLabel", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -72f);
            panelRect.sizeDelta = new Vector2(520f, 72f);
            panel.GetComponent<Image>().color = new Color(0.10f, 0.16f, 0.22f, 0.90f);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 8f);
            textRect.offsetMax = new Vector2(-24f, -8f);
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private IEnumerator ScenarioRewardPreview(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 2), false, false, false);
            List<RewardDef> rewards = GameManager.Instance?.dataManager?.FetchRewardDataList()?.rewards?
                .Where(reward => reward != null)
                .Take(3)
                .ToList() ?? new List<RewardDef>();
            if (rewards.Count == 0)
            {
                FailCurrent("보상 미리보기에 쓸 보상 데이터가 없다.");
                yield break;
            }

            GameManager.Instance.gameState = BaseEnums.GameState.RewardSelection;
            GameManager.Instance.uiManager?.ShowRewardPanel(rewards);
            yield return WaitUnscaled(0.25f);
            yield return CaptureStill("reward_preview");
        }

        private IEnumerator ScenarioResultPreview(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 2), false, false, false);
            GameManager game = GameManager.Instance;
            game.uiManager?.HideRewardPanel();
            var result = new BattleResultData
            {
                Victory = true,
                Reason = "적 전멸",
                StageLabel = "1-2",
                ThemeName = "공허의 시험장",
                LifeBefore = 20,
                LifeAfter = 20,
                GoldGained = 84,
                ExpGained = 120,
                Kills = 2,
                TokensGained = 1,
                NextStep = "보상 선택",
            };
            result.Party.Add(new BattleResultData.Member("세이", "SEI", false));
            result.Damage.Add(new BattleResultData.DamageLine("세이", "SEI", 1834, false));
            game.uiManager?.ShowBattleResult(result, () => { });
            // 결과 화면은 1.4초 판정 띠 뒤에 본문을 연다. 전환까지 포함해 정지 화면을 남긴다.
            yield return WaitUnscaled(1.75f);
            yield return CaptureStill("result_preview");
        }

        private IEnumerator ScenarioCommonSfx(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 6), false, false, false);
            VerifyEvents.Raise("common_sfx");
            string[] cues =
            {
                "common.hit.contact", "common.death", "common.entry", "common.shield",
                "common.status.positive", "common.status.negative", "common.ultimate.ready", "common.crit",
            };
            foreach (string cue in cues)
            {
                AudioManager.PlayCue(cue);
                yield return WaitUnscaled(0.05f);
            }
            yield return CaptureStill("common_sfx");
        }

        private IEnumerator ScenarioUiSfx(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, Stage(config, 2), false, false, false);
            List<RewardDef> rewards = GameManager.Instance?.dataManager?.FetchRewardDataList()?.rewards?
                .Where(reward => reward != null).Take(3).ToList() ?? new List<RewardDef>();
            if (rewards.Count > 0) GameManager.Instance.uiManager?.ShowRewardPanel(rewards);
            VerifyEvents.Raise("ui_sfx");
            string[] cues =
            {
                "ui.click", "ui.confirm", "ui.cancel", "ui.reward.select",
                "ui.battle.start", "ui.battle.victory", "ui.battle.defeat",
            };
            foreach (string cue in cues)
            {
                AudioManager.PlayCue(cue);
                yield return WaitUnscaled(0.08f);
            }
            yield return CaptureStill("ui_sfx");
        }

        private IEnumerator ScenarioBackdropTransition(VerifyScenarioRequest config)
        {
            string targetPath = BackgroundOverridePath(config);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                FailCurrent("배경 전환 미리보기에는 backgroundOverridePath가 필요하다.");
                yield break;
            }

            // 먼저 테마 기본 배경을 안정화한 뒤 외부 PNG로 교체해 0.45초 전환 전 구간을 남긴다.
            BattlefieldBackdrop.SetVerificationBackgroundOverride(null);
            DebugMode.ResetAll();
            DebugMode.AllyInvincible = true;
            DebugMode.EnemyInvincible = true;
            DebugMode.SetTimeScale(1f);
            GameManager.Instance.DebugLoadStage(Stage(config, 2));
            SetPartyLevel(config);
            yield return WaitForState(BaseEnums.GameState.Preparation, 5f);
            yield return WaitUnscaled(1.05f);

            BattlefieldBackdrop.SetVerificationBackgroundOverride(targetPath);
            yield return CaptureWindow("backdrop_transition", CaptureSeconds(config, 0.7f),
                EveryFrames(config, 1));
        }

        private IEnumerator ScenarioSpeed8(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 2), true, true, true, 8f);
            yield return WaitUnscaled(Warmup(config, 0.2f));
            yield return CaptureWindow("speed8", CaptureSeconds(config), EveryFrames(config, 1));
        }

        private IEnumerator ScenarioLayerIsolation(VerifyScenarioRequest config)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 2), true, true, true, 1f);
            yield return WaitUnscaled(0.2f);
            Time.timeScale = 0f;
            BattlefieldBackdrop.ClearFocus();
            UnitCardView[] views = FindObjectsByType<UnitCardView>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector2? primary = views.Length > 0 ? views[0].CenterWorld : null;
            Vector2? secondary = views.Length > 1 ? views[1].CenterWorld : null;
            BattlefieldBackdrop.PushFocus(0.8f, 0f, 10f, 0f,
                new Color(0.16f, 0.30f, 0.46f), primary, secondary);
            yield return null;
            DumpPortraitDiagnostics();

            (string Tag, string[] Hidden)[] passes =
            {
                ("all_visible", Array.Empty<string>()),
                ("hide_portrait", new[] { "portrait" }),
                ("hide_portrait_copy", new[] { "portrait_copy" }),
                ("hide_ground_shadow", new[] { "ground_shadow" }),
                ("hide_foot_marker", new[] { "foot_marker" }),
                ("hide_formation_marks", new[] { "formation_marks" }),
                ("hide_orb", new[] { "orb" }),
                ("hide_panel", new[] { "panel" }),
                // 이 두 키는 현행 카드에 렌더러가 없다. 기준 화면과 같음을 캡처해 부재도 증명한다.
                ("hide_spot_mask", new[] { "spot_mask" }),
                ("hide_tone_down", new[] { "tone_down" }),
                ("hide_all_card_layers", new[] { "portrait", "ground_shadow", "foot_marker", "orb", "panel" }),
                ("hide_all_card_and_formation", new[] { "portrait", "ground_shadow", "foot_marker", "orb", "panel", "formation_marks" }),
                ("hide_all_card_and_spot", new[] { "portrait", "ground_shadow", "foot_marker", "orb", "panel", "spot_mask" }),
                ("hide_all_card_and_tone", new[] { "portrait", "ground_shadow", "foot_marker", "orb", "panel", "tone_down" }),
                ("hide_focus_all", new[] { "focus_all" }),
                ("hide_all_card_and_focus_all", new[] { "portrait", "ground_shadow", "foot_marker", "orb", "panel", "focus_all" }),
                ("hide_card_roots", new[] { "card_root" }),
                ("hide_card_roots_and_formation", new[] { "card_root", "formation_marks" }),
            };

            foreach ((string tag, string[] hidden) in passes)
            {
                SetCardDiagnosticLayers(hidden);
                yield return CaptureStill(tag);
            }

            SetCardDiagnosticLayers(config?.hiddenCardLayers);
            Time.timeScale = 1f;
        }

        private IEnumerator PrepareStage(VerifyScenarioRequest config, int stage, bool startBattle,
            bool allyInvincible, bool enemyInvincible, float defaultSpeed = 1f)
        {
            DebugMode.ResetAll();
            ApplyOverrides(config, allyInvincible, enemyInvincible, defaultSpeed);
            GameManager.Instance.DebugLoadStage(Mathf.Max(1, stage));
            SetPartyLevel(config);
            yield return WaitForState(BaseEnums.GameState.Preparation, 5f);
            yield return WaitUnscaled(1.05f);
            ApplyCardDiagnostics(config);

            if (!startBattle) yield break;
            GameManager.Instance.StartRound();
            yield return WaitForState(BaseEnums.GameState.RoundInProgress, 5f);
            if (GameManager.Instance.gameState != BaseEnums.GameState.RoundInProgress)
                FailCurrent("전투 상태로 전환되지 않았다.");
            ApplyCardDiagnostics(config);
        }

        private void ApplyCardDiagnostics(VerifyScenarioRequest config)
        {
            SetCardDiagnosticLayers(config?.hiddenCardLayers);
            if (config?.dumpPortraitCopies == true) DumpPortraitDiagnostics();
        }

        private static void SetCardDiagnosticLayers(IEnumerable<string> hiddenLayers)
        {
            var hidden = new HashSet<string>(hiddenLayers ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            foreach (UnitCardView view in FindObjectsByType<UnitCardView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                view.SetVerificationHiddenLayers(hidden);
                view.gameObject.SetActive(!hidden.Contains("card_root"));
            }

            bool hideSpots = hidden.Contains("spot_mask") || hidden.Contains("focus_all");
            bool hideTone = hidden.Contains("tone_down") || hidden.Contains("focus_all");
            foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null) continue;
                if (renderer.gameObject.name is "CombatFocusSpotlight" or "CombatFocusSecondarySpotlight")
                    renderer.gameObject.SetActive(!hideSpots);
                else if (renderer.gameObject.name == "CombatFocusVignette")
                    renderer.gameObject.SetActive(!hideTone);
                else if (renderer.gameObject.name.StartsWith("FormationMark", StringComparison.Ordinal))
                    renderer.gameObject.SetActive(!hidden.Contains("formation_marks"));
            }
            if (hidden.Contains("focus_all")) BattlefieldBackdrop.ClearFocus();
        }

        private void DumpPortraitDiagnostics()
        {
            if (!_dumpedPortraitScenarios.Add(_currentScenario)) return;
            string directory = Path.Combine(_requestDirectory, "diagnostics", SafeName(_currentScenario));
            int index = 0;
            foreach (UnitCardView view in FindObjectsByType<UnitCardView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string sprite = view.PortraitSprite != null ? view.PortraitSprite.name : "portrait";
                view.DumpVerificationPortraitCopies(directory, $"{++index:00}_{sprite}");
            }
        }

        private void ApplyOverrides(VerifyScenarioRequest config, bool allyDefault, bool enemyDefault, float speedDefault)
        {
            BattlefieldBackdrop.SetVerificationBackgroundOverride(BackgroundOverridePath(config));
            DebugMode.AllyInvincible = config?.allyInvincible >= 0 ? config.allyInvincible == 1 : allyDefault;
            DebugMode.EnemyInvincible = config?.enemyInvincible >= 0 ? config.enemyInvincible == 1 : enemyDefault;
            DebugMode.SetTimeScale(config != null && config.timeScale > 0f ? config.timeScale : speedDefault);
        }

        private string BackgroundOverridePath(VerifyScenarioRequest config)
        {
            string path = !string.IsNullOrWhiteSpace(config?.backgroundOverridePath)
                ? config.backgroundOverridePath
                : _request.backgroundOverridePath;
            if (string.IsNullOrWhiteSpace(path)) return null;
            return Path.IsPathRooted(path) ? path : Path.Combine(_requestDirectory, path);
        }

        private void SetPartyLevel(VerifyScenarioRequest config)
        {
            int level = config?.partyLevel > 0 ? config.partyLevel : _request.partyLevel;
            if (level > 0) DebugOverlay.SetPartyLevel(level);
        }

        private IEnumerator WaitForState(BaseEnums.GameState state, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline && GameManager.Instance?.gameState != state)
                yield return null;
        }

        private IEnumerator WaitForEvent(string tag, int afterSerial, float timeout)
        {
            _waitMatched = false;
            string wanted = tag?.Trim().ToLowerInvariant();
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_events.Any(item => item.Serial > afterSerial && item.Tag == wanted))
                {
                    _waitMatched = true;
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator CaptureStill(string tag, bool keepTooltip = false)
        {
            yield return EndOfFrame;
            Capture(tag, keepTooltip);
            yield return null;
        }

        private IEnumerator CaptureWindow(string tag, float seconds, int everyFrames)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, seconds);
            int frame = 0;
            do
            {
                yield return EndOfFrame;
                if (frame % Mathf.Max(1, everyFrames) == 0) Capture(tag);
                frame++;
            } while (Time.realtimeSinceStartup < deadline);
        }

        /// <summary>짧은 퇴장을 첫 프레임부터 지정한 게임 시간 간격으로 잡는다.</summary>
        private IEnumerator CaptureWindowAtInterval(string tag, float seconds, float interval)
        {
            float step = Mathf.Max(0.01f, interval);
            int count = Mathf.CeilToInt(Mathf.Max(0.05f, seconds) / step) + 1;
            float startedAt = Time.time;
            for (int i = 0; i < count; i++)
            {
                // captureDeltaTime은 에디터의 실제 Update 간격을 고정하지 못한다.
                // 목표 시각을 기다려야 0.03초 접촉 시트가 0.56/0.80초 소실 뒤까지 온전히 담긴다.
                float targetTime = startedAt + i * step;
                while (Time.time + 0.0001f < targetTime) yield return null;
                yield return EndOfFrame;
                Capture(tag);
            }
        }

        /// <summary>상태 카탈로그처럼 게임 배속과 무관하게 실제 1초 단위로 읽어야 하는 검수용 캡처.</summary>
        private IEnumerator CaptureWindowAtRealtimeInterval(string tag, float seconds, float interval)
        {
            float step = Mathf.Max(0.01f, interval);
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + Mathf.Max(0.05f, seconds);
            float nextCaptureAt = startedAt;
            while (Time.realtimeSinceStartup <= deadline)
            {
                while (Time.realtimeSinceStartup + 0.0001f < nextCaptureAt) yield return null;
                if (Time.realtimeSinceStartup > deadline) break;
                yield return EndOfFrame;
                Capture(tag);
                // PNG 저장이 한 프레임보다 오래 걸리면 밀린 장수를 몰아 찍지 않고 실제 시간축의 다음 장으로 건너뛴다.
                nextCaptureAt = Mathf.Max(nextCaptureAt + step, Time.realtimeSinceStartup);
            }
        }

        /// <summary>사용자 검수 영상에 행동 직전 0.5초를 실제 시각으로 남긴 뒤 같은 창에서 시전한다.</summary>
        private IEnumerator CaptureActionWindowAtInterval(string tag, float seconds, float interval,
            Action action, float preRoll = 0.5f)
        {
            // 표시 1×에서도 엔진 Time.timeScale은 0.5이므로 사용자 영상의 프리롤·총길이는
            // 게임 시간이 아니라 실제 경과 시간을 기준으로 맞춘다.
            yield return CaptureActionWindowRealtimeAtInterval(tag, seconds, interval, action, preRoll);
        }

        /// <summary>
        /// 사용자 검수 영상은 표시 배속과 무관하게 실제 0.5초 프리롤과 30fps 이상 간격을 지킨다.
        /// 엔진 Time.timeScale이 1× HUD에서 0.5이므로 이 경로만 실시간 축을 사용한다.
        /// </summary>
        private IEnumerator CaptureActionWindowRealtimeAtInterval(string tag, float seconds, float interval,
            Action action, float preRoll = 0.5f)
        {
            float step = Mathf.Max(1f / 60f, interval);
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + Mathf.Max(preRoll + 0.05f, seconds);
            float nextCaptureAt = startedAt;
            bool acted = false;
            while (Time.realtimeSinceStartup <= deadline)
            {
                while (Time.realtimeSinceStartup + 0.0001f < nextCaptureAt) yield return null;
                if (!acted && Time.realtimeSinceStartup - startedAt + 0.0001f >= preRoll)
                {
                    acted = true;
                    action?.Invoke();
                }
                if (Time.realtimeSinceStartup > deadline) break;
                yield return EndOfFrame;
                Capture(tag);
                nextCaptureAt = Mathf.Max(nextCaptureAt + step, Time.realtimeSinceStartup);
            }
            if (!acted) action?.Invoke();
        }

        private void Capture(string baseTag, bool keepTooltip = false)
        {
            // 자동 캡처는 포인터 위치와 무관해야 한다. 매 프레임 호버가 다시 계산되므로 저장 직전에 닫는다.
            // 툴팁 자체를 검수하는 캡처만 고정 좌표(ShowAt)로 띄운 툴팁을 남긴다.
            if (!keepTooltip) Managers.UI.Core.UITooltip.HideAny();
            string scenarioDirectory = Path.Combine(_requestDirectory, "out", SafeName(_currentScenario));
            Directory.CreateDirectory(scenarioDirectory);
            string visualTag = string.IsNullOrWhiteSpace(_latestVisualEvent) ? "frame" : _latestVisualEvent;
            string tag = SafeName($"{baseTag}_{visualTag}");
            string extension = _currentScenario.StartsWith("v13", StringComparison.Ordinal) ? "rgba" : "png";
            string file = $"{++_captureSequence:0000}_{tag}.{extension}";
            string path = Path.Combine(scenarioDirectory, file);
            int frame = Time.frameCount;
            float realtime = Time.realtimeSinceStartup;
            if (_currentScenario.StartsWith("v13", StringComparison.Ordinal)) V13CaptureFrame(path);
            else ScreenCapture.CaptureScreenshot(path);
            _result.captures.Add(new VerifyCaptureRecord
            {
                scenario = _currentScenario,
                tag = tag,
                path = path,
                frame = frame,
                realtime = realtime,
                backgroundLuminance = BattlefieldBackdrop.CurrentBackgroundLuminance,
            });
            RecordPortraitRects(frame, realtime);
            RecordUiRects(frame, realtime);
            V13CaptureDiagnostics(path);
        }

        private void RecordPortraitRects(int frame, float realtime)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            foreach (UnitCardView view in FindObjectsByType<UnitCardView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Unit unit = view.BoundUnit;
                Rect rect = view.PortraitFocusScreenRect(camera);
                if (unit == null || rect.width <= 1f || rect.height <= 1f) continue;
                _result.portraitRects.Add(new VerifyPortraitRectRecord
                {
                    scenario = _currentScenario,
                    realtime = realtime,
                    frame = frame,
                    instanceId = unit.GetInstanceID(),
                    worldX = view.Body.position.x, worldY = view.Body.position.y,
                    unit = unit.UnitName,
                    xMin = rect.xMin,
                    yMin = rect.yMin,
                    xMax = rect.xMax,
                    yMax = rect.yMax,
                });
            }
            // 칸 없는 분신도 실제 표시 좌표를 기록해 이동·소멸 검수에 포함한다.
            foreach (Entities.View.SummonCardView view in FindObjectsByType<Entities.View.SummonCardView>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                SpriteRenderer portrait = view.PortraitRenderer;
                if (view.BoundUnit == null || portrait == null || !portrait.enabled) continue;
                Bounds bounds = portrait.bounds;
                Vector3 min = camera.WorldToScreenPoint(bounds.min);
                Vector3 max = camera.WorldToScreenPoint(bounds.max);
                _result.portraitRects.Add(new VerifyPortraitRectRecord
                {
                    scenario = _currentScenario, realtime = realtime, frame = frame,
                    instanceId = view.BoundUnit.GetInstanceID(),
                    worldX = view.transform.position.x, worldY = view.transform.position.y,
                    unit = view.BoundUnit.UnitName, xMin = min.x, yMin = min.y, xMax = max.x, yMax = max.y,
                });
            }
        }

        private void RecordUiRects(int frame, float realtime)
        {
            foreach (TextMeshProUGUI label in FindObjectsByType<TextMeshProUGUI>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (label == null || string.IsNullOrWhiteSpace(label.text)) continue;
                if (label.name is not ("Unit" or "Kind" or "Action" or "InsertMark")) continue;
                Transform row = label.transform;
                while (row != null && row.name != "InsertedAction") row = row.parent;
                if (row == null) continue;
                CanvasGroup details = label.GetComponentInParent<CanvasGroup>();
                if (details != null && details.alpha < 0.05f) continue;

                var corners = new Vector3[4];
                label.rectTransform.GetWorldCorners(corners);
                float xMin = corners.Min(point => point.x);
                float yMin = corners.Min(point => point.y);
                float xMax = corners.Max(point => point.x);
                float yMax = corners.Max(point => point.y);
                _result.uiRects.Add(new VerifyUiRectRecord
                {
                    scenario = _currentScenario,
                    realtime = realtime,
                    frame = frame,
                    groupId = row.gameObject.GetInstanceID(),
                    element = label.name,
                    text = label.text,
                    xMin = xMin,
                    yMin = yMin,
                    xMax = xMax,
                    yMax = yMax,
                });
            }
        }

        private IEnumerator WaitForCapturedFiles(float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool ready = _result.captures.All(item => File.Exists(item.path) && new FileInfo(item.path).Length > 0);
                if (ready) yield break;
                yield return null;
            }

            foreach (VerifyCaptureRecord capture in _result.captures)
            {
                if (!File.Exists(capture.path) || new FileInfo(capture.path).Length == 0)
                    _result.warnings.Add($"캡처 파일 쓰기를 확인하지 못했다: {capture.path}");
            }
        }

        private IEnumerator WaitUnscaled(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private void OnVerifyEvent(string tag)
        {
            _latestVisualEvent = tag;
            _events.Add((++_eventSerial, tag));
            _result?.visualEvents.Add(new VerifyVisualEventRecord
            {
                scenario = string.IsNullOrWhiteSpace(_currentScenario) ? "bootstrap" : _currentScenario,
                realtime = Time.realtimeSinceStartup,
                tag = tag,
                frame = Time.frameCount,
            });
            if (_events.Count > 256) _events.RemoveRange(0, 64);
        }

        private void OnSfxEvent(SfxPlaybackEvent item)
        {
            if (_result == null || item == null) return;
            _result.sfxEvents.Add(new VerifySfxRecord
            {
                scenario = string.IsNullOrWhiteSpace(_currentScenario) ? "bootstrap" : _currentScenario,
                realtime = item.realtime,
                cue = item.cue,
                clip = item.clip,
                mixerGroup = item.mixerGroup,
                played = item.played,
                reason = item.reason,
                volume = item.volume,
                pitch = item.pitch,
                timeScale = item.timeScale,
            });
        }

        private void OnVfxSlotPlayed(VfxSlotPlaybackEvent item)
        {
            if (_result == null || item == null) return;
            if (_currentScenario?.StartsWith("v13", StringComparison.Ordinal) == true)
                RecordPortraitRects(Time.frameCount, item.Realtime);
            if (_v13PeakDiagnostics && item.Phase == "peak" && item.SlotName is "hit" or "affected")
                _v13DiagnosticUntil = Time.realtimeSinceStartup +
                    (_currentScenario.StartsWith("v13yv_", StringComparison.Ordinal) && item.SlotName == "affected" ? 0.50f : 0.15f);
            Camera camera = Camera.main;
            Vector3 screen = camera != null ? camera.WorldToScreenPoint(item.WorldPoint) : Vector3.zero;
            _result.vfxSlots.Add(new VerifyVfxSlotRecord
            {
                scenario = string.IsNullOrWhiteSpace(_currentScenario) ? "bootstrap" : _currentScenario,
                realtime = item.Realtime,
                frame = Time.frameCount,
                skillKey = item.SkillKey,
                slot = item.SlotName,
                partId = item.PartId,
                phase = item.Phase,
                slotIndex = item.SlotIndex,
                casterInstanceId = item.Caster != null ? item.Caster.GetInstanceID() : 0,
                targetInstanceId = item.Target != null ? item.Target.GetInstanceID() : 0,
                caster = item.Caster != null ? item.Caster.UnitName : string.Empty,
                target = item.Target != null ? item.Target.UnitName : string.Empty,
                screenX = screen.x,
                screenY = screen.y,
                worldX = item.WorldPoint.x,
                worldY = item.WorldPoint.y,
                angle = item.Angle,
                mirrored = item.Mirrored,
                sortingLayer = UnityEngine.SortingLayer.IDToName(item.SortingLayerId),
                sortingOrder = item.SortingOrder,
            });
        }

        private static void OnTargetRedirected(Combat.AttackTargetRouter.RedirectEvent item)
        {
            if (item?.Protected == null || item.RedirectedTo == null) return;
            VerifyEvents.Raise($"guard_{item.Protected.UnitName}_to_{item.RedirectedTo.UnitName}");
        }

        private void OnActionStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            _lastActionKind = kind;
            _lastActionLabel = label;
            if (kind == ActionScheduler.ActionKind.Ultimate && unit != null && !unit.IsEnemy)
                VerifyEvents.Raise("ultimate");
            if (string.Equals(label, "빅뱅", StringComparison.Ordinal)) VerifyEvents.Raise("bigbang");
            if (string.Equals(label, "땅고르기", StringComparison.Ordinal)) VerifyEvents.Raise("sei_normal");
            if (string.Equals(label, "돌격", StringComparison.Ordinal) && unit != null && unit.IsEnemy)
                VerifyEvents.Raise("contact_action");
            if (string.Equals(label, "사격", StringComparison.Ordinal) && unit != null && unit.IsEnemy)
                VerifyEvents.Raise("void_bolt");
        }

        private void OnActionAnnounced(Combat.ActionAnnouncement item)
        {
            _lastAnnouncementKind = item.Kind;
            _lastAnnouncementName = item.ActionName;
            _announcements.Add((item.Kind, item.ActionName));
            VerifyEvents.Raise($"announce_{item.Kind}_{SafeName(item.ActionName)}");
        }

        private void RecordMechanic(string check, string expected, string actual, bool passed)
        {
            _result?.mechanics.Add(new VerifyMechanicRecord
            {
                scenario = string.IsNullOrWhiteSpace(_currentScenario) ? "bootstrap" : _currentScenario,
                check = check,
                expected = expected,
                actual = actual,
                passed = passed,
            });
            if (!passed) FailCurrent($"메커니즘 검증 실패 — {check}: 기대 {expected}, 실제 {actual}");
        }

        private IEnumerator ExecuteGuarded(IEnumerator root, Action<Exception> onException = null)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                bool moved;
                object yielded = null;
                try
                {
                    moved = current.MoveNext();
                    if (moved) yielded = current.Current;
                }
                catch (Exception exception)
                {
                    onException?.Invoke(exception);
                    if (onException == null) AddScenarioFailure(_currentScenario ?? "runner", exception.ToString());
                    yield break;
                }

                if (!moved)
                {
                    stack.Pop();
                    continue;
                }

                if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
        }

        private void FailCurrent(string message)
        {
            VerifyScenarioResult current = _result.scenarios.LastOrDefault();
            if (current == null) return;
            current.success = false;
            current.message = message;
            _result.success = false;
        }

        private void AddScenarioFailure(string name, string message)
        {
            _result.scenarios.Add(new VerifyScenarioResult
            {
                name = name,
                success = false,
                message = message,
            });
            _result.success = false;
        }

        private void FailLineupBuild(string message)
        {
            if (!string.IsNullOrWhiteSpace(_currentScenario) && _result?.scenarios?.Count > 0)
                FailCurrent(message);
            else
                AddScenarioFailure("bootstrap", message);
        }

        private void WriteRuntimeResult()
        {
            try
            {
                Directory.CreateDirectory(_requestDirectory);
                string path = Path.Combine(_requestDirectory, VerifyHarnessSession.RuntimeResultFile);
                File.WriteAllText(path, JsonUtility.ToJson(_result, true));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static List<Unit> AliveAllies()
            => GridManager.Instance?.heroList?.Where(unit => unit != null && unit.isActive && !unit.IsEnemy).ToList()
               ?? new List<Unit>();

        private static List<Unit> AliveEnemies()
            => GridManager.Instance?.enemyList?.Where(unit => unit != null && unit.isActive && unit.IsEnemy).ToList()
               ?? new List<Unit>();

        private static IEnumerator WaitForCasting(Unit unit, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (unit != null && unit.isCasting && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static int Stage(VerifyScenarioRequest config, int fallback)
            => config != null && config.stage > 0 ? config.stage : fallback;

        private int CombatStage(VerifyScenarioRequest config, int partyFiveFallback, int soloFallback)
        {
            if (config != null && config.stage > 0) return config.stage;
            return ResolveLineup(config) == "solo1" ? soloFallback : partyFiveFallback;
        }

        private string ResolveLineup(VerifyScenarioRequest config)
        {
            string value = !string.IsNullOrWhiteSpace(config?.lineup) ? config.lineup : _request.lineup;
            // 보상 UI 시나리오는 세이 · 인드라 · 쿠베라 · 아그니 · 수리야 편성이 기준이다. 시나리오에 편성을 따로 적지 않았으면 그것을 쓴다.
            if (string.IsNullOrWhiteSpace(config?.lineup) && IsRewardUiScenario(config?.name?.Trim().ToLowerInvariant()))
                value = "party5_reward_ui";
            return value?.Trim().ToLowerInvariant() switch
            {
                null or "" or "party5" or "five" => "party5",
                "party5_sabah" or "sabah5" => "party5_sabah",
                "party5_bastet" or "bastet5" => "party5_bastet",
                "party5_jean" or "jean5" => "party5_jean",
                "party5_jean_bastet" or "jean_bastet5" => "party5_jean_bastet",
                "party5_lokapala" or "lokapala5" => "party5_lokapala",
                "party5_summon" or "summon5" => "party5_summon",
                "party5_yama_varuna" or "yama_varuna5" => "party5_yama_varuna",
                "party5_reward_ui" or "reward_ui5" => "party5_reward_ui",
                "solo1" or "solo" or "sei" => "solo1",
                _ => value.Trim().ToLowerInvariant(),
            };
        }

        private float CaptureSeconds(VerifyScenarioRequest config, float fallback = 0f)
        {
            if (config != null && config.captureSeconds > 0f) return config.captureSeconds;
            if (fallback > 0f) return fallback;
            return _request.captureSeconds > 0f ? _request.captureSeconds : 0.9f;
        }

        private int EveryFrames(VerifyScenarioRequest config, int fallback = 0)
        {
            if (config != null && config.everyFrames > 0) return config.everyFrames;
            if (fallback > 0) return fallback;
            return _request.everyFrames > 0 ? _request.everyFrames : 3;
        }

        private float Timeout(VerifyScenarioRequest config)
            => _request.timeoutSeconds > 0f ? _request.timeoutSeconds : 30f;

        private SkillFxPlaybackMode ResolveVfxVariant(VerifyScenarioRequest config)
        {
            string value = !string.IsNullOrWhiteSpace(config?.vfxVariant)
                ? config.vfxVariant
                : _request.vfxVariant;
            return value?.Trim().ToLowerInvariant() switch
            {
                "legacy16" or "a" => SkillFxPlaybackMode.Legacy16,
                "blended16" or "b" => SkillFxPlaybackMode.Blended16,
                "hybrid32" or "c" => SkillFxPlaybackMode.Hybrid32,
                _ => SkillFxPlaybackMode.Final,
            };
        }

        private VfxStyle ResolveVfxStyle(VerifyScenarioRequest config)
        {
            string value = !string.IsNullOrWhiteSpace(config?.vfxStyle)
                ? config.vfxStyle
                : _request.vfxStyle;
            // 비교용 별칭은 요청마다 옵션을 되풀이하지 않아도 이름 그대로 화풍을 고정해야 한다.
            if (string.IsNullOrWhiteSpace(value))
            {
                if (_currentScenario.EndsWith("_legacy", StringComparison.Ordinal)) return VfxStyle.Legacy;
                if (_currentScenario.EndsWith("_handpaint", StringComparison.Ordinal)) return VfxStyle.Handpaint;
            }
            return value?.Trim().ToLowerInvariant() switch
            {
                "legacy" or "old" => VfxStyle.Legacy,
                "handpaint" or "handpainted" or "pack" => VfxStyle.Handpaint,
                _ => VfxStyle.CharacterDefault,
            };
        }

        private static float Warmup(VerifyScenarioRequest config, float fallback)
            => config != null && config.warmupSeconds > 0f ? config.warmupSeconds : fallback;

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "capture";
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray());
        }
    }
}
#endif
