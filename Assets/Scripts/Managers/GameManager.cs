using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using Managers.UI.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        private RoundManager _roundManager;

        /// <summary>전투 행동을 중앙에서 예약·직렬 실행하는 스케줄러.</summary>
        public ActionScheduler ActionScheduler { get; } = new();
        public GridManager gridManager;
        public UIManager uiManager;
        public SfxManager sfxManager;
        public InventoryManager inventoryManager;
        public DragAndDropManager dragAndDropManager;
        public RunManager runManager;
        public RewardManager rewardManager;
        public RoundManager RoundManager => _roundManager;
        public const int MaxTrainingStage = 100;
        public GameMode CurrentMode => runManager != null ? runManager.CurrentMode : GameMode.Training;
        public bool PreparationActionUsed => preparationActionUsed;

        public static void LoadMainMenuScene()
        {
            CleanupRunContext();
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        public static void LoadBattleScene()
        {
            if (Instance != null)
            {
                CleanupRunContext();
            }

            SceneManager.LoadScene(SceneNames.Game);
        }

        public GameState gameState;
        public int KillCount;

        // 준비 단계 타이머 관련
        public float preparationTime = 30f; // 준비 시간 (초)
        private float currentPreparationTime;
        private bool isPreparationTimerActive = false;

        // 라운드 진행 타이머 관련
        /// <summary>
        /// 라운드 제한 — <b>전역 턴 수</b>. 벽시계가 아니다.
        ///
        /// 전투가 턴제이므로 초로 재면 시전 딜레이·투사체 비행 같은 <b>연출 길이가
        /// 라운드당 행동 수를 좌우</b>한다. 연출을 손볼 때마다 밸런스가 흔들리므로 턴으로 센다.
        /// 참가 유닛이 많을수록 각자 얻는 턴은 줄어드는데, 보스전처럼 인원이 적은 전투에
        /// 더 많은 턴이 돌아가는 셈이라 의도한 성질이다.
        /// </summary>
        public int roundTurnLimit = 80;
        /// <summary>이번 라운드에 남은 턴 수.</summary>
        private int remainingRoundTurns;
        private bool isRoundProgressTimerActive = false;
        /// <summary>
        /// 현재 단계 타이머의 남은 비율(1 = 방금 시작, 0 = 시간 종료).
        /// HUD의 원형 프로그레스가 읽는다. 타이머가 돌지 않는 단계에서는 1을 반환한다.
        /// </summary>
        public float PhaseProgressRatio
        {
            get
            {
                if (isRoundProgressTimerActive && roundTurnLimit > 0)
                {
                    return Mathf.Clamp01(remainingRoundTurns / (float)roundTurnLimit);
                }

                if (isPreparationTimerActive && preparationTime > 0f)
                {
                    return Mathf.Clamp01(currentPreparationTime / preparationTime);
                }

                return 1f;
            }
        }

        /// <summary>
        /// HUD 게이지 옆에 띄울 숫자.
        /// 준비 단계에서는 <b>남은 초</b>, 전투 중에는 <b>남은 턴</b>이다.
        /// </summary>
        public int PhaseRemainingSeconds
        {
            get
            {
                if (isRoundProgressTimerActive) return Mathf.Max(0, remainingRoundTurns);
                if (isPreparationTimerActive) return Mathf.Max(0, Mathf.CeilToInt(currentPreparationTime));
                return 0;
            }
        }

        /// <summary>전투 중이면 true. HUD가 숫자 단위를 '턴'으로 바꿔 표시한다.</summary>
        public bool IsRoundTurnGauge => isRoundProgressTimerActive;

        private float eventStageEnteredAt;
        private float trainingPhaseEnteredAt;
        private StageEventData currentStageEvent;
        private StageEventChoiceData pendingEventChoice;

        /// <summary>
        /// 자리가 없어 <b>떠나보낼 사람을 고르는 중</b>인 영입 선택지. 고르거나 포기하면 비워진다.
        /// </summary>
        private StageEventChoiceData pendingRecruitChoice;
        private int eventDialogueIndex;
        private bool eventBattleInProgress;

        // 사건(이벤트) 시스템: 페이즈 사이 어디서든 사건을 끼워 넣기 위한 스케줄러와,
        // 현재 사건이 끝난 뒤 이어서 실행할 연속 동작(continuation).
        private readonly EventScheduler _eventScheduler = new EventScheduler();
        private Action _eventResumeAction;
        public EventScheduler EventScheduler => _eventScheduler;

        // 생명력 시스템
        public int life; // 현재 생명력

        // 아군 필드 상태 저장 (라운드 복원용)
        private List<UnitSaveData> allyFieldSnapshot;
        private bool preparationActionUsed;

        // 게임 데이터 관련
        public DataManager dataManager;
        public UnitDataList unitDataList;
        public ItemDataList itemDataList;
        public ResourceTokenDataList resourceTokenDataList;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                KillCount = 0;
                life = 20; // 생명력 초기화
                
                // 런타임에만 DontDestroyOnLoad 적용
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }

                EnsurePersistentManagers();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void NextGameState(bool isGameOver)
        {
            if (isGameOver)
            {
                gameState = GameState.GameOver;
            }
            else
            {
                switch (gameState)
                {
                    case GameState.CharacterSelection:
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                        break;
                    case GameState.Preparation:
                        StartRound();
                        break;
                    case GameState.RoundInProgress:
                        gameState = GameState.RoundEnd;
                        isPreparationTimerActive = false;
                        isRoundProgressTimerActive = false;
                        break;
                    case GameState.RoundEnd:
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                        break;
                    case GameState.RewardSelection:
                        gameState = GameState.Preparation;
                        StartPreparationTimer();
                        break;
                    case GameState.EventStage:
                        CompleteEventStage();
                        break;
                    case GameState.TrainingPhase:
                        // 육성 종료 직후(after training)는 사건 발생 지점이다.
                        RunEventCheckpoint(() => runManager?.AdvanceToNextStage());
                        break;
                    case GameState.RunComplete:
                        _eventScheduler.Clear();
                        SaveSystem.DeleteSave();
                        LoadMainMenuScene();
                        break;
                    case GameState.GameOver:
                        _eventScheduler.Clear();
                        SaveSystem.DeleteSave();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void StartPreparationTimer()
        {
            currentPreparationTime = preparationTime;
            isPreparationTimerActive = false;
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed);
        }

        public void StartRound()
        {
            if (!AllowProgressWhileWithinCarryLimit()) return;

            if (_roundManager != null && _roundManager.TryGetScheduledEvent(out StageEventData scheduledEvent))
            {
                EnterStageSlotEvent(scheduledEvent);
                return;
            }

            // 전투 시작 직전(before battle)은 사건 발생 지점이다.
            // 예약된 사건이 있으면 먼저 처리하고, 끝나면 실제 전투를 시작한다.
            RunEventCheckpoint(StartBattle);
        }

        private void StartBattle()
        {
            gameState = GameState.RoundInProgress;
            Cell.PlacementModeActive = false;
            isPreparationTimerActive = false;
            uiManager?.HidePreparationPhasePanel();

            // 드랍은 이번 전투에서 쓰러뜨린 적만 센다.
            _battleKilledEnemyIds.Clear();
            CaptureBattleStart();

            // 아군 필드 상태 저장 (라운드 종료 후 복원용)
            SaveAllyFieldState();
            runManager?.SaveCurrentRun();

            // 라운드 진행 타이머 시작
            remainingRoundTurns = roundTurnLimit;
            isRoundProgressTimerActive = true;

            Debug.LogWarning("[GameManager] 🔥 라운드 시작 - GridManager.OnRoundStart() 호출");
            GridManager.Instance.OnRoundStart();

            // 유닛이 모두 배치·활성화된 뒤에 행동치를 초기화해야 한다.
            ActionScheduler.BeginRound();
        }

        // ── 경험치 지급량 ──────────────────────────────────────────
        // 아군 레벨이 스테이지 진행과 대략 보조를 맞추도록 스테이지 비례로 준다.
        // 적 레벨 = 스테이지이므로, 이 곡선이 아군/적 격차를 결정한다.
        //
        // 두 갈래로 나뉜다.
        //   전투(처치·클리어) — 필드의 파티 전원이 받는다.
        //   훈련             — 메인과 <b>그 훈련에 서포트 카드로 앉은</b> 서포트만 받는다.
        // 무게를 훈련 쪽에 실어 서포트가 메인의 약 80% 레벨에 머물게 한다.
        // 메인의 곡선은 예전(전원 동일 지급)과 같다 — 100스테이지 Lv.86 · 50스테이지 Lv.44.
        // 서포트는 참여율에 따라 100스테이지 Lv.65~75(참여율 5~45%)에 선다.
        public const int ExpPerKillBase = 4;
        public const int ExpPerKillPerStage = 1;
        public const int ExpPerStageClearBase = 25;
        public const int ExpPerStageClearPerStage = 9;
        public const int ExpPerTrainingBase = 90;
        public const int ExpPerTrainingPerStage = 11;

        // ── 골드 수급 ──────────────────────────────────────────────
        // 소모품 가격이 스테이지에 비례하므로(RewardManager.ShopPrice) 수입도 스테이지에 비례한다.
        // 적 4기 기준 한 전투에 125 × 스테이지 + 50이 들어온다.
        public const int GoldPerKillPerStage = 25;
        public const int GoldPerClearBase = 50;
        public const int GoldPerClearPerStage = 25;

        // 전투 중 획득한 EXP는 즉시 주지 않고 모아 둔다.
        // 라운드 종료 시 아군 필드를 전투 시작 시점 스냅샷으로 되돌리므로,
        // 복원이 끝난 뒤에 지급해야 성장이 사라지지 않는다.
        private int pendingPartyExp;

        /// <summary>
        /// 활성 아군 전원에게 EXP를 지급한다.
        /// 선두주자(250)·음유시인(277)이 필드에 있으면 그만큼 배율이 붙는다.
        /// </summary>
        public void GrantExpToParty(int amount)
        {
            if (GridManager.Instance == null) return;
            GrantExpTo(GridManager.Instance.heroList, amount);
        }

        /// <summary>지정한 아군에게만 EXP를 지급한다. 배율 규칙은 <see cref="GrantExpToParty"/>와 같다.</summary>
        private static void GrantExpTo(IEnumerable<Unit> heroes, int amount)
        {
            if (amount <= 0 || heroes == null) return;

            int scaled = Mathf.Max(1, Mathf.RoundToInt(
                amount * Codes.Passive.RewardModifiers.ExpMultiplier()));

            foreach (Unit hero in heroes.Distinct())
            {
                if (hero == null || hero.IsEnemy || !hero.isActive) continue;
                hero.AddExp(scaled);
            }
        }

        private int CurrentStageForExp => Mathf.Max(1, _roundManager?.Stage ?? 1);

        /// <summary>이번 전투에서 처치한 적의 데이터 ID. 중복을 지우지 않는다 — 같은 적을 둘 잡아도 드랍표는 한 벌이다.</summary>
        private readonly List<int> _battleKilledEnemyIds = new();

        public IReadOnlyList<int> BattleKilledEnemyIds => _battleKilledEnemyIds;

        public void OnKillEnemy(Unit killed = null)
        {
            KillCount++;
            if (killed != null && killed.IsEnemy) _battleKilledEnemyIds.Add(killed.ID);
            // 상인(273)이 필드에 있으면 획득 골드가 늘어난다.
            int gold = Mathf.RoundToInt(
                GoldPerKillPerStage * Mathf.Max(1, _roundManager?.Stage ?? 1) *
                Codes.Passive.RewardModifiers.GoldMultiplier());
            inventoryManager?.AddGold(gold);
            pendingPartyExp += ExpPerKillBase + ExpPerKillPerStage * CurrentStageForExp;
            
            // 3의 배수 킬마다 토큰 보상 지급
            if (KillCount % 3 == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    int randomTokenId = UnityEngine.Random.Range(1, 9); // 1-8 사이의 무작위 tokenId
                    inventoryManager.AddToken(randomTokenId, 1);
                }
                Debug.Log($"킬 {KillCount}번째 달성! 무작위 토큰 5개를 지급했습니다.");
            }
        }

        private void Start()
        {
            gameState = GameState.Preparation;
            allyFieldSnapshot = new List<UnitSaveData>();
            sfxManager = GetComponent<SfxManager>();

            dataManager = GetComponent<DataManager>();
            unitDataList = dataManager.FetchUnitDataList();
            itemDataList = dataManager.FetchItemDataList();
            resourceTokenDataList = dataManager.FetchTokenDataList();

            gridManager.gameManager = this;
            gridManager.InitializeComponent();
            inventoryManager = GetComponent<InventoryManager>();
            inventoryManager.Initialize();

            _roundManager = new RoundManager(dataManager);
            StartFromIntent();
        }

        private void Update()
        {
            if (_roundManager != null && _roundManager.IsRoundInProgress)
            {
                _roundManager.UpdateRound();
            }

            // 전투 중에는 스케줄러가 행동 순서를 굴린다.
            // 유닛은 스스로 공격하지 않고 여기서 호출된 시점에만 행동한다.
            if (gameState == GameState.RoundInProgress)
            {
                ActionScheduler.Tick(Time.deltaTime);
            }

            // 준비 단계 타이머 처리
            if (gameState == GameState.Preparation && isPreparationTimerActive)
            {
                currentPreparationTime -= Time.deltaTime;
                
                // UI 업데이트 (음수가 되지 않도록 보정)
                int displayTime = Mathf.Max(0, Mathf.CeilToInt(currentPreparationTime));
                if (uiManager != null)
                {
                    uiManager.UpdateGameStatus(gameState, displayTime);
                }
                
                // 시간이 다 되면 자동 시작
                if (currentPreparationTime <= 0)
                {
                    StartRound();
                }
            }
            // 라운드 진행 중 타이머 처리
            else if (gameState == GameState.RoundInProgress && isRoundProgressTimerActive)
            {
                // 남은 턴은 스케줄러가 연 턴 수에서 역산한다. 벽시계는 보지 않는다.
                remainingRoundTurns = Mathf.Max(0, roundTurnLimit - ActionScheduler.TurnsTaken);

                // 남은 적 수 계산 (필드의 적 + 스폰 대기 중인 적)
                int remainingEnemies = GetRemainingEnemyCount();

                // 아군 전멸 체크
                if (AreAllAlliesDefeated())
                {
                    EndRoundByAllyDefeat();
                    return;
                }

                if (uiManager != null)
                {
                    uiManager.UpdateGameStatusWithEnemyCount(gameState, remainingRoundTurns, remainingEnemies);
                }

                // 턴을 다 쓰면 라운드 종료
                if (remainingRoundTurns <= 0)
                {
                    EndRoundByTimeout();
                }
            }
            else if (uiManager != null)
            {
                // 다른 상태일 때는 기본 상태만 표시
                if (gameState == GameState.RoundInProgress)
                {
                    // 라운드 진행 중이지만 타이머가 비활성화된 경우 (적 수만 표시)
                    int remainingEnemies = GetRemainingEnemyCount();
                    uiManager.UpdateGameStatusWithEnemyCount(gameState, 0, remainingEnemies);
                }
                else
                {
                    uiManager.UpdateGameStatus(gameState, 0);
                }
            }

            // 육성 페이즈는 키 입력이 아니라 집중 스탯 버튼 선택으로 진행한다
            // (UIManager 훈련 패널 -> GameManager.CompleteTrainingPhaseWithFocus).

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Core.DebugMode.SessionActive) Managers.UI.DevTools.DebugOverlay.EnsureBanner();
            if (!Core.DebugMode.SuiteRunning) HandleDebugInput();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>보상/사건/시전 중에도 다음 시나리오를 같은 출발 상태에서 시작한다.</summary>
        public void DebugResetBattle()
        {
            Core.DebugMode.BeginSession();
            bool restoreField = gameState == GameState.RoundInProgress && allyFieldSnapshot?.Count > 0;
            gameState = GameState.Preparation;
            StopAllCoroutines();
            _roundManager?.StopRound();
            ActionScheduler.EndRound();
            isPreparationTimerActive = isRoundProgressTimerActive = false;
            pendingPartyExp = 0;
            eventBattleInProgress = false;
            currentStageEvent = null;
            pendingEventChoice = null;
            pendingRecruitChoice = null;
            _eventResumeAction = null;
            _eventScheduler.Clear();
            _pendingTrainingResult = default;   // 값 형식이라 null을 넣을 수 없다
            preparationActionUsed = false;
            ShopPurchaseCount = 0;
            uiManager?.HidePreparationPhasePanel();
            uiManager?.HideRewardPanel();
            uiManager?.HideShopPanel();
            uiManager?.HideSkillPanel();
            uiManager?.HideTrainingPhasePanel();
            uiManager?.HideTrainingResultPanel();
            uiManager?.HideCharacterSelection();
            uiManager?.HideEventStagePanel();
            uiManager?.HideBattleResult();
            if (gridManager != null)
            {
                foreach (Unit unit in gridManager.heroList.Concat(gridManager.enemyList).Where(u => u != null).ToList())
                    unit.DebugResetCombatState();
                gridManager.OnRoundEnd();
                if (restoreField) RestoreAllyFieldState();
                gridManager.ClearActiveEnemies();
            }
            allyFieldSnapshot?.Clear();
        }

        public void DebugLoadStage(int stage)
        {
            if (_roundManager == null) return;
            DebugResetBattle();
            _roundManager.InitializeStage(Mathf.Max(1, stage));
            _roundManager.LoadRound(Mathf.Max(1, stage));
            EnterNextStageAfterLoad();
        }

        public void DebugEndBattle(bool victory)
        {
            Core.DebugMode.BeginSession();
            if (gameState != GameState.RoundInProgress)
            {
                Debug.Log("[디버그] 승패 처리는 전투 중에만 가능합니다.");
                return;
            }
            // Die()를 반복하면 사망 분열/소환이 새 적을 만들 수 있다. 라운드 종료 경로로 처리한다.
            if (victory) EndRoundByEnemyDefeat();
            else EndRoundByAllyDefeat();
        }

        /// <summary>
        /// 디버그 입력. 에디터와 개발 빌드에만 들어간다.
        ///
        /// F1: 디버그 패널 열기/닫기 (조작은 전부 패널에 있다)
        /// F2: 적 전멸 — 지금 전투를 즉시 이긴다
        /// F3: 아군 무적 토글
        /// F4: 배속 순환 1 → 4 → 8배
        /// F9: 현재 테마의 사건을 즉시 실행(사건 연출 확인용)
        /// </summary>
        private void HandleDebugInput()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F1))
            {
                UI.DevTools.DebugOverlay.Toggle();
                return;
            }
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F2))
            {
                DebugEndBattle(true);
                return;
            }
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F3))
            {
                UI.DevTools.DebugOverlay.ToggleAllyInvincible();
                return;
            }
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F4))
            {
                float next = Time.timeScale < 1.5f ? 4f : Time.timeScale < 6f ? 8f : 1f;
                Core.DebugMode.SetTimeScale(next);
                return;
            }

            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9)) return;
            if (gameState == GameState.EventStage) return;
            Core.DebugMode.BeginSession();

            StageEventData sample = _roundManager?.GetDebugSampleEvent() ?? BuildFallbackEvent();
            GameState previousState = gameState;
            Debug.Log($"[디버그] 사건 미리보기 실행: {sample?.title}");

            // 사건이 끝나면 원래 상태로 되돌린다(스테이지 진행에는 영향을 주지 않는다).
            // {deity} 치환은 EnterEvent 내부(PrepareRandomEventVariant)에서 처리된다.
            EnterEvent(sample, () =>
            {
                gameState = previousState;
                if (previousState == GameState.Preparation)
                {
                    uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed);
                }
            });
        }
#endif

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginRunAfterCharacterSelection()
        {
            if (_roundManager == null)
            {
                _roundManager = new RoundManager(dataManager);
                _roundManager.InitializeStage(1);
            }

            _roundManager.LoadRound(1);
            EnterNextStageAfterLoad();
            runManager?.SaveCurrentRun();
        }

        public void EnterPreparationAfterReward()
        {
            gameState = GameState.Preparation;
            preparationActionUsed = false;
            ShopPurchaseCount = 0;
            Cell.PlacementModeActive = false;
            StartPreparationTimer();
            uiManager?.UpdateLifeText();
        }

        public void EnterNextStageAfterLoad()
        {
            if (_roundManager != null && _roundManager.TryGetScheduledEvent(out StageEventData scheduledEvent))
            {
                EnterStageSlotEvent(scheduledEvent);
            }
            else
            {
                EnterPreparationAfterReward();
            }
        }

        /// <summary>
        /// 테마 고정 슬롯(내용 슬롯 5) 사건 진입. 스테이지 자체가 사건으로 대체되며,
        /// 사건 종료 후에는 다음 스테이지로 진행한다.
        /// </summary>
        private void EnterStageSlotEvent(StageEventData stageEvent)
        {
            EnterEvent(stageEvent ?? BuildFallbackEvent(), () => runManager?.AdvanceToNextStage());
        }

        /// <summary>
        /// 페이즈 사이 어디서든 호출할 수 있는 일반 사건 진입점.
        /// </summary>
        /// <param name="stageEvent">표시할 사건 데이터.</param>
        /// <param name="onComplete">사건이 끝난 뒤 이어서 실행할 연속 동작. null이면 기본 진행.</param>
        public void EnterEvent(StageEventData stageEvent, Action onComplete)
        {
            if (stageEvent == null)
            {
                onComplete?.Invoke();
                return;
            }

            _eventResumeAction = onComplete;
            gameState = GameState.EventStage;
            isPreparationTimerActive = false;
            isRoundProgressTimerActive = false;
            eventStageEnteredAt = Time.unscaledTime;
            currentStageEvent = PrepareRandomEventVariant(stageEvent);
            pendingEventChoice = null;
            eventDialogueIndex = 0;
            uiManager?.ShowEventStagePanel(currentStageEvent, eventDialogueIndex);
        }

        private static StageEventData PrepareRandomEventVariant(StageEventData source)
        {
            if (source?.randomSpeakers == null || source.randomSpeakers.Count == 0) return source;
            string speaker = source.randomSpeakers[UnityEngine.Random.Range(0, source.randomSpeakers.Count)];
            return new StageEventData
            {
                id = source.id,
                themeId = source.themeId,
                stageInRound = source.stageInRound,
                tier = source.tier,
                requiresBossDefeatId = source.requiresBossDefeatId,
                triggerBossId = source.triggerBossId,
                triggerThemeId = source.triggerThemeId,
                triggerStageInRound = source.triggerStageInRound,
                requiresRunEventIds = source.requiresRunEventIds,
                allowUnlockedRecruit = source.allowUnlockedRecruit,
                requiresUnitInParty = source.requiresUnitInParty,
                title = source.title?.Replace("{deity}", speaker),
                oncePerRun = source.oncePerRun,
                blockedUnitIds = source.blockedUnitIds,
                randomSpeakers = source.randomSpeakers,
                choices = source.choices,
                // 대사 필드 복사/치환은 DTO의 CloneWithReplacement가 담당한다
                // (필드 추가 시 복사 누락으로 연출이 사라지는 것을 막기 위함).
                dialogue = source.dialogue?.Select(line => line.CloneWithReplacement("{deity}", speaker)).ToList(),
            };
        }

        /// <summary>
        /// 런타임에 사건을 예약한다. 다음 페이즈 체크포인트에서 발생한다.
        /// </summary>
        public void RequestEvent(StageEventData stageEvent)
        {
            _eventScheduler.Enqueue(stageEvent);
        }

        /// <summary>
        /// 페이즈 전환 지점에서 호출한다. 예약된 사건이 있으면 먼저 발생시키고,
        /// 사건 종료 후 <paramref name="continuation"/>을 이어서 실행한다.
        /// 사건을 삽입해 흐름을 가로챘으면 true, 없으면 false(=호출부가 continuation을 직접 진행).
        /// </summary>
        public bool TryRunEventCheckpoint(Action continuation)
        {
            StageEventData queued = _eventScheduler.TryDequeue();
            if (queued == null) return false;

            EnterEvent(queued, continuation);
            return true;
        }

        /// <summary>
        /// 페이즈 전환 지점에서 호출한다. 예약된 사건이 있으면 발생시키고 종료 후
        /// <paramref name="continuation"/>을 잇는다. 없으면 즉시 <paramref name="continuation"/>을 실행한다.
        /// 사건이 발생할 수 있는 세 지점(육성 종료 후, 전투 시작 전, 전투 승리 후 보상 전)에서 사용한다.
        /// </summary>
        public void RunEventCheckpoint(Action continuation)
        {
            if (!TryRunEventCheckpoint(continuation))
            {
                continuation?.Invoke();
            }
        }

        public void AdvanceEventDialogue()
        {
            if (gameState != GameState.EventStage || currentStageEvent == null) return;
            int dialogueCount = currentStageEvent.dialogue?.Count ?? 0;
            eventDialogueIndex = Mathf.Min(eventDialogueIndex + 1, dialogueCount);

            // 선택지가 없는 사건(인트로 등)은 대사가 끝나면 바로 종료한다.
            bool hasChoices = (currentStageEvent.choices?.Count ?? 0) > 0;
            if (eventDialogueIndex >= dialogueCount && !hasChoices)
            {
                CompleteEventStage();
                return;
            }

            uiManager?.ShowEventStagePanel(currentStageEvent, eventDialogueIndex);
        }

        /// <summary>
        /// 남은 대사를 한 번에 건너뛴다.
        /// 선택지가 있는 사건은 선택지 화면으로, 없는 사건(인트로 등)은 바로 종료로 간다.
        /// </summary>
        public void SkipEventDialogue()
        {
            if (gameState != GameState.EventStage || currentStageEvent == null) return;
            eventDialogueIndex = currentStageEvent.dialogue?.Count ?? 0;

            bool hasChoices = (currentStageEvent.choices?.Count ?? 0) > 0;
            if (!hasChoices)
            {
                CompleteEventStage();
                return;
            }

            uiManager?.ShowEventStagePanel(currentStageEvent, eventDialogueIndex);
        }

        public void SelectEventChoice(string choiceId)
        {
            if (gameState != GameState.EventStage || currentStageEvent?.choices == null) return;
            StageEventChoiceData choice = currentStageEvent.choices.FirstOrDefault(entry => entry.id == choiceId);
            if (choice == null) return;

            if (string.Equals(choice.action, "pay_gold", StringComparison.OrdinalIgnoreCase))
            {
                int cost = Mathf.Max(0, choice.goldCostPerStage * (_roundManager?.Stage ?? 1));
                if (inventoryManager == null || !inventoryManager.TrySpendGold(cost))
                {
                    uiManager?.ShowEventMessage(string.IsNullOrWhiteSpace(choice.failureText)
                        ? $"골드가 부족합니다. 필요 골드: {cost}"
                        : choice.failureText.Replace("{cost}", cost.ToString()));
                    return;
                }
                uiManager?.ShowEventResolution(string.IsNullOrWhiteSpace(choice.successText)
                    ? $"공물로 {cost} 골드를 바쳤다."
                    : choice.successText.Replace("{cost}", cost.ToString()));
                ApplyEventChoiceRewards(choice);
                runManager?.SaveCurrentRun();
                return;
            }

            if (choice.battleEnemyId > 0)
            {
                MarkCurrentEventTriggered();
                BeginEventBattle(choice);
                return;
            }

            // 자리가 없는 채로 합류시키면 영입이 조용히 실패한다. 먼저 자리를 묻는다.
            if (choice.grantUnitId > 0 && !HasRoomForRecruit(choice.grantUnitId))
            {
                BeginRecruitRosterPrompt(choice);
                return;
            }

            ApplyEventChoiceRewards(choice);
            runManager?.SaveCurrentRun();
            uiManager?.ShowEventResolution(choice.successText ?? "사건이 끝났다.");
        }

        // ── 영입 자리 비우기 ─────────────────────────────────────────

        /// <summary>이 유닛을 지금 받을 수 있는가. 이미 일행이면 자리를 묻지 않는다.</summary>
        private static bool HasRoomForRecruit(int unitId)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return false;
            if (grid.heroList.Any(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.ID == unitId))
            {
                return true;
            }
            return grid.HasAvailableAllySlot();
        }

        /// <summary>자리가 없다고 알리고, 떠나보낼 사람을 고르거나 합류를 포기하게 한다.</summary>
        private void BeginRecruitRosterPrompt(StageEventChoiceData choice)
        {
            List<Unit> leavable = GetDismissableUnits();
            if (leavable.Count == 0)
            {
                // 메인 혼자 자리를 다 채우는 편성은 없지만, 그래도 막다른 길을 만들지 않는다.
                uiManager?.ShowEventResolution(string.IsNullOrWhiteSpace(choice.failureText)
                    ? "자리를 비울 수 없었다. 합류는 없던 일이 되었다."
                    : choice.failureText);
                return;
            }

            pendingRecruitChoice = choice;
            uiManager?.ShowEventRosterPrompt(
                string.IsNullOrWhiteSpace(choice.rosterFullText)
                    ? "자리가 꽉 찼다. 새로 맞이하려면 누군가가 떠나야 할 것 같다."
                    : choice.rosterFullText,
                leavable);
        }

        /// <summary>떠나보낼 수 있는 아군. 메인 캐릭터와 소환수는 뺀다 — 런의 축이거나 자리를 차지하지 않는다.</summary>
        private static List<Unit> GetDismissableUnits()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return new List<Unit>();

            int mainUnitId = CharacterSelectionManager.Instance?.MainUnitId ?? 0;
            return grid.heroList
                .Where(hero => hero != null && hero.isActive && !hero.IsEnemy && !hero.IsSummon &&
                               hero.ID != mainUnitId)
                .ToList();
        }

        /// <summary>한 명을 떠나보내고 기다리던 합류를 마무리한다. 사건 화면이 부른다.</summary>
        public void DismissUnitForRecruit(int unitId)
        {
            if (gameState != GameState.EventStage) return;
            StageEventChoiceData choice = pendingRecruitChoice;
            if (choice == null) return;

            Unit leaving = GetDismissableUnits().FirstOrDefault(hero => hero.ID == unitId);
            if (leaving == null) return;

            string leavingName = leaving.UnitName;
            Cell vacated = leaving.currentCell;
            GridManager.Instance.RetireUnit(leaving);
            GridManager.Instance.PruneUnitLists();

            // 비운 칸의 예약을 즉시 푼다.
            //
            // <see cref="Unit.DeactivateUnit"/>가 죽은 자리를 2초간 잠그는 것은
            // <b>전투 중에 그 자리로 곧바로 다시 소환되는 것</b>을 막기 위해서다.
            // 여기는 사건 중이라 그 이유가 없고, 풀지 않으면 같은 프레임에 이어지는 영입이
            // 방금 비운 칸을 못 보고 조용히 실패한다.
            if (vacated != null) vacated.reservedTime = 0f;

            pendingRecruitChoice = null;
            ApplyEventChoiceRewards(choice);
            runManager?.SaveCurrentRun();
            string joined = choice.successText ?? "새로운 동료가 합류했다.";
            uiManager?.ShowEventResolution($"{leavingName}은(는) 일행과 헤어졌다.\n{joined}");
        }

        /// <summary>자리를 비우지 않고 합류를 포기한다. 사건 화면이 부른다.</summary>
        public void CancelRecruitForRoster()
        {
            if (gameState != GameState.EventStage) return;
            StageEventChoiceData choice = pendingRecruitChoice;
            if (choice == null) return;

            pendingRecruitChoice = null;
            MarkCurrentEventTriggered();
            runManager?.SaveCurrentRun();
            uiManager?.ShowEventResolution(string.IsNullOrWhiteSpace(choice.failureText)
                ? "자리를 비우지 않기로 했다. 합류는 없던 일이 되었다."
                : choice.failureText);
        }

        private void BeginEventBattle(StageEventChoiceData choice)
        {
            pendingEventChoice = choice;
            eventBattleInProgress = true;
            uiManager?.HideEventStagePanel();
            SaveAllyFieldState();
            if (_roundManager == null || !_roundManager.StartEventBattle(choice.battleEnemyId))
            {
                eventBattleInProgress = false;
                uiManager?.ShowEventResolution("전투를 시작할 수 없습니다.");
                return;
            }

            // 사건 전투도 결과 화면과 딜 그래프를 쓴다. 시작 시점 값을 여기서도 잡아야 지난 전투 값이 남지 않는다.
            CaptureBattleStart();
            gameState = GameState.RoundInProgress;
            remainingRoundTurns = roundTurnLimit;
            isRoundProgressTimerActive = true;
            GridManager.Instance.OnRoundStart();
        }

        public void CompleteEventStage()
        {
            if (gameState != GameState.EventStage) return;
            uiManager?.HideEventStagePanel();

            // 사건을 비우기 전에 해금을 먼저 찍는다.
            // 어느 선택지로 끝났든 — 합류·거절·자리 부족·전투 패배 — 이 한 지점을 지나므로
            // 여기서 한 번만 다룬다.
            GrantRecruitUnlock(currentStageEvent);

            currentStageEvent = null;
            pendingEventChoice = null;
            pendingRecruitChoice = null;
            eventDialogueIndex = 0;

            // 사건 진입 시 지정한 연속 동작으로 흐름을 이어간다.
            // 지정되지 않았다면(구형 호출 경로) 기본적으로 다음 스테이지로 진행한다.
            Action resume = _eventResumeAction;
            _eventResumeAction = null;
            if (resume != null)
            {
                resume.Invoke();
            }
            else
            {
                runManager?.AdvanceToNextStage();
            }
        }

        private StageEventData BuildFallbackEvent()
        {
            string theme = _roundManager?.CurrentThemeName ?? "낯선";
            return new StageEventData
            {
                id = "fallback_event",
                themeId = _roundManager?.CurrentThemeId ?? 0,
                stageInRound = _roundManager?.ContentSlotInRound ?? 5,
                title = $"{theme}의 갈림길",
                dialogue = new List<StageEventDialogueData>
                {
                    new StageEventDialogueData { speaker = "여행자", text = "잠시 숨을 고르며 다음 길을 살핀다." },
                },
                choices = new List<StageEventChoiceData>
                {
                    new StageEventChoiceData { id = "continue", text = "길을 계속 간다.", action = "continue", successText = "일행은 다시 여정을 시작했다." },
                },
            };
        }

        public void EnterTrainingPhase()
        {
            if (!AllowProgressWhileWithinCarryLimit()) return;

            gameState = GameState.TrainingPhase;
            isPreparationTimerActive = false;
            isRoundProgressTimerActive = false;
            trainingPhaseEnteredAt = Time.unscaledTime;
            uiManager?.ShowTrainingPhasePanel();
        }

        // 육성 페이즈에서 집중 스탯을 선택하면 호출된다.
        // 메인 캐릭터에 훈련을 적용한 뒤 다음 스테이지로 진행한다.
        public void CompleteTrainingPhaseWithFocus(BaseEnums.PrimaryStat focus)
        {
            if (gameState != GameState.TrainingPhase) return;
            if (!AllowProgressWhileWithinCarryLimit())
            {
                uiManager?.HideTrainingPhasePanel();
                gameState = GameState.Preparation;
                RefreshPreparationForCarryWeight();
                return;
            }

            // 이번 훈련에 앉은 서포트는 훈련을 적용하기 <b>전에</b> 읽는다.
            // ApplyTraining이 턴을 넘기며 배치를 무효화하기 때문이다.
            var trainees = new List<Unit> { TrainingManager.GetMainUnit() };
            trainees.AddRange(TrainingManager.GetSupportsOn(focus));

            TrainingManager.TrainingResult result = TrainingManager.ApplyTraining(focus);

            // 육성 EXP는 메인과 <b>이 훈련에 참여한 서포트</b>만 받는다.
            // 예전에는 5인이 똑같이 받아 서포트가 메인과 같은 레벨로 자랐다. 서포트는 전투 EXP를
            // 늘 함께 받고, 훈련 EXP는 참여할 때만 받아 메인의 약 80% 레벨에 선다.
            GrantExpTo(trainees, ExpPerTrainingBase + ExpPerTrainingPerStage * CurrentStageForExp);
            runManager?.SaveCurrentRun();

            // 결과를 한 장으로 보여 준 다음에 준비 페이즈로 넘어간다.
            // 확인을 누르면 CompleteTrainingResult가 이어받는다.
            _pendingTrainingResult = result;
            uiManager?.ShowTrainingResultPanel(result);
        }

        /// <summary>확인을 누르기 전까지 들고 있는 훈련 결과. 준비 페이즈 안내줄에 다시 쓴다.</summary>
        private TrainingManager.TrainingResult _pendingTrainingResult;

        /// <summary>훈련 결과 화면에서 확인을 눌렀다. 여기서 준비 페이즈로 돌아간다.</summary>
        public void CompleteTrainingResult()
        {
            uiManager?.HideTrainingResultPanel();
            uiManager?.HideTrainingPhasePanel();

            preparationActionUsed = true;
            gameState = GameState.Preparation;
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed,
                BuildTrainingResultMessage(_pendingTrainingResult));
        }

        /// <summary>
        /// 지금 훈련을 열 수 있는가. 조건은 <see cref="OpenTrainingFromPreparation"/>과 같다.
        /// 스킬 화면의 빈 상태가 "훈련하러 가기"를 켤지 결정할 때 묻는다.
        /// </summary>
        public bool CanOpenTraining => gameState == GameState.Preparation && !preparationActionUsed
                                       && !IsPreparationLimitedToDeck() && !HasOverburdenedHeroes;

        public void OpenTrainingFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            isPreparationTimerActive = false;
            gameState = GameState.TrainingPhase;
            uiManager?.HidePreparationPhasePanel();
            uiManager?.ShowTrainingPhasePanel();
        }

        /// <summary>
        /// 훈련 화면을 닫고 준비 페이즈로 돌아간다. 준비 행동은 쓰지 않는다.
        ///
        /// 예전에는 훈련 화면에 뒤로 가는 길이 없었다. ESC를 누르면 일시정지 메뉴가 떠서
        /// 결정을 누르는 것 말고는 빠져나갈 수 없었다(QA). 서포트 배치는 그대로 남으므로
        /// 다시 열어도 같은 자리가 나온다.
        /// </summary>
        public void CancelTrainingFromPreparation()
        {
            if (gameState != GameState.TrainingPhase) return;

            uiManager?.HideTrainingPhasePanel();
            gameState = GameState.Preparation;
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed,
                "훈련을 열었다가 닫았습니다. 준비 행동은 쓰지 않았습니다.");
        }

        /// <summary>
        /// 휴식 한 번이 되찾아 주는 훈련 체력.
        ///
        /// 훈련 평균 비용이 17이므로 이 값은 곧 <b>훈련 3회마다 휴식 1회</b>라는 리듬이다.
        /// 올리면 훈련 가동률이 오르고, 내리면 성장 총량이 줄어든다.
        /// </summary>
        public const int RestEnergyRecovery = 50;

        /// <summary>
        /// 휴식이 되돌리는 파티 체력 비율.
        ///
        /// 예전에는 <b>완전 회복</b>이었다. 그러면 스테이지마다 공짜 엘릭서를 쓰는 셈이라
        /// 상점의 회복약과 회복 보상이 통째로 무의미해진다. 휴식은 최후의 안전판으로 남기고
        /// 완전 회복은 값을 치르는 쪽(상점 · 보상)의 몫으로 돌렸다.
        /// </summary>
        public const float RestPartyHealRatio = 0.30f;

        public void RestFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            HealActiveHeroesByRatio(RestPartyHealRatio);

            // 휴식이 하는 일은 셋이다 — 훈련 체력 · 컨디션 · 파티 체력.
            // 컨디션을 확실히 올리는 수단은 휴식뿐이다(훈련은 흔들기만 한다).
            TrainingManager.State.RestoreEnergy(RestEnergyRecovery);
            TrainingManager.State.ImproveCondition();

            // 휴식도 턴을 쓴다. 서포트는 다음 훈련에서 다른 자리에 앉는다.
            TrainingManager.InvalidateSupportPlacement();

            preparationActionUsed = true;
            runManager?.SaveCurrentRun();
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed,
                $"휴식 완료: 훈련 체력 +{RestEnergyRecovery} (현재 {TrainingManager.State.Energy}) · " +
                $"컨디션 {TrainingManager.State.ConditionName} · " +
                $"파티 체력 {Mathf.RoundToInt(RestPartyHealRatio * 100f)}% 회복.");
        }

        public void BeginAdditionalBattleFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            preparationActionUsed = true;
            StartRound();
        }

        public void OpenDeckSetupFromPreparation()
        {
            if (gameState != GameState.Preparation) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            // 배치 모드를 켜 놓일 자리를 빛낸다. 다시 누르면 끈다.
            Cell.PlacementModeActive = !Cell.PlacementModeActive;
            uiManager?.ShowPreparationPhasePanel(
                IsPreparationLimitedToDeck(),
                preparationActionUsed,
                Cell.PlacementModeActive
                    ? "덱 구성: 빛나는 칸이 놓을 수 있는 자리입니다. 카드를 끌어 옮기고(다른 카드 위에 놓으면 자리를 바꿉니다), 짧게 누르면 캐릭터 창이 열립니다."
                    : "덱 구성을 닫았습니다. 준비 중에는 언제든 카드를 끌어 배치를 바꿀 수 있습니다.");
        }

        // ── 상점 ─────────────────────────────────────────────────────

        /// <summary>
        /// 준비 페이즈 한 번에 살 수 있는 횟수.
        ///
        /// 가격만으로는 후반을 막지 못한다 — 누적 골드가 스테이지 제곱으로 불어나므로
        /// 횟수를 묶지 않으면 스테이지마다 파티를 완전 회복시킬 수 있고, 그러면 체력 소모가
        /// 런의 압박에서 빠진다. 훈련·휴식과 달리 <b>준비 행동을 쓰지는 않는다</b>.
        /// </summary>
        public const int MaxShopPurchases = 2;

        /// <summary>이번 준비 페이즈에 이미 산 횟수.</summary>
        public int ShopPurchaseCount { get; private set; }

        public int ShopPurchasesLeft => Mathf.Max(0, MaxShopPurchases - ShopPurchaseCount);

        /// <summary>보스전 준비에서도 연다. 보스 앞에서 회복약을 사는 것이 상점의 쓸모다.</summary>
        public bool CanOpenShop => gameState == GameState.Preparation && !HasOverburdenedHeroes;

        public void OpenShopFromPreparation()
        {
            if (!CanOpenShop) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            uiManager?.ShowShopPanel();
        }

        /// <summary>상점에서 한 번 샀다. 횟수는 준비 페이즈가 바뀔 때 0으로 돌아간다.</summary>
        public void NotifyShopPurchase()
        {
            ShopPurchaseCount = Mathf.Min(MaxShopPurchases, ShopPurchaseCount + 1);
            runManager?.SaveCurrentRun();
        }

        // ── 스킬 ─────────────────────────────────────────────────────

        /// <summary>
        /// 힌트받은 스킬을 스킬 Pt로 배우는 화면. 상점과 마찬가지로 준비 행동을 쓰지 않는다.
        /// 보스전 준비에서도 열린다 — 이미 번 스킬 Pt를 쓰는 일까지 막을 이유가 없다.
        /// </summary>
        public bool CanOpenSkillScreen => gameState == GameState.Preparation && !HasOverburdenedHeroes;

        public void OpenSkillScreenFromPreparation()
        {
            if (!CanOpenSkillScreen) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            uiManager?.ShowSkillPanel();
        }

        /// <summary>스킬을 하나 배웠다. 런 상태가 바뀌었으므로 바로 저장한다.</summary>
        public void NotifySkillLearned()
        {
            runManager?.SaveCurrentRun();
        }

        public void OpenEquipmentFromPreparation()
        {
            if (gameState != GameState.Preparation) return;
            uiManager?.ShowEquipmentPanel();
        }

        public bool IsPreparationLimitedToDeck()
        {
            return CurrentMode == GameMode.Training && _roundManager != null && _roundManager.IsCurrentBossStage;
        }

        public bool HasOverburdenedHeroes => GridManager.Instance?.heroList?.Any(hero =>
            hero != null && hero.isActive && !hero.IsEnemy && hero.IsOverCarryWeightMax) == true;

        public string CarryWeightBlockMessage
        {
            get
            {
                List<Unit> overburdened = GridManager.Instance?.heroList?
                    .Where(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.IsOverCarryWeightMax)
                    .ToList() ?? new List<Unit>();
                if (overburdened.Count == 0) return null;

                string units = string.Join(", ", overburdened.Select(hero =>
                    $"{hero.UnitName} {hero.CarryWeightCurrent}/{hero.CarryWeightMax}"));
                return $"3차 중량 초과: {units}. 장비에서 휴대품을 비워야 다른 행동을 할 수 있습니다.";
            }
        }

        private bool AllowProgressWhileWithinCarryLimit()
        {
            if (!HasOverburdenedHeroes) return true;
            RefreshPreparationForCarryWeight();
            return false;
        }

        public void RefreshPreparationForCarryWeight()
        {
            if (gameState != GameState.Preparation) return;
            uiManager?.ShowPreparationPhasePanel(
                IsPreparationLimitedToDeck(), preparationActionUsed, CarryWeightBlockMessage);
        }

        public void RestorePreparationActionState(bool actionUsed, int shopPurchaseCount = 0)
        {
            preparationActionUsed = actionUsed;
            ShopPurchaseCount = Mathf.Clamp(shopPurchaseCount, 0, MaxShopPurchases);
            if (gameState == GameState.Preparation)
            {
                uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed);
            }
        }

        /// <summary>출전 중인 아군의 최대 체력 대비 비율만큼 회복한다. 쓰러진 유닛은 일어나지 않는다.</summary>
        private void HealActiveHeroesByRatio(float ratio)
        {
            foreach (Unit hero in GridManager.Instance.heroList)
            {
                if (hero != null && hero.isActive && !hero.IsEnemy)
                {
                    hero.RestoreHpOutsideCombat(Mathf.CeilToInt(hero.HpMax * Mathf.Clamp01(ratio)));
                }
            }
        }

        private static string BuildTrainingResultMessage(TrainingManager.TrainingResult result)
        {
            string transferred = result.HintedCodeIds != null && result.HintedCodeIds.Count > 0
                ? $" / 스킬 힌트 {string.Join(", ", result.HintedCodeIds)}"
                : "";
            string supports = result.SupportMessages != null && result.SupportMessages.Count > 0
                ? $" / 서포트 {result.SupportMessages.Count}명 판정"
                : "";

            if (result.Failed)
            {
                return $"훈련 실패 ({result.FailureRate}%): {FormatPrimaryStat(result.Focus)} 상승 없음, " +
                       $"체력 {result.EnergySpent} 소모 (남은 체력 {result.EnergyAfter}){supports}";
            }

            string conPart = result.SecondaryText.Length > 0 ? $" ({result.SecondaryText})" : "";
            return $"훈련 완료: {FormatPrimaryStat(result.Focus)} +{result.StatGain}{conPart}, " +
                   $"스킬 Pt +{result.SkillPointsGained}, 훈련 Lv.{result.NewFocusTrainingLevel}, " +
                   $"남은 체력 {result.EnergyAfter}{supports}{transferred}";
        }

        private static string FormatPrimaryStat(BaseEnums.PrimaryStat stat)
        {
            return stat switch
            {
                BaseEnums.PrimaryStat.STR => "근력",
                BaseEnums.PrimaryStat.DEX => "민첩",
                BaseEnums.PrimaryStat.CON => "체력",
                BaseEnums.PrimaryStat.INT => "지능",
                BaseEnums.PrimaryStat.LUK => "행운",
                _ => stat.ToString(),
            };
        }

        private int GetRemainingEnemyCount()
        {
            int fieldEnemies = 0;

            // 필드에 있는 적 수 계산 (벤치에 있는 적은 제외)
            foreach (Unit enemy in GridManager.Instance.enemyList)
            {
                if (enemy != null && enemy.isActive)
                {
                    // 벤치에 있는 유닛은 제외 (적이 벤치에 있을 가능성은 낮지만 안전을 위해)
                    if (GridManager.Instance.IsBenchCell(enemy.currentCell))
                    {
                        continue;
                    }
                    fieldEnemies++;
                }
            }

            return fieldEnemies;
        }

        private bool AreAllAlliesDefeated()
        {
            // 필드에 있는 아군 수 계산 (벤치 제외)
            foreach (Unit ally in GridManager.Instance.heroList)
            {
                if (ally != null && ally.isActive)
                {
                    // 벤치에 있는 유닛은 제외, 필드에 있는 아군만 체크
                    if (!GridManager.Instance.IsBenchCell(ally.currentCell))
                    {
                        return false; // 필드에 살아있는 아군이 있으면 false
                    }
                }
            }
            return true; // 필드에 살아있는 아군이 없으면 true
        }

        private void EndRoundByTimeout()
        {
            // 라운드 진행 타이머 정지
            isRoundProgressTimerActive = false;
            
            // 남은 적 수만큼 생명력 차감
            int remainingEnemies = GetRemainingEnemyCount();
            _battleEndReason = $"턴 초과 — {roundTurnLimit}턴 안에 적을 모두 쓰러뜨리지 못했습니다";
            _battleEnemiesLeft = remainingEnemies;
            TakeDamage(remainingEnemies);

            Debug.Log($"라운드 시간 초과! 남은 적 {remainingEnemies}마리만큼 생명력 차감. 현재 생명력: {life}");
            
            // 라운드 종료 처리
            EndRound(false);
        }

        public void EndRoundByEnemyDefeat()
        {
            // 적 전멸로 인한 라운드 종료 (생명력 차감 없음)
            isRoundProgressTimerActive = false;
            Debug.Log("모든 적을 처치했습니다! 라운드 승리!");
            _battleEndReason = "적 전멸";
            _battleEnemiesLeft = 0;
            EndRound(true);
        }

        private void EndRoundByAllyDefeat()
        {
            // 아군 전멸로 인한 라운드 종료 (패배 처리)
            isRoundProgressTimerActive = false;
            
            // 남은 적 수만큼 생명력 차감 (패배 페널티)
            int remainingEnemies = GetRemainingEnemyCount();
            _battleEndReason = "아군 전멸";
            _battleEnemiesLeft = remainingEnemies;
            TakeDamage(remainingEnemies);

            Debug.Log($"아군이 전멸했습니다! 남은 적 {remainingEnemies}마리만큼 생명력 차감. 현재 생명력: {life}");
            
            // 라운드 종료 처리
            EndRound(false);
        }

        private void EndRound(bool victory)
        {
            _roundManager?.StopRound();
            gameState = GameState.RoundEnd;
            Combat.DamageMeter.End();
            Combat.CombatLog.End(victory, _battleEndReason ?? (victory ? "적 전멸" : "패배"));
            // 쓰러진 아군은 필드 복원이 되살리기 전에 세어야 한다.
            BattleResultData result = BeginBattleResult(victory);
            // 궁극기 자원만 전투 종료 정리보다 먼저 회수한다. 상태이상·방어막·고유 전투 자원은
            // 기존 OnRoundEnd 정리를 그대로 거쳐 다음 전투로 넘어가지 않는다.
            CaptureAllyUltimateResources();
            GridManager.Instance?.OnRoundEnd();
            
            // 아군 필드 상태 복원 (게임 오버가 아닌 경우에만)
            if (life > 0)
            {
                RestoreAllyFieldState();
            }
            GridManager.Instance?.ClearActiveEnemies();

            if (life <= 0)
            {
                pendingPartyExp = 0;
                gameState = GameState.GameOver;
                SaveSystem.DeleteSave();
                FinishBattleResult(result);
                result.GameOver = true;
                // 예전에는 여기서 아무 화면 없이 멈췄다. 결과 화면이 런 종료를 알리고 메인 메뉴로 보낸다.
                PresentBattleResult(result, LoadMainMenuScene);
                return;
            }

            // 필드 복원이 끝난 뒤에 EXP를 지급한다(복원 전에 주면 스냅샷에 덮여 사라진다).
            int earnedExp = pendingPartyExp;
            pendingPartyExp = 0;
            if (victory)
            {
                earnedExp += ExpPerStageClearBase + ExpPerStageClearPerStage * CurrentStageForExp;
            }
            GrantExpToParty(earnedExp);
            result.ExpGained = earnedExp > 0
                ? Mathf.Max(1, Mathf.RoundToInt(earnedExp * Codes.Passive.RewardModifiers.ExpMultiplier()))
                : 0;

            // 강화제는 <이긴 전투 수>로 산다. 예전에는 져도 깎여, 막힌 벽 앞에서 다시 도전할수록
            // 버프가 먼저 사라져 더 불리해졌다.
            if (victory) ConsumePartyTonicBattle();

            if (victory)
            {
                GrantClearGold();
                QueueBossClearEvents();
                // 사건 안의 임시 전투는 그 슬롯을 이긴 것이 아니다.
                if (!eventBattleInProgress) QueueSlotClearEvents();
            }

            FinishBattleResult(result);

            if (eventBattleInProgress)
            {
                PresentBattleResult(result, () => ResolveEventBattle(victory));
                return;
            }

            if (victory)
            {
                // 전투 승리 직후·보상 표시 직전(after battle, before reward)은 사건 발생 지점이다.
                // 예약된 사건이 있으면 먼저 처리하고, 끝나면 보상 화면을 연다.
                result.NextStep = "보상 선택";
                PresentBattleResult(result, () => RunEventCheckpoint(ShowRewardSelection));
            }
            else
            {
                // 패배도 해당 스테이지의 확정 결과다. 클리어 보상은 지급하지 않고 다음 스테이지로 진행한다.
                result.NextStep = "다음 스테이지";
                PresentBattleResult(result, () => runManager?.AdvanceToNextStage());
            }
        }

        // ── 전투 결과 정산 ───────────────────────────────────────────
        // 결과 화면(BattleResultScreen)에 넘길 값을 전투 시작과 끝에서 모은다.
        // 전투 중 골드는 처치마다 바로 들어오므로 시작 시점과의 차로 잰다.

        private int _battleStartLife;
        private int _battleStartGold;
        private int _battleStartKills;
        private string _battleEndReason;
        private int _battleEnemiesLeft;
        private readonly List<Unit> _battleParty = new();

        private void CaptureBattleStart()
        {
            _battleStartLife = life;
            _battleStartGold = inventoryManager != null ? inventoryManager.Gold : 0;
            _battleStartKills = KillCount;
            _battleEndReason = null;
            _battleEnemiesLeft = 0;

            _battleParty.Clear();
            if (GridManager.Instance == null) return;
            foreach (Unit hero in GridManager.Instance.heroList)
            {
                if (hero == null || !hero.isActive || hero.IsEnemy || hero.IsSummon) continue;
                if (hero.currentCell == null || GridManager.Instance.IsBenchCell(hero.currentCell)) continue;
                _battleParty.Add(hero);
            }
            Combat.DamageMeter.Begin(_battleParty);
            Combat.CombatLog.Begin(_battleParty);
        }

        /// <summary>전투가 끝난 직후 — 필드 복원 전에 — 판정과 쓰러진 아군을 적는다.</summary>
        private BattleResultData BeginBattleResult(bool victory)
        {
            var result = new BattleResultData
            {
                Victory = victory,
                Reason = _battleEndReason ?? (victory ? "적 전멸" : "패배"),
                LifeBefore = _battleStartLife,
                LifeAfter = life,
                EnemiesLeft = _battleEnemiesLeft,
                StageLabel = _roundManager != null ? $"{_roundManager.Round}-{_roundManager.StageInRound}" : "",
                ThemeName = _roundManager?.CurrentThemeName,
            };

            // 쓰러지면 DeactivateUnit이 isActive를 끈다. 이름은 전투 시작 때 잡아 둔 참조에서 읽는다.
            foreach (Unit hero in _battleParty)
            {
                if (hero == null) continue;
                bool fallen = !hero.isActive || hero.HpCurr <= 0;
                result.Party.Add(new BattleResultData.Member(hero.UnitName, hero.PortraitPath, fallen));
            }

            // 딜 그래프. 전투 중 우측 그래프와 같은 값을 많이 넣은 순서로 옮긴다.
            foreach (Combat.DamageMeter.Entry entry in Combat.DamageMeter.Sorted())
            {
                bool fallen = entry.Unit == null || !entry.Unit.isActive || entry.Unit.HpCurr <= 0;
                result.Damage.Add(new BattleResultData.DamageLine(entry.Name, entry.Portrait, entry.Damage, fallen));
            }

            return result;
        }

        /// <summary>보수 지급이 끝난 뒤의 값(목숨 · 골드 · 처치 · 토큰)을 채운다.</summary>
        private void FinishBattleResult(BattleResultData result)
        {
            result.LifeAfter = life;
            result.GoldGained = Mathf.Max(0, (inventoryManager != null ? inventoryManager.Gold : 0) - _battleStartGold);
            result.Kills = Mathf.Max(0, KillCount - _battleStartKills);
            // 처치 3의 배수마다 무작위 토큰 5개(OnKillEnemy).
            result.TokensGained = Mathf.Max(0, KillCount / 3 - _battleStartKills / 3) * 5;
        }

        /// <summary>
        /// 결과 화면을 띄우고, 확인을 누르면 <paramref name="continuation"/>으로 넘어간다.
        /// 자동 검증 도구가 전투를 연달아 돌릴 때는 화면 없이 곧바로 넘어간다 — 누를 사람이 없다.
        /// </summary>
        private void PresentBattleResult(BattleResultData result, Action continuation)
        {
            bool skip = uiManager == null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            skip |= Core.DebugMode.SuiteRunning;
#endif
            if (skip)
            {
                // 검증 도구는 런 종료 뒤에도 씬을 유지해 결과를 읽는다. 메인 메뉴로 보내지 않는다.
                if (!result.GameOver) continuation?.Invoke();
                return;
            }

            uiManager.ShowBattleResult(result, continuation);
        }

        /// <summary>전투 승리 보수. 처치 골드와 함께 상인(273) 패시브의 배율을 받는다.</summary>
        private void GrantClearGold()
        {
            int gold = Mathf.RoundToInt(
                (GoldPerClearBase + GoldPerClearPerStage * CurrentStageForExp) *
                Codes.Passive.RewardModifiers.GoldMultiplier());
            inventoryManager?.AddGold(gold);
            Debug.Log($"[골드] 전투 승리 보수 {gold} 획득 (현재 {inventoryManager?.Gold})");
        }

        /// <summary>걸려 있는 강화제의 남은 전투 수를 하나 깎는다. 끊긴 병이 있으면 스탯을 다시 돌린다.</summary>
        private static void ConsumePartyTonicBattle()
        {
            Core.PartyTonicState tonics = RunManager.Instance?.PartyTonics;
            if (tonics == null || !tonics.HasAny) return;

            if (tonics.ConsumeBattle()) GridManager.Instance?.RefreshAllyAttributes();
        }

        private void ShowRewardSelection()
        {
            gameState = GameState.RewardSelection;
            var rewards = rewardManager != null
                ? rewardManager.GenerateRewards(RewardManager.RewardCountForStage(_roundManager?.Stage ?? 1),
                    _roundManager?.Round ?? 1, CurrentMode, _battleKilledEnemyIds)
                : new List<RewardDef>();
            uiManager?.ShowRewardPanel(rewards);
        }

        /// <summary>
        /// 보스를 넘어선 직후 사건을 예약한다. <b>유닛 이름이 코드에 들어 있지 않다</b> —
        /// 사건 데이터의 <c>triggerBossId</c>가 방아쇠를 들고 있으므로,
        /// 새 영입 사건은 <c>80_stages.yaml</c>에 한 줄 더하는 것으로 끝난다.
        /// </summary>
        private void QueueBossClearEvents()
        {
            // 최종 보스와 중간 보스 둘 다 방아쇠가 된다.
            // 노르드 중부의 합류 분기(시구르드·브륀힐드)가 6슬롯 중간 보스 자리에 있다.
            QueueClearEventsFor(_roundManager?.CurrentBossId ?? 0);
            QueueClearEventsFor(_roundManager?.CurrentMidBossId ?? 0);
        }

        private void QueueClearEventsFor(int enemyId)
        {
            if (enemyId <= 0 || _roundManager == null) return;

            // 격파 기록부터 남긴다. <c>requiresBossDefeatId</c>로 열리는 사건들이 이 기록을 본다.
            //
            // 예전에는 '최초 격파' 자체가 합류 사건의 일회성이라 사건을 찾은 뒤에 기록해야
            // 했다. 지금은 <b>해금 여부</b>가 일회성을 맡으므로 기록이 무엇도 소진하지 않는다.
            SaveSystem.MarkBossDefeated(enemyId);

            foreach (StageEventData triggered in _roundManager.GetEventsTriggeredByBoss(enemyId))
            {
                if (CanOfferEvent(triggered)) RequestEvent(triggered);
            }
        }

        /// <summary>
        /// 테마의 특정 내용 슬롯을 이긴 직후 사건을 예약한다(<c>triggerThemeId</c> + <c>triggerStageInRound</c>).
        /// 천공 1슬롯의 츠쿠요미 합류처럼 범용 적만 서는 전투가 방아쇠일 때 쓴다.
        /// 스테이지가 넘어가기 전이라 테마와 슬롯은 방금 이긴 전투의 값이다.
        /// </summary>
        private void QueueSlotClearEvents()
        {
            if (_roundManager == null) return;
            foreach (StageEventData triggered in _roundManager.GetEventsTriggeredByCurrentSlotClear())
            {
                if (CanOfferEvent(triggered)) RequestEvent(triggered);
            }
        }

        /// <summary>
        /// 이 사건을 지금 띄울 수 있는가.
        /// 영입 사건이라면 <b>이미 해금했거나 일행에 있는</b> 유닛을 다시 제안하지 않는다.
        /// </summary>
        private bool CanOfferEvent(StageEventData stageEvent)
        {
            if (stageEvent == null) return false;
            if (stageEvent.oncePerRun && runManager != null &&
                runManager.HasTriggeredEvent(stageEvent.id)) return false;

            // 체인의 앞 사건을 이번 런에 봤는가. 목록 중 하나면 된다.
            if (stageEvent.requiresRunEventIds != null && stageEvent.requiresRunEventIds.Count > 0 &&
                (runManager == null || !stageEvent.requiresRunEventIds.Any(runManager.HasTriggeredEvent))) return false;

            if (RoundManager.IsBlockedByDeck(stageEvent)) return false;
            if (stageEvent.requiresUnitInParty > 0 && !IsUnitInParty(stageEvent.requiresUnitInParty)) return false;

            int recruitUnitId = stageEvent.RecruitUnitId;
            if (recruitUnitId <= 0) return true;

            if (!stageEvent.allowUnlockedRecruit && SaveSystem.IsStarterUnlocked(recruitUnitId)) return false;
            return !IsUnitInParty(recruitUnitId);
        }

        private static bool IsUnitInParty(int unitId)
            => GridManager.Instance != null && GridManager.Instance.heroList.Any(hero =>
                hero != null && hero.isActive && !hero.IsEnemy && hero.ID == unitId);

        /// <summary>
        /// 영입 사건을 끝까지 본 것만으로 그 유닛을 영구 해금한다.
        ///
        /// <b>합류시켰든 거절했든 같다 — 만났다는 사실이 해금 조건이다.</b>
        /// 거절이 해금까지 빼앗으면 선택지가 아니라 함정이 된다 — 그 런에 자리가 없거나
        /// 지금 필요하지 않다는 이유로 거절해도 다음 런부터는 골라 쓸 수 있어야 한다.
        ///
        /// 육성 모드에서만 남긴다 — 런 중 합류가 해금으로 이어지는 다른 경로(<see cref="GridManager"/>)와 같은 줄을 쓴다.
        /// </summary>
        private void GrantRecruitUnlock(StageEventData stageEvent)
        {
            if (CurrentMode != GameMode.Training) return;

            if (stageEvent == null) return;

            // 사건이 여럿을 내밀면 <b>전원</b>을 해금한다. 만났다는 사실이 조건이므로
            // 한 명만 데려가도 나머지가 다음 런에서 사라지면 안 된다.
            foreach (int recruitUnitId in stageEvent.RecruitUnitIds)
            {
                SaveSystem.AddStarterUnlock(recruitUnitId);
            }
        }

        private void ResolveEventBattle(bool victory)
        {
            eventBattleInProgress = false;
            isRoundProgressTimerActive = false;
            gameState = GameState.EventStage;

            StageEventChoiceData resolvedChoice = pendingEventChoice;
            if (victory && resolvedChoice != null)
            {
                ApplyEventChoiceRewards(resolvedChoice);
                runManager?.SaveCurrentRun();
            }

            string resultText = victory
                ? resolvedChoice?.successText ?? "전투에서 승리했다."
                : resolvedChoice?.failureText ?? "전투에서 물러났다.";
            uiManager?.ShowEventResolution(resultText);
        }

        private void ApplyEventChoiceRewards(StageEventChoiceData choice)
        {
            if (choice == null) return;
            MarkCurrentEventTriggered();
            if (choice.grantUnitId > 0) RecruitSupportUnit(choice.grantUnitId);
            if (choice.grantItemId > 0) inventoryManager?.AddItem(choice.grantItemId);
            if (choice.grantPassiveCodeId <= 0) return;

            IEnumerable<Unit> recipients = choice.grantPassiveToAll
                ? GridManager.Instance.heroList.Where(hero => hero != null && hero.isActive && !hero.IsEnemy)
                : Enumerable.Empty<Unit>();
            foreach (Unit recipient in recipients.ToList())
            {
                recipient.GrantPermanentPassive(choice.grantPassiveCodeId);
            }
        }

        private void MarkCurrentEventTriggered()
        {
            if (currentStageEvent?.oncePerRun == true)
            {
                runManager?.MarkEventTriggered(currentStageEvent.id);
            }
        }

        private bool RecruitSupportUnit(int unitId)
        {
            if (unitId <= 0 || GridManager.Instance == null) return false;
            if (GridManager.Instance.heroList.Any(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.ID == unitId))
            {
                return true;
            }
            if (!GridManager.Instance.HasAvailableBenchSlot())
            {
                for (int x = GridManager.Instance.GetRearColumn(false); x <= GridManager.Instance.GetFrontColumn(false); x++)
                {
                    if (x == 0) continue;
                    for (int y = GridManager.Instance.yMin; y <= GridManager.Instance.yMax; y++)
                    {
                        if (!GridManager.Instance.IsCellAvailable(x, y)) continue;
                        GridManager.Instance.SpawnUnit(x, y, false, unitId);
                        return true;
                    }
                }
                Debug.LogWarning($"[사건] 서포트 유닛 {unitId} 영입 실패: 빈 슬롯이 없습니다.");
                return false;
            }
            GridManager.Instance.SpawnUnit(0, 0, false, unitId, true);
            return GridManager.Instance.heroList.Any(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.ID == unitId);
        }

        private void TakeDamage(int damage)
        {
            life = Mathf.Max(0, life - damage);
            
            // UI 업데이트
            if (uiManager != null)
            {
                uiManager.UpdateLifeText();
            }
            
            // 생명력이 0이 되면 게임 오버
            if (life <= 0)
            {
                gameState = GameState.GameOver;
                Debug.Log("생명력이 0이 되었습니다. 게임 오버!");
                SaveSystem.DeleteSave();
            }
        }

        private void SaveAllyFieldState()
        {
            allyFieldSnapshot.Clear();
            
            // 현재 필드에 있는 아군 유닛들의 좌표와 런 성장 상태를 함께 저장한다.
            foreach (Unit ally in GridManager.Instance.heroList)
            {
                if (ally != null && ally.isActive && ally.currentCell != null)
                {
                    // 벤치가 아닌 필드에 있는 유닛만 저장
                    if (!GridManager.Instance.IsBenchCell(ally.currentCell))
                    {
                        allyFieldSnapshot.Add(BuildUnitSnapshot(ally, false));
                    }
                }
            }
            
            Debug.Log($"아군 필드 상태 저장됨: {allyFieldSnapshot.Count}개 유닛");
        }

        /// <summary>
        /// 전투가 끝난 시점의 궁극기 자원을 전투 전 스냅샷에 덮어쓴다.
        /// 사망한 영웅도 heroList에는 남아 있으므로 패배 때 쌓은 자원까지 보존된다.
        /// </summary>
        private void CaptureAllyUltimateResources()
        {
            if (allyFieldSnapshot == null || GridManager.Instance == null) return;

            foreach (UnitSaveData saved in allyFieldSnapshot)
            {
                Unit ally = GridManager.Instance.heroList.LastOrDefault(hero =>
                    hero != null && !hero.IsEnemy && !hero.IsSummon && hero.ID == saved.unitId &&
                    hero.currentCell != null && hero.currentCell.xPos == saved.xPos && hero.currentCell.yPos == saved.yPos);

                ally ??= GridManager.Instance.heroList.LastOrDefault(hero =>
                    hero != null && !hero.IsEnemy && !hero.IsSummon && hero.ID == saved.unitId);

                if (ally != null) saved.ultimateResource = ally.ManaCurr;
            }
        }

        public void RestoreAllyFieldState()
        {
            if (gameState == GameState.GameOver) return; // 게임 오버 시에는 복원하지 않음
            
            // 현재 필드에 있는 모든 아군 유닛 제거 (벤치는 유지)
            // 현재 필드에 있는 모든 아군 유닛 제거 (벤치는 유지).
            // 비활성화만 하면 리스트와 씬에 잔해가 남아 라운드마다 사본이 쌓이므로 완전히 물린다.
            // 소환수는 칸이 없다. 칸으로만 거르면 리스트에 남아 라운드마다 쌓인다.
            var fieldAllies = GridManager.Instance.heroList.Where(ally =>
                ally != null && (ally.IsSummon ||
                    (ally.currentCell != null && !GridManager.Instance.IsBenchCell(ally.currentCell)))).ToList();

            foreach (Unit ally in fieldAllies)
            {
                GridManager.Instance.RetireUnit(ally);
            }
            GridManager.Instance.PruneUnitLists();
            
            // 저장된 상태로 아군 필드 복원
            foreach (UnitSaveData saved in allyFieldSnapshot)
            {
                GridManager.Instance.SpawnUnit(saved.xPos, saved.yPos, false, saved.unitId, false);
                Unit restored = GridManager.Instance.heroList
                    .LastOrDefault(hero => hero != null && hero.isActive && !hero.IsEnemy && hero.ID == saved.unitId);
                restored?.RestoreRunState(saved);
            }
            
            Debug.Log($"아군 필드 상태 복원됨: {allyFieldSnapshot.Count}개 유닛");
        }

        private static UnitSaveData BuildUnitSnapshot(Unit unit, bool isBench)
        {
            return new UnitSaveData
            {
                unitId = unit.ID,
                currentHP = unit.HpCurr,
                xPos = unit.currentCell.xPos,
                yPos = unit.currentCell.yPos,
                isBench = isBench,
                ultimateResource = unit.ManaCurr,
                // 레벨/EXP를 빠뜨리면 라운드 종료 복원에서 성장이 초기화된다.
                level = unit.Level,
                exp = unit.Exp,
                trainingLevel = unit.TrainingLevel,
                strUpgrade = unit.StrUpgrade,
                dexUpgrade = unit.DexUpgrade,
                conUpgrade = unit.ConUpgrade,
                intUpgrade = unit.IntUpgrade,
                lukUpgrade = unit.LukUpgrade,
                codeAccelerationBonus = unit.CodeAccelerationRunBonus,
                equippedItemIds = unit.EquippedItemIds.ToList(),
                carriedItemIds = unit.CarriedItemIds.ToList(),
                grantedPassiveCodeIds = unit.GrantedPassiveCodeIds.ToList(),
            };
        }

        /// <summary>
        /// 매니저 수명 주기 규칙:
        /// - 씬 배치(Game.unity): GameManager GO(+DataManager/SfxManager/InventoryManager 컴포넌트),
        ///   GridManager, UIManager, DragAndDropManager — 씬 로드 시 함께 생성/파괴된다.
        /// - 런타임 영속(DontDestroyOnLoad): SettingsManager(앱 수명),
        ///   RunManager/RewardManager/CharacterSelectionManager(런 수명) — 이 메서드가 없으면 생성하고,
        ///   런 종료 시 CleanupRunContext()가 런 수명 매니저만 파괴한다.
        /// </summary>
        private void EnsurePersistentManagers()
        {
            if (SettingsManager.Instance == null)
            {
                new GameObject("SettingsManager").AddComponent<SettingsManager>();
            }

            if (RunManager.Instance == null)
            {
                runManager = new GameObject("RunManager").AddComponent<RunManager>();
            }
            else
            {
                runManager = RunManager.Instance;
            }

            if (RewardManager.Instance == null)
            {
                rewardManager = new GameObject("RewardManager").AddComponent<RewardManager>();
            }
            else
            {
                rewardManager = RewardManager.Instance;
            }
        }

        private void StartFromIntent()
        {
            switch (GameStartIntent.Current)
            {
                case GameStartIntent.Intent.NewGame:
                    runManager.StartRun(GameMode.Training);
                    _roundManager.InitializeStage(1);
                    // 테스트 흐름: 시작 대사를 건너뛰고 즉시 캐릭터 선택으로 이동한다.
                    EnterCharacterSelection();
                    break;
                case GameStartIntent.Intent.InfiniteMode:
                    runManager.StartRun(GameMode.Infinite);
                    _roundManager.InitializeStage(1);
                    EnterCharacterSelection();
                    break;
                case GameStartIntent.Intent.Continue:
                    if (runManager.LoadSavedRun())
                    {
                        if (gameState == GameState.Preparation)
                        {
                            StartPreparationTimer();
                        }
                    }
                    else
                    {
                        _roundManager.InitializeStage(1);
                        EnterCharacterSelection();
                    }
                    break;
                case GameStartIntent.Intent.DirectStart:
                default:
                    runManager.StartRun(GameMode.Training);
                    _roundManager.InitializeStage(1);
                    _roundManager.LoadRound(1);
                    gameState = GameState.Preparation;
                    StartPreparationTimer();
                    break;
            }

            GameStartIntent.Current = GameStartIntent.Intent.DirectStart;
        }

        private void EnterCharacterSelection()
        {
            gameState = GameState.CharacterSelection;
            uiManager?.ShowCharacterSelection();
        }

        /// <summary>
        /// 인트로 시퀀스를 재생하고 끝나면 <paramref name="onComplete"/>를 실행한다.
        /// 인트로 데이터가 없으면 바로 다음 흐름으로 넘어간다(데이터 누락이 진행을 막지 않는다).
        /// </summary>
        private void PlayIntroThen(Action onComplete)
        {
            StageEventData intro = LoadIntroSequence("new_game_intro");
            if (intro == null)
            {
                onComplete?.Invoke();
                return;
            }

            EnterEvent(intro, onComplete);
        }

        private StageEventData LoadIntroSequence(string introId)
        {
            IntroDataList introData = dataManager?.FetchIntroDataList();
            List<StageEventData> intros = introData?.intros;
            if (intros == null || intros.Count == 0)
            {
                Debug.Log("[인트로] 인트로 데이터가 없어 건너뜁니다 (Data/00_intro.yaml).");
                return null;
            }

            StageEventData match = intros.FirstOrDefault(entry => entry != null && entry.id == introId)
                ?? intros.FirstOrDefault(entry => entry != null);
            return match;
        }

        private static void CleanupRunContext()
        {
            CharacterSelectionManager.DestroyInstance();
            RunManager.DestroyInstance();
            RewardManager.DestroyInstance();

            if (Instance != null)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
        }
    }
}
