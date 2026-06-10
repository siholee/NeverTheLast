using System.Collections.Generic;
using System.Linq;
using Entities;
using StatusEffects.Base;
using UnityEngine;

namespace Managers
{
    public class RoundManager
    {
        private readonly DataManager _dataManager;
        public int Stage { get; private set; }  // 현재 전투 스테이지
        public int Round { get; private set; }  // 10스테이지마다 증가하는 라운드
        public int EnemyLevel => Mathf.Max(0, Stage - 1);
        public bool IsRoundInProgress { get; private set; }

        private StageThemeData _currentStageTheme;
        private EnemyDataList _enemyDataList;
        private RoundTypeDataList _roundTypeDataList;

        public RoundManager(DataManager manager)
        {
            _dataManager = manager;
            Stage = 1;
            Round = 1;
        }

        public void SpawnHeroesForTest()
        {
            // GameManager.Instance.gridManager.SpawnUnit(-1, 2, false, 1);
        }

        /// <summary>
        /// 스테이지 시작 시 호출. 스테이지 테마를 랜덤으로 선택
        /// </summary>
        public void InitializeStage(int stageNumber)
        {
            Stage = Mathf.Max(1, stageNumber);
            Round = GetRewardRound(Stage);
            
            // 스테이지 테마 데이터 로드
            StageThemeDataList themeDataList = _dataManager.FetchStageThemeDataList();
            if (themeDataList == null || themeDataList.stageThemes.Count == 0)
            {
                Debug.LogError("Failed to load stage theme data.");
                return;
            }
            
            // 랜덤으로 스테이지 테마 선택 (현재는 노르드만 있음)
            _currentStageTheme = themeDataList.stageThemes[Random.Range(0, themeDataList.stageThemes.Count)];
            Debug.Log($"Stage {Stage} theme: {_currentStageTheme.name}");
            
            // 적 데이터와 라운드 타입 데이터 로드
            _enemyDataList = _dataManager.FetchEnemyDataList();
            _roundTypeDataList = _dataManager.FetchRoundTypeDataList();
        }

        /// <summary>
        /// 라운드 시작. 적을 즉시 소환
        /// </summary>
        public void LoadRound(int roundNumber)
        {
            Stage = Mathf.Max(1, roundNumber);
            Round = GetRewardRound(Stage);
            
            if (_enemyDataList == null || _roundTypeDataList == null)
            {
                Debug.LogError("Enemy data or round type data not loaded.");
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
            
            // 적 소환
            SpawnEnemiesForRound(roundType);
            
            // 시너지 계산 및 적용
            GameManager.Instance.CalculateSynergies();
            ApplySynergyEffects();

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

        public void StopRound()
        {
            IsRoundInProgress = false;
        }

        /// <summary>
        /// 라운드 타입에 따라 적을 소환
        /// </summary>
        private void SpawnEnemiesForRound(RoundTypeData roundType)
        {
            List<int> enemyIdsToSpawn = new List<int>();
            
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
                
                // 보스 호위 추가 (패턴의 classes)
                if (selectedPattern.classes != null)
                {
                    foreach (int classId in selectedPattern.classes)
                    {
                        int enemyId = GetRandomEnemyByFactionAndClass(_currentStageTheme.faction, classId);
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
                
                // 일반 적 추가 (패턴의 classes)
                if (selectedPattern.classes != null)
                {
                    foreach (int classId in selectedPattern.classes)
                    {
                        int enemyId = GetRandomEnemyByFactionAndClass(_currentStageTheme.faction, classId);
                        if (enemyId != -1) enemyIdsToSpawn.Add(enemyId);
                    }
                }
            }
            // 일반 라운드
            else
            {
                if (selectedPattern.classes != null)
                {
                    foreach (int classId in selectedPattern.classes)
                    {
                        int enemyId = GetRandomEnemyByFactionAndClass(_currentStageTheme.faction, classId);
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
        /// 진영과 직업으로 랜덤 적 ID를 가져옴
        /// </summary>
        private int GetRandomEnemyByFactionAndClass(int faction, int classId)
        {
            List<EnemyData> matchingEnemies = _enemyDataList.enemies
                .Where(e => e.faction == faction && e.@class == classId && e.tier == "normal")
                .ToList();
            
            if (matchingEnemies.Count == 0)
            {
                Debug.LogWarning($"No enemy found for faction {faction} and class {classId}");
                return -1;
            }
            
            return matchingEnemies[Random.Range(0, matchingEnemies.Count)].id;
        }

        /// <summary>
        /// 적을 배치. 직업에 따라 전열/후열을 결정하고, 같은 열은 y좌표를 1부터 증가
        /// 전열 (x=1): 처형자(9), 투사(8), 파수꾼(7)
        /// 후열 (x=2): 사수(10), 마법사(11), 책략가(12), 메카닉(13), 지원가(14)
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
                
                int column = GetColumnForClass(enemy.@class);
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
        /// 직업에 따라 Column(xPos) 반환
        /// </summary>
        private int GetColumnForClass(int classId)
        {
            // 전열 (x=1): 처형자(9), 투사(8), 파수꾼(7)
            if (classId == 7 || classId == 8 || classId == 9) return 1;
            
            // 후열 (x=2): 사수(10), 마법사(11), 책략가(12), 메카닉(13), 지원가(14)
            return 2;
        }

        private void ApplySynergyEffects()
        {
            foreach (var synergy in GameManager.Instance.SynergyCounts)
            {
                foreach (var unit in GridManager.Instance.heroList)
                {
                    var synergyEffect = SynergyEffectFactory.CreateSynergyEffect(synergy.Key);
                    synergyEffect.SetStack(Mathf.Min(synergy.Value.Count, synergy.Value.MaxCount));
                    unit.SetSynergyEffect(synergy.Key, synergyEffect);
                }
            }
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

        // 기존 메서드들 (호환성 유지)
        public void NotifyCellAvailable(int cellIndex)
        {
            // 새 시스템에서는 대기열이 없으므로 빈 메서드
        }

        public int GetTotalQueuedEnemies()
        {
            // 새 시스템에서는 대기열이 없으므로 0 반환
            return 0;
        }
    }
}
