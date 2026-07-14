using System.Collections.Generic;
using System.Linq;
using Entities;
using UnityEngine;

namespace Managers
{
    public class RoundManager
    {
        private readonly DataManager _dataManager;
        public int Stage { get; private set; }  // 현재 전투 스테이지
        public int Round { get; private set; }  // 10스테이지마다 증가하는 라운드
        public int EnemyLevel => Mathf.Max(0, Stage - 1);
        public int StageInRound => ((Stage - 1) % 10) + 1;
        public int CurrentThemeId => _currentStageTheme?.id ?? 0;
        public string CurrentThemeName => _currentStageTheme?.name ?? "";
        public bool IsCurrentBossStage => IsBossStage(Stage);
        public bool IsCurrentEventStage { get; private set; }
        public bool IsRoundInProgress { get; private set; }
        public StageEventData CurrentEventData => _stageThemeDataList?.events?
            .FirstOrDefault(data => data.themeId == CurrentThemeId && data.stageInRound == StageInRound);

        private StageThemeDataList _stageThemeDataList;
        private StageThemeData _currentStageTheme;
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
            IsCurrentEventStage = false;
            
            EnsureDataLoaded();
            EnsureThemeForCurrentRound();

            if (_enemyDataList == null || _roundTypeDataList == null || _currentStageTheme == null)
            {
                Debug.LogError("Enemy data, round type data, or stage theme data not loaded.");
                return;
            }

            if (IsEventStage())
            {
                IsCurrentEventStage = true;
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

            int themeIndex = (Round - 1) % _stageThemeDataList.stageThemes.Count;
            StageThemeData selectedTheme = _stageThemeDataList.stageThemes[themeIndex];
            if (_currentStageTheme?.id == selectedTheme.id) return;

            _currentStageTheme = selectedTheme;
            Debug.Log($"Round {Round} theme: {_currentStageTheme.name}");
        }

        private bool IsEventStage()
        {
            if (CurrentEventData != null) return true;
            return StageInRound == 5 && !IsFixedBossStage(Stage);
        }

        private bool IsFixedBossStage(int stage)
        {
            return GetFixedBossId(stage) > 0;
        }

        private bool CurrentRoundHasFixedBoss()
        {
            if (_stageThemeDataList?.fixedBossStages == null) return false;

            return _stageThemeDataList.fixedBossStages.Any(data => GetRewardRound(data.stage) == Round);
        }

        private bool IsBossStage(int stage)
        {
            if (GetFixedBossId(stage) > 0) return true;

            int stageInRound = ((stage - 1) % 10) + 1;
            return (stageInRound == 10 && !CurrentRoundHasFixedBoss()) ||
                   (stageInRound == 9 && CurrentRoundHasFixedBoss());
        }

        private int GetFixedBossId(int stage)
        {
            FixedBossStageData fixedBoss = _stageThemeDataList?.fixedBossStages?
                .FirstOrDefault(data => data.stage == stage);
            return fixedBoss?.bossId ?? 0;
        }

        public void StopRound()
        {
            IsRoundInProgress = false;
        }

        public bool StartEventBattle(int enemyId)
        {
            if (enemyId <= 0) return false;
            EnsureDataLoaded();
            IsCurrentEventStage = false;
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

            if (StageInRound == 8 && _currentStageTheme.midBossId > 0)
            {
                enemyIdsToSpawn.Add(_currentStageTheme.midBossId);
                PlaceEnemies(enemyIdsToSpawn);
                return;
            }

            bool isThemeBossStage = (StageInRound == 10 && !CurrentRoundHasFixedBoss()) ||
                                    (StageInRound == 9 && CurrentRoundHasFixedBoss());
            if (isThemeBossStage && _currentStageTheme.bossId > 0)
            {
                enemyIdsToSpawn.Add(_currentStageTheme.bossId);
                PlaceEnemies(enemyIdsToSpawn);
                return;
            }

            ThemeStagePatternData themeStagePattern = _currentStageTheme.stagePatterns?
                .FirstOrDefault(data => data.stageInRound == StageInRound);
            if (themeStagePattern?.patterns != null && themeStagePattern.patterns.Count > 0)
            {
                RoundPattern themePattern = SelectRandomPattern(themeStagePattern.patterns);
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
