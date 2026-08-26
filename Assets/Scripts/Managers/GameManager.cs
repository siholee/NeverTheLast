using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
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
            isPreparationTimerActive = false;
            uiManager?.HidePreparationPhasePanel();

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
        public const int ExpPerKillBase = 10;
        public const int ExpPerKillPerStage = 2;
        public const int ExpPerStageClearBase = 50;
        public const int ExpPerStageClearPerStage = 10;
        public const int ExpPerTrainingBase = 30;
        public const int ExpPerTrainingPerStage = 5;

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
            if (amount <= 0 || GridManager.Instance == null) return;

            int scaled = Mathf.Max(1, Mathf.RoundToInt(
                amount * Codes.Passive.RewardModifiers.ExpMultiplier()));

            foreach (Unit hero in GridManager.Instance.heroList)
            {
                if (hero == null || hero.IsEnemy || !hero.isActive) continue;
                hero.AddExp(scaled);
            }
        }

        private int CurrentStageForExp => Mathf.Max(1, _roundManager?.Stage ?? 1);

        public void OnKillEnemy()
        {
            KillCount++;
            // 상인(273)이 필드에 있으면 획득 골드가 늘어난다.
            int gold = Mathf.RoundToInt(
                25 * Mathf.Max(1, _roundManager?.Stage ?? 1) * Codes.Passive.RewardModifiers.GoldMultiplier());
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

#if UNITY_EDITOR
            HandleDebugInput();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용 디버그 입력. 빌드에는 포함되지 않는다.
        /// F9: 현재 테마의 사건을 즉시 실행(사건 연출 확인용). 테마 사건이 없으면 첫 사건을 사용한다.
        /// </summary>
        private void HandleDebugInput()
        {
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F9)) return;
            if (gameState == GameState.EventStage) return;

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

            ApplyEventChoiceRewards(choice);
            runManager?.SaveCurrentRun();
            uiManager?.ShowEventResolution(choice.successText ?? "사건이 끝났다.");
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

            gameState = GameState.RoundInProgress;
            remainingRoundTurns = roundTurnLimit;
            isRoundProgressTimerActive = true;
            GridManager.Instance.OnRoundStart();
        }

        public void CompleteEventStage()
        {
            if (gameState != GameState.EventStage) return;
            uiManager?.HideEventStagePanel();
            currentStageEvent = null;
            pendingEventChoice = null;
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

            TrainingManager.TrainingResult result = TrainingManager.ApplyTraining(focus);

            // 육성 EXP도 파티 전체가 나눠 받는다.
            // 메인에게만 주면 서포터가 100스테이지에서 26레벨까지 뒤처져,
            // 후반에 서포터가 제 몫을 못 하는 구조가 된다. 성장 속도는 5인이 동일하다.
            // (집중 훈련의 스탯 보너스는 여전히 메인에게만 붙는다 — 차별화는 그쪽이 담당한다.)
            GrantExpToParty(ExpPerTrainingBase + ExpPerTrainingPerStage * CurrentStageForExp);
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

        public void OpenTrainingFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            isPreparationTimerActive = false;
            gameState = GameState.TrainingPhase;
            uiManager?.HidePreparationPhasePanel();
            uiManager?.ShowTrainingPhasePanel();
        }

        /// <summary>휴식 한 번이 되찾아 주는 훈련 체력.</summary>
        public const int RestEnergyRecovery = 40;

        public void RestFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;
            if (!AllowProgressWhileWithinCarryLimit()) return;

            HealAllActiveHeroes();

            // 훈련 체력도 함께 회복한다. 우마무스메의 휴식이 하는 일이 이것이다.
            TrainingManager.State.RestoreEnergy(RestEnergyRecovery);

            // 휴식도 턴을 쓴다. 서포트는 다음 훈련에서 다른 자리에 앉는다.
            TrainingManager.InvalidateSupportPlacement();

            preparationActionUsed = true;
            runManager?.SaveCurrentRun();
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed,
                $"휴식 완료: 아군 체력을 회복하고 훈련 체력을 {RestEnergyRecovery} 되찾았습니다 " +
                $"(현재 {TrainingManager.State.Energy}).");
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

            uiManager?.ShowPreparationPhasePanel(
                IsPreparationLimitedToDeck(),
                preparationActionUsed,
                "덱 구성: 필드에서 배치를 정리한 뒤 전투 시작을 누르세요.");
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

        public void RestorePreparationActionState(bool actionUsed)
        {
            preparationActionUsed = actionUsed;
            if (gameState == GameState.Preparation)
            {
                uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed);
            }
        }

        private void HealAllActiveHeroes()
        {
            foreach (Unit hero in GridManager.Instance.heroList)
            {
                if (hero != null && hero.isActive && !hero.IsEnemy)
                {
                    hero.ModifyHp(hero.HpMax);
                }
            }
        }

        private static string BuildTrainingResultMessage(TrainingManager.TrainingResult result)
        {
            string transferred = result.TransferredPassiveIds != null && result.TransferredPassiveIds.Count > 0
                ? $" / 전수 스킬 {string.Join(", ", result.TransferredPassiveIds)}"
                : "";
            string supports = result.SupportMessages != null && result.SupportMessages.Count > 0
                ? $" / 서포트 {result.SupportMessages.Count}명 판정"
                : "";

            if (result.Failed)
            {
                return $"훈련 실패 ({result.FailureRate}%): {FormatPrimaryStat(result.Focus)} 상승 없음, " +
                       $"체력 {result.EnergySpent} 소모 (남은 체력 {result.EnergyAfter}){supports}";
            }

            return $"훈련 완료: {FormatPrimaryStat(result.Focus)} +{result.StatGain}, " +
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
            EndRound(true);
        }

        private void EndRoundByAllyDefeat()
        {
            // 아군 전멸로 인한 라운드 종료 (패배 처리)
            isRoundProgressTimerActive = false;
            
            // 남은 적 수만큼 생명력 차감 (패배 페널티)
            int remainingEnemies = GetRemainingEnemyCount();
            TakeDamage(remainingEnemies);
            
            Debug.Log($"아군이 전멸했습니다! 남은 적 {remainingEnemies}마리만큼 생명력 차감. 현재 생명력: {life}");
            
            // 라운드 종료 처리
            EndRound(false);
        }

        private void EndRound(bool victory)
        {
            _roundManager?.StopRound();
            gameState = GameState.RoundEnd;
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
                SaveSystem.DeleteSave();
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

            if (eventBattleInProgress)
            {
                ResolveEventBattle(victory);
                return;
            }

            if (victory)
            {
                // 전투 승리 직후·보상 표시 직전(after battle, before reward)은 사건 발생 지점이다.
                // 예약된 사건이 있으면 먼저 처리하고, 끝나면 보상 화면을 연다.
                RunEventCheckpoint(ShowRewardSelection);
            }
            else
            {
                runManager?.SaveCurrentRun();
                NextGameState(false);
            }
        }

        private void ShowRewardSelection()
        {
            gameState = GameState.RewardSelection;
            var rewards = rewardManager != null
                ? rewardManager.GenerateRewards(3, _roundManager?.Round ?? 1, CurrentMode, _roundManager?.CurrentThemeId ?? 0)
                : new List<RewardDef>();
            uiManager?.ShowRewardPanel(rewards);
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

        public void RestoreAllyFieldState()
        {
            if (gameState == GameState.GameOver) return; // 게임 오버 시에는 복원하지 않음
            
            // 현재 필드에 있는 모든 아군 유닛 제거 (벤치는 유지)
            // 현재 필드에 있는 모든 아군 유닛 제거 (벤치는 유지).
            // 비활성화만 하면 리스트와 씬에 잔해가 남아 라운드마다 사본이 쌓이므로 완전히 물린다.
            var fieldAllies = GridManager.Instance.heroList.Where(ally =>
                ally != null && ally.currentCell != null &&
                !GridManager.Instance.IsBenchCell(ally.currentCell)).ToList();

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
