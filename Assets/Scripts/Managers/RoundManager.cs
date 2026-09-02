using System.Collections.Generic;
using System.Linq;
using Core;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>스테이지의 성격. HUD 진행 예고에서 칸 색과 기호를 정하는 데 쓴다.</summary>
    public enum StageKind
    {
        Normal,
        Elite,
        Event,
        MidBoss,
        Boss,
    }

    public class RoundManager
    {
        private readonly DataManager _dataManager;
        public int Stage { get; private set; }  // 현재 전투 스테이지
        public int Round { get; private set; }  // 10스테이지마다 증가하는 라운드
        // 누적 전투 라운드 기준 성장 레벨. 1번째 전투는 기본 스탯(성장 0회),
        // n번째 전투는 각 스탯의 IncrementLvl을 (n - 1)회 적용한다.
        /// <summary>적 유닛의 레벨. 현재 스테이지와 같다(1스테이지 = Level 1 = 성장분 0).</summary>
        public int EnemyLevel => Mathf.Max(1, Stage);
        public int StageInRound => ((Stage - 1) % 10) + 1;

        /// <summary>
        /// 이 스테이지가 소비하는 <b>내용 슬롯</b>. 평소에는 <see cref="StageInRound"/>과 같다.
        ///
        /// 고정 보스가 낀 라운드는 <b>2번 슬롯을 건너뛰고 이후를 1씩 당긴다.</b>
        /// 그래서 마지막 한 칸이 비고 거기에 고정 보스가 들어간다.
        ///
        /// <code>
        /// 평소       1  2  3  4  5(사건) 6  7  8  9  10(보스)
        /// 고정 보스  1  3  4  5(사건)    6  7  8  9  10(보스)  고정 보스
        /// </code>
        ///
        /// 결과적으로 테마 보스는 9번째 전투로 당겨진다 — 기존 규칙과 같은 자리다.
        /// </summary>
        public int ContentSlotInRound => ContentSlot(Stage);
        public int CurrentThemeId => _currentStageTheme?.id ?? 0;
        public string CurrentThemeName => _currentStageTheme?.name ?? "";
        public bool IsCurrentBossStage => IsBossStage(Stage);
        public bool IsRoundInProgress { get; private set; }
        private int _cachedEventStage = int.MinValue;
        private StageEventData _cachedEvent;
        private StageEventData RawCurrentEventData => SelectCurrentEventData();
        private StageEventData CurrentEventData => IsEventEligible(RawCurrentEventData) ? RawCurrentEventData : null;

        /// <summary>
        /// 현재 스테이지가 테마 고정 슬롯(내용 슬롯 5) 사건 스테이지라면 true를 반환한다.
        /// stageEvent는 테마에 정의된 사건이 없으면 null일 수 있다(호출부에서 대체 사건 사용).
        /// 고정 보스 스테이지에서는 사건이 발생하지 않으며, 사건 전투 중에는 재진입하지 않는다.
        /// </summary>
        public bool TryGetScheduledEvent(out StageEventData stageEvent)
        {
            stageEvent = null;
            if (IsRoundInProgress || !IsEventStage()) return false;
            stageEvent = CurrentEventData;
            return true;
        }

        /// <summary>동적 체크포인트에서 직접 예약할 사건을 ID로 찾는다.</summary>
        public StageEventData GetEventById(string eventId)
        {
            EnsureDataLoaded();
            return string.IsNullOrWhiteSpace(eventId)
                ? null
                : _stageThemeDataList?.events?.FirstOrDefault(data => data?.id == eventId);
        }

        private StageEventData SelectCurrentEventData()
        {
            if (_cachedEventStage == Stage) return _cachedEvent;
            _cachedEventStage = Stage;
            _cachedEvent = null;

            List<StageEventData> events = _stageThemeDataList?.events;
            if (events == null) return null;

            int slot = ContentSlotInRound;
            List<StageEventData> unlockedTierEvents = events
                .Where(data => data != null && data.themeId == 0 && data.stageInRound == slot &&
                               data.tier > 0 && IsEventEligible(data))
                .ToList();
            if (unlockedTierEvents.Count > 0)
            {
                // 같은 티어의 영입 사건은 별개 사건으로 취급하며 무작위로 하나만 제시한다.
                int highestTier = unlockedTierEvents.Max(data => data.tier);
                List<StageEventData> highest = unlockedTierEvents
                    .Where(data => data.tier == highestTier)
                    .ToList();
                _cachedEvent = highest[UnityEngine.Random.Range(0, highest.Count)];
                return _cachedEvent;
            }

            _cachedEvent = events.FirstOrDefault(data =>
                data != null && data.themeId == CurrentThemeId && data.stageInRound == slot);
            return _cachedEvent;
        }

        private StageThemeDataList _stageThemeDataList;
        private StageThemeData _currentStageTheme;
        private List<StageThemeData> _activeThemes;
        private EnemyDataList _enemyDataList;
        private RoundTypeDataList _roundTypeDataList;

        public RoundManager(DataManager manager)
        {
            _dataManager = manager;
            Stage = 1;
            Round = 1;
        }

        /// <summary>
        /// 스테이지 시작 시 호출. 스테이지 테마를 랜덤으로 선택
        /// </summary>
        public void InitializeStage(int stageNumber)
        {
            Stage = Mathf.Max(1, stageNumber);
            Round = GetRewardRound(Stage);
            
            EnsureDataLoaded();
            EnsureThemeForCurrentRound();
        }

        /// <summary>
        /// 라운드 시작. 적을 즉시 소환
        /// </summary>
        public void LoadRound(int roundNumber)
        {
            Stage = Mathf.Max(1, roundNumber);
            Round = GetRewardRound(Stage);

            EnsureDataLoaded();
            EnsureThemeForCurrentRound();

            if (_enemyDataList == null || _roundTypeDataList == null || _currentStageTheme == null)
            {
                Debug.LogError("Enemy data, round type data, or stage theme data not loaded.");
                return;
            }

            MarkSuppressedOnceEventIfBlocked();

            if (IsEventStage())
            {
                IsRoundInProgress = false;
                Debug.Log($"Battle Stage {Stage}, Reward Round {Round} is event stage for theme {CurrentThemeName}");
                return;
            }
            
            // 스테이지 데이터에서 현재 라운드의 roundType ID 가져오기
            StageData stageData = _roundTypeDataList.stages.Find(s => s.stageNumber == 1);
            if (stageData == null || stageData.rounds == null || stageData.rounds.Count == 0)
            {
                Debug.LogError($"Stage pattern data not found for battle stage {Stage}.");
                return;
            }
            
            int patternIndex = (Stage - 1) % stageData.rounds.Count;
            int roundTypeId = stageData.rounds[patternIndex];
            RoundTypeData roundType = _roundTypeDataList.roundTypes.Find(rt => rt.id == roundTypeId);
            
            if (roundType == null)
            {
                Debug.LogError($"RoundType {roundTypeId} not found.");
                return;
            }

            if (roundType.isBoss && CurrentRoundHasFixedBoss())
            {
                roundType = _roundTypeDataList.roundTypes.FirstOrDefault(rt => !rt.isBoss && !rt.isElite) ?? roundType;
            }
            
            // 적 소환
            SpawnEnemiesForStage(roundType);
            
            Debug.Log($"Battle Stage {Stage}, Reward Round {Round} started with {roundType.name}");
            IsRoundInProgress = true;
        }

        public bool TryLoadNextRound()
        {
            if (_roundTypeDataList == null)
            {
                _roundTypeDataList = _dataManager.FetchRoundTypeDataList();
            }

            StageData stageData = _roundTypeDataList?.stages?.Find(s => s.stageNumber == 1);
            if (stageData?.rounds != null && stageData.rounds.Count > 0)
            {
                LoadRound(Stage + 1);
                return true;
            }

            Debug.Log("[RoundManager] 라운드 패턴 데이터가 없어 다음 스테이지를 로드할 수 없습니다.");
            return false;
        }

        private static int GetRewardRound(int battleStage)
        {
            return Mathf.Max(1, ((battleStage - 1) / 10) + 1);
        }

        private void EnsureDataLoaded()
        {
            _stageThemeDataList ??= _dataManager.FetchStageThemeDataList();
            _enemyDataList ??= _dataManager.FetchEnemyDataList();
            _roundTypeDataList ??= _dataManager.FetchRoundTypeDataList();
        }

        private void EnsureThemeForCurrentRound()
        {
            if (_stageThemeDataList?.stageThemes == null || _stageThemeDataList.stageThemes.Count == 0)
            {
                Debug.LogError("Failed to load stage theme data.");
                return;
            }

            List<StageThemeData> themes = ActiveThemes();
            int themeIndex = (Round - 1) % themes.Count;
            StageThemeData selectedTheme = themes[themeIndex];
            if (_currentStageTheme?.id == selectedTheme.id) return;

            _currentStageTheme = selectedTheme;
            Debug.Log($"Round {Round} theme: {_currentStageTheme.name}");
        }

        /// <summary>
        /// 런에 실제로 도는 테마 목록. <c>enabled: false</c>인 테마는 빠진다.
        /// 전부 꺼져 있으면 데이터가 잘못된 것이므로 원래 목록을 그대로 쓴다
        /// (런이 시작조차 못 하는 것보다는 낫다).
        /// </summary>
        private List<StageThemeData> ActiveThemes()
        {
            if (_activeThemes != null) return _activeThemes;

            _activeThemes = _stageThemeDataList.stageThemes.Where(theme => theme.enabled).ToList();
            if (_activeThemes.Count == 0)
            {
                Debug.LogError("[RoundManager] 켜져 있는 스테이지 테마가 하나도 없습니다. 전체 목록을 그대로 씁니다.");
                _activeThemes = _stageThemeDataList.stageThemes;
            }

            return _activeThemes;
        }

        /// <summary>현재 테마의 중간 보스 슬롯. 데이터가 비어 있으면 기존 규칙인 8을 쓴다.</summary>
        private int MidBossStageInRound =>
            (_currentStageTheme?.midBossStageInRound ?? 0) > 0 ? _currentStageTheme.midBossStageInRound : 8;

        private bool IsEventStage()
        {
            if (IsFixedBossStage(Stage)) return false;
            if (RawCurrentEventData != null) return CurrentEventData != null;
            if (CurrentEventData != null) return true;
            return ContentSlotInRound == 5;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 디버그용: 사건 연출을 즉시 확인하기 위한 표본 사건을 반환한다.
        /// 현재 테마의 사건을 우선 사용하고, 없으면 정의된 첫 사건을 사용한다.
        /// 실제 진행(oncePerRun 기록 등)에는 영향을 주지 않는다.
        /// </summary>
        public StageEventData GetDebugSampleEvent()
        {
            EnsureDataLoaded();
            List<StageEventData> events = _stageThemeDataList?.events;
            if (events == null || events.Count == 0) return null;

            return events.FirstOrDefault(data => data != null && data.themeId == CurrentThemeId)
                ?? events.FirstOrDefault(data => data != null);
        }
#endif

        private static bool IsEventEligible(StageEventData stageEvent)
        {
            if (stageEvent == null) return false;
            if (stageEvent.requiresBossDefeatId > 0 &&
                !SaveSystem.HasDefeatedBoss(stageEvent.requiresBossDefeatId)) return false;
            RunManager runManager = GameManager.Instance?.runManager;
            if (stageEvent.oncePerRun && runManager != null && runManager.HasTriggeredEvent(stageEvent.id)) return false;
            int recruitUnitId = stageEvent.choices?
                .Select(choice => choice?.grantUnitId ?? 0)
                .FirstOrDefault(unitId => unitId > 0) ?? 0;
            if (recruitUnitId > 0 && SaveSystem.IsStarterUnlocked(recruitUnitId)) return false;
            return !IsBlockedByDeck(stageEvent);
        }

        private static bool IsBlockedByDeck(StageEventData stageEvent)
        {
            if (stageEvent?.blockedUnitIds == null || stageEvent.blockedUnitIds.Count == 0) return false;
            return GridManager.Instance != null && GridManager.Instance.heroList.Any(hero =>
                hero != null && hero.isActive && !hero.IsEnemy && stageEvent.blockedUnitIds.Contains(hero.ID));
        }

        private void MarkSuppressedOnceEventIfBlocked()
        {
            StageEventData stageEvent = RawCurrentEventData;
            RunManager runManager = GameManager.Instance?.runManager;
            if (stageEvent?.oncePerRun != true || runManager == null || runManager.HasTriggeredEvent(stageEvent.id)) return;
            if (IsBlockedByDeck(stageEvent)) runManager.MarkEventTriggered(stageEvent.id);
        }

        public bool CurrentThemeHasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && _currentStageTheme?.tags != null &&
                   _currentStageTheme.tags.Any(value => string.Equals(value, tag, System.StringComparison.OrdinalIgnoreCase));
        }

        private bool IsFixedBossStage(int stage)
        {
            return GetFixedBossId(stage) > 0;
        }

        private bool CurrentRoundHasFixedBoss() => RoundHasFixedBoss(Round);

        private bool RoundHasFixedBoss(int rewardRound)
        {
            if (_stageThemeDataList?.fixedBossStages == null) return false;

            return _stageThemeDataList.fixedBossStages.Any(data => GetRewardRound(data.stage) == rewardRound);
        }

        /// <summary>
        /// 임의 스테이지의 내용 슬롯. 규칙은 <see cref="ContentSlotInRound"/>을 본다.
        /// 고정 보스 스테이지 자신은 어떤 슬롯에도 대응하지 않는다(11을 반환하고 호출부가 먼저 걸러낸다).
        /// </summary>
        private int ContentSlot(int stage)
        {
            int stageInRound = ((stage - 1) % 10) + 1;
            if (!RoundHasFixedBoss(GetRewardRound(stage))) return stageInRound;

            return stageInRound >= 2 ? stageInRound + 1 : stageInRound;
        }

        private bool IsBossStage(int stage)
        {
            if (GetFixedBossId(stage) > 0) return true;

            return ContentSlot(stage) == 10;
        }

        /// <summary>현재 스테이지에 실제 배치되는 보스 ID. 보스 스테이지가 아니면 0.</summary>
        public int CurrentBossId
        {
            get
            {
                int fixedBoss = GetFixedBossId(Stage);
                if (fixedBoss > 0) return fixedBoss;
                return ContentSlotInRound == 10 ? _currentStageTheme?.bossId ?? 0 : 0;
            }
        }

        private int GetFixedBossId(int stage)
        {
            FixedBossStageData fixedBoss = _stageThemeDataList?.fixedBossStages?
                .FirstOrDefault(data => data.stage == stage);
            return fixedBoss?.bossId ?? 0;
        }

        /// <summary>
        /// 임의의 스테이지가 어떤 성격인지 판정한다. HUD의 진행 예고 표시에 쓴다.
        /// 스폰 로직(<see cref="SpawnEnemiesForStage"/>)과 같은 규칙을 따르므로
        /// 규칙을 바꿀 때는 두 곳을 함께 고쳐야 한다.
        /// </summary>
        public StageKind GetStageKind(int stage)
        {
            EnsureDataLoaded();

            if (GetFixedBossId(stage) > 0) return StageKind.Boss;

            int slot = ContentSlot(stage);

            if (slot == 10) return StageKind.Boss;
            // 중보스 여부는 테마마다 다르지만, 예고는 현재 테마 기준으로만 근사한다.
            if (slot == MidBossStageInRound && (_currentStageTheme?.midBossId ?? 0) > 0) return StageKind.MidBoss;
            if (slot == 5) return StageKind.Event;
            return StageKind.Normal;
        }

        /// <summary>현재 스테이지부터 count개의 진행 예고를 반환한다.</summary>
        public List<StageKind> GetUpcomingStageKinds(int count)
        {
            var kinds = new List<StageKind>();
            for (int i = 0; i < Mathf.Max(0, count); i++)
            {
                kinds.Add(GetStageKind(Stage + i));
            }

            return kinds;
        }

        public void StopRound()
        {
            IsRoundInProgress = false;
        }

        public bool StartEventBattle(int enemyId)
        {
            if (enemyId <= 0) return false;
            EnsureDataLoaded();
            IsRoundInProgress = true;
            PlaceEnemies(new List<int> { enemyId });
            return true;
        }

        /// <summary>
        /// 라운드 타입에 따라 적을 소환
        /// </summary>
        private void SpawnEnemiesForStage(RoundTypeData roundType)
        {
            List<int> enemyIdsToSpawn = new List<int>();

            int fixedBossId = GetFixedBossId(Stage);
            if (fixedBossId > 0)
            {
                enemyIdsToSpawn.Add(fixedBossId);
                PlaceEnemies(enemyIdsToSpawn);
                return;
            }

            // 테마가 이 스테이지의 편성을 직접 적어 두었으면 그쪽이 우선한다.
            // 보스에게 호위를 붙이려면 중간 보스·보스 단독 스폰보다 먼저 봐야 한다.
            int slot = ContentSlotInRound;
            ThemeStagePatternData themeStagePattern = _currentStageTheme.stagePatterns?
                .FirstOrDefault(data => data.stageInRound == slot);
            bool hasThemePattern = themeStagePattern?.patterns != null && themeStagePattern.patterns.Count > 0;

            if (!hasThemePattern && slot == MidBossStageInRound && _currentStageTheme.midBossId > 0)
            {
                enemyIdsToSpawn.Add(_currentStageTheme.midBossId);
                PlaceEnemies(enemyIdsToSpawn);
                return;
            }

            if (!hasThemePattern && slot == 10 && _currentStageTheme.bossId > 0)
            {
                enemyIdsToSpawn.Add(_currentStageTheme.bossId);
                PlaceEnemies(enemyIdsToSpawn);
                return;
            }

            if (hasThemePattern)
            {
                RoundPattern themePattern = SelectRandomPattern(themeStagePattern.patterns);
                if (themePattern?.enemyIds != null)
                {
                    foreach (int enemyId in themePattern.enemyIds)
                    {
                        if (_enemyDataList.enemies.Any(enemy => enemy.id == enemyId))
                        {
                            enemyIdsToSpawn.Add(enemyId);
                        }
                    }
                }
                if (themePattern?.archetypes != null)
                {
                    foreach (int archetypeId in themePattern.archetypes)
                    {
                        int enemyId = GetRandomEnemyByThemeAndArchetype(_currentStageTheme.enemyThemeId, archetypeId);
                        if (enemyId != -1) enemyIdsToSpawn.Add(enemyId);
                    }
                }
                PlaceEnemies(enemyIdsToSpawn);
                return;
            }
            
            // 패턴 중 하나를 가중치에 따라 랜덤 선택
            RoundPattern selectedPattern = SelectRandomPattern(roundType.patterns);
            
            if (selectedPattern == null)
            {
                Debug.LogError($"No valid pattern found for round type {roundType.name}");
                return;
            }
            
            // 보스 라운드
            if (roundType.isBoss)
            {
                enemyIdsToSpawn.Add(selectedPattern.bossId);
                
                // 보스 호위 추가 (패턴의 archetypes)
                if (selectedPattern.archetypes != null)
                {
                    foreach (int archetypeId in selectedPattern.archetypes)
                    {
                        int enemyId = GetRandomEnemyByThemeAndArchetype(_currentStageTheme.enemyThemeId, archetypeId);
                        if (enemyId != -1) enemyIdsToSpawn.Add(enemyId);
                    }
                }
            }
            // 엘리트 라운드
            else if (roundType.isElite)
            {
                // 엘리트 적 추가
                if (selectedPattern.eliteIds != null && selectedPattern.eliteIds.Count > 0)
                {
                    foreach (int eliteId in selectedPattern.eliteIds)
                    {
                        enemyIdsToSpawn.Add(eliteId);
                    }
                }
                
                // 일반 적 추가 (패턴의 archetypes)
                if (selectedPattern.archetypes != null)
                {
                    foreach (int archetypeId in selectedPattern.archetypes)
                    {
                        int enemyId = GetRandomEnemyByThemeAndArchetype(_currentStageTheme.enemyThemeId, archetypeId);
                        if (enemyId != -1) enemyIdsToSpawn.Add(enemyId);
                    }
                }
            }
            // 일반 라운드
            else
            {
                if (selectedPattern.archetypes != null)
                {
                    foreach (int archetypeId in selectedPattern.archetypes)
                    {
                        int enemyId = GetRandomEnemyByThemeAndArchetype(_currentStageTheme.enemyThemeId, archetypeId);
                        if (enemyId != -1) enemyIdsToSpawn.Add(enemyId);
                    }
                }
            }
            
            // 적 배치 (직업에 따라 행 결정, 아래부터 채움)
            PlaceEnemies(enemyIdsToSpawn);
        }

        /// <summary>
        /// 가중치에 따라 랜덤으로 패턴 선택
        /// </summary>
        private RoundPattern SelectRandomPattern(List<RoundPattern> patterns)
        {
            if (patterns == null || patterns.Count == 0) return null;
            
            int totalWeight = 0;
            foreach (var pattern in patterns)
            {
                totalWeight += pattern.weight;
            }
            
            int randomValue = Random.Range(0, totalWeight);
            int currentWeight = 0;
            
            foreach (var pattern in patterns)
            {
                currentWeight += pattern.weight;
                if (randomValue < currentWeight)
                {
                    return pattern;
                }
            }
            
            // 기본값으로 첫 번째 패턴 반환
            return patterns[0];
        }

        /// <summary>
        /// 테마와 적 분류로 랜덤 적 ID를 가져옴
        /// </summary>
        private int GetRandomEnemyByThemeAndArchetype(int themeId, int archetypeId)
        {
            List<EnemyData> matchingEnemies = _enemyDataList.enemies
                .Where(e => e.themeId == themeId && e.archetype == archetypeId && e.tier == "normal")
                .ToList();
            
            if (matchingEnemies.Count == 0)
            {
                Debug.LogWarning($"No enemy found for theme {themeId} and archetype {archetypeId}");
                return -1;
            }
            
            return matchingEnemies[Random.Range(0, matchingEnemies.Count)].id;
        }

        /// <summary>
        /// 적을 배치. 적 분류에 따라 전열/후열을 결정하고, 같은 열은 y좌표를 1부터 증가
        /// 전열 (x=1): 7, 8, 9
        /// 후열 (x=2): 10, 11, 12, 13, 14
        /// </summary>
        private void PlaceEnemies(List<int> enemyIds)
        {
            // 전열/후열별로 적을 그룹화
            Dictionary<int, List<int>> columnGroups = new Dictionary<int, List<int>>
            {
                { 1, new List<int>() },
                { 2, new List<int>() }
            };
            
            // 적을 열별로 분류
            foreach (int enemyId in enemyIds)
            {
                EnemyData enemy = _enemyDataList.enemies.Find(e => e.id == enemyId);
                if (enemy == null) continue;
                
                int column = GetColumnForArchetype(enemy.archetype);
                columnGroups[column].Add(enemyId);
            }
            
            // 각 열에 적 배치 (y는 1부터 순차적으로)
            foreach (var column in columnGroups)
            {
                int xPos = column.Key;
                int yPos = 1;  // y는 1부터 시작
                
                foreach (int enemyId in column.Value)
                {
                    if (yPos > 4) break;  // 각 열 최대 4칸
                    
                    // (xPos, yPos)에 적 배치
                    GameManager.Instance.gridManager.SpawnUnit(xPos, yPos, true, enemyId);
                    Debug.Log($"Enemy {enemyId} spawned at Cell ({xPos}, {yPos})");
                    
                    yPos++;  // 같은 열 내에서 y 증가
                }
            }
        }

        /// <summary>
        /// 적 분류에 따라 Column(xPos) 반환
        /// </summary>
        private int GetColumnForArchetype(int archetypeId)
        {
            if (archetypeId == 7 || archetypeId == 8 || archetypeId == 9) return 1;
            
            return 2;
        }

        /// <summary>
        /// 라운드 업데이트 - 적이 모두 처치되었는지 확인
        /// </summary>
        public void UpdateRound()
        {
            if (AreAllEnemiesDefeated())
            {
                Debug.Log("All enemies defeated. Ending round.");
                EndRound();
            }
        }

        private bool AreAllEnemiesDefeated()
        {
            // 적 측 필드 셀들이 모두 비어있는지 확인
            return GameManager.Instance.gridManager.AreAllEnemySideCellsEmpty();
        }

        private void EndRound()
        {
            IsRoundInProgress = false;
            
            // 적 전멸로 인한 라운드 종료를 GameManager에 알림
            GameManager.Instance.EndRoundByEnemyDefeat();
        }
    }
}
