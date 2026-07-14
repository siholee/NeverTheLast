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
            SceneManager.LoadScene("MainMenu");
        }

        public static void LoadBattleScene()
        {
            if (Instance != null)
            {
                CleanupRunContext();
            }

            SceneManager.LoadScene("Game");
        }

        public GameState gameState;
        public int KillCount;

        // 준비 단계 타이머 관련
        public float preparationTime = 30f; // 준비 시간 (초)
        private float currentPreparationTime;
        private bool isPreparationTimerActive = false;

        // 라운드 진행 타이머 관련
        public float roundProgressTime = 60f; // 라운드 진행 시간 (초)
        private float currentRoundProgressTime;
        private bool isRoundProgressTimerActive = false;
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
                        RunEventCheckpoint(() => runManager?.AdvanceAfterTrainingPhase());
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
            currentRoundProgressTime = roundProgressTime;
            isRoundProgressTimerActive = true;

            Debug.LogWarning("[GameManager] 🔥 라운드 시작 - GridManager.OnRoundStart() 호출");
            GridManager.Instance.OnRoundStart();
        }

        public void OnKillEnemy()
        {
            KillCount++;
            inventoryManager?.AddGold(25 * Mathf.Max(1, _roundManager?.Stage ?? 1));
            
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
                currentRoundProgressTime -= Time.deltaTime;
                
                // 남은 적 수 계산 (필드의 적 + 스폰 대기 중인 적)
                int remainingEnemies = GetRemainingEnemyCount();
                
                // 아군 전멸 체크
                if (AreAllAlliesDefeated())
                {
                    EndRoundByAllyDefeat();
                    return;
                }
                
                // UI 업데이트
                int displayTime = Mathf.Max(0, Mathf.CeilToInt(currentRoundProgressTime));
                if (uiManager != null)
                {
                    uiManager.UpdateGameStatusWithEnemyCount(gameState, displayTime, remainingEnemies);
                }
                
                // 시간이 다 되면 라운드 종료 (시간 초과)
                if (currentRoundProgressTime <= 0)
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
        }

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
        /// 테마 고정 슬롯(StageInRound == 5) 사건 진입. 스테이지 자체가 사건으로 대체되며,
        /// 사건 종료 후에는 다음 스테이지로 진행한다.
        /// </summary>
        private void EnterStageSlotEvent(StageEventData stageEvent)
        {
            EnterEvent(stageEvent ?? BuildFallbackEvent(), () => runManager?.AdvanceAfterReward());
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
            currentStageEvent = stageEvent;
            pendingEventChoice = null;
            eventDialogueIndex = 0;
            uiManager?.ShowEventStagePanel(currentStageEvent, eventDialogueIndex);
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
                return;
            }

            if (choice.battleEnemyId > 0)
            {
                BeginEventBattle(choice);
                return;
            }

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
            currentRoundProgressTime = roundProgressTime;
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
                runManager?.AdvanceAfterReward();
            }
        }

        private StageEventData BuildFallbackEvent()
        {
            string theme = _roundManager?.CurrentThemeName ?? "낯선";
            return new StageEventData
            {
                id = "fallback_event",
                themeId = _roundManager?.CurrentThemeId ?? 0,
                stageInRound = _roundManager?.StageInRound ?? 5,
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

            TrainingManager.TrainingResult result = TrainingManager.ApplyTraining(focus);
            uiManager?.HideTrainingPhasePanel();
            preparationActionUsed = true;
            gameState = GameState.Preparation;
            runManager?.SaveCurrentRun();
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed, BuildTrainingResultMessage(result));
        }

        public void OpenTrainingFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;

            isPreparationTimerActive = false;
            gameState = GameState.TrainingPhase;
            uiManager?.HidePreparationPhasePanel();
            uiManager?.ShowTrainingPhasePanel();
        }

        public void RestFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;

            HealAllActiveHeroes();
            preparationActionUsed = true;
            runManager?.SaveCurrentRun();
            uiManager?.ShowPreparationPhasePanel(IsPreparationLimitedToDeck(), preparationActionUsed, "휴식 완료: 모든 활성 아군의 체력을 회복했습니다.");
        }

        public void BeginAdditionalBattleFromPreparation()
        {
            if (gameState != GameState.Preparation || preparationActionUsed || IsPreparationLimitedToDeck()) return;

            preparationActionUsed = true;
            StartRound();
        }

        public void OpenDeckSetupFromPreparation()
        {
            if (gameState != GameState.Preparation) return;

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

            return $"훈련 완료: {FormatPrimaryStat(result.Focus)} +{result.StatGain}, 트레이닝 Lv.{result.NewTrainingLevel}{supports}{transferred}";
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
            
            // 아군 필드 상태 복원 (게임 오버가 아닌 경우에만)
            if (life > 0)
            {
                RestoreAllyFieldState();
            }
            GridManager.Instance?.ClearActiveEnemies();
            
            if (life <= 0)
            {
                SaveSystem.DeleteSave();
                return;
            }

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
                if (resolvedChoice.grantUnitId > 0)
                {
                    RecruitSupportUnit(resolvedChoice.grantUnitId);
                }
                if (resolvedChoice.grantItemId > 0)
                {
                    inventoryManager?.AddItem(resolvedChoice.grantItemId);
                }
                runManager?.SaveCurrentRun();
            }

            string resultText = victory
                ? resolvedChoice?.successText ?? "전투에서 승리했다."
                : resolvedChoice?.failureText ?? "전투에서 물러났다.";
            uiManager?.ShowEventResolution(resultText);
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
            var fieldAllies = GridManager.Instance.heroList.Where(ally => 
                ally != null && ally.isActive && ally.currentCell != null && 
                !GridManager.Instance.IsBenchCell(ally.currentCell)).ToList();
            
            foreach (Unit ally in fieldAllies)
            {
                ally.DeactivateUnit();
            }
            
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
                trainingLevel = unit.TrainingLevel,
                strUpgrade = unit.StrUpgrade,
                dexUpgrade = unit.DexUpgrade,
                conUpgrade = unit.ConUpgrade,
                intUpgrade = unit.IntUpgrade,
                lukUpgrade = unit.LukUpgrade,
                codeAccelerationBonus = unit.CodeAccelerationRunBonus,
                equippedItemIds = unit.EquippedItemIds.ToList(),
            };
        }

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
                    gameState = GameState.CharacterSelection;
                    uiManager?.ShowCharacterSelection();
                    break;
                case GameStartIntent.Intent.InfiniteMode:
                    runManager.StartRun(GameMode.Infinite);
                    _roundManager.InitializeStage(1);
                    gameState = GameState.CharacterSelection;
                    uiManager?.ShowCharacterSelection();
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
                        gameState = GameState.CharacterSelection;
                        uiManager?.ShowCharacterSelection();
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
