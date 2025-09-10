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
        public int Round { get; private set; }
        public bool IsRoundInProgress { get; private set; }

        private readonly Dictionary<int, Queue<int>> _spawnQueues; // 적 대기열
        private RoundData _currentRound;

        public RoundManager(DataManager manager)
        {
            _dataManager = manager;
            _spawnQueues = new Dictionary<int, Queue<int>>();
        }

        public void SpawnHeroesForTest()
        {
            // GameManager.Instance.gridManager.SpawnUnit(-1, 2, false, 1);
        }

        public void LoadRound(int roundNumber)
        {
            // if (roundNumber == 1) SpawnHeroesForTest();
            Round = roundNumber;

            RoundDataList roundDataList = _dataManager.FetchRoundDataList();
            _currentRound = roundDataList.rounds.Find(r => r.roundNumber == Round);
            if (_currentRound == null)
            {
                // Debug.LogError($"No data found for round {ROUND}.");
                return;
            }

            foreach (var cell in _currentRound.cells)
            {
                Queue<int> queue = new Queue<int>(cell.enemyIds);
                _spawnQueues[cell.cellIndex] = queue;
                // Debug.Log($"Cell {cell.cellIndex}: Initialized with {queue.Count} enemies in queue.");
            }
            
            GameManager.Instance.CalculateSynergies();
            ApplySynergyEffects();

            // Debug.Log($"Round {ROUND} data loaded successfully.");
            IsRoundInProgress = true;
        }

        private void ApplySynergyEffects()
        {
            foreach (var synergy in GameManager.Instance.SynergyCounts)
            {
                // Debug.Log($"Synergy {synergy.Key}: {synergy.Value} units.");
                foreach (var unit in GridManager.Instance.heroList)
                {
                    var synergyEffect = SynergyEffectFactory.CreateSynergyEffect(synergy.Key);
                    synergyEffect.SetStack(Mathf.Min(synergy.Value.Count, synergy.Value.MaxCount));
                    unit.SetSynergyEffect(synergy.Key, synergyEffect);
                }
            }
        }

        public void UpdateRound()
        {
            // Debug.Log("Updating round...");
            foreach (var cellIndex in _spawnQueues.Keys)
            {
                // Debug.Log($"Checking cellIndex {cellIndex}...");

                // 적을 소환하려 시도하고 실패하면 루프를 종료
                while (_spawnQueues[cellIndex].Count > 0)
                {
                    if (!TrySpawnEnemy(cellIndex))
                    {
                        // Debug.Log($"No space available for cellIndex {cellIndex}. Stopping spawn attempts.");
                        break; // 소환 실패 시 루프 종료
                    }
                }

                if (_spawnQueues[cellIndex].Count > 0)
                {
                    // Debug.Log($"Cell {cellIndex}: {spawnQueues[cellIndex].Count} enemies remain in queue.");
                }
            }

            if (AreAllQueuesEmpty())
            {
                Debug.Log("All queues are empty. Ending round.");
                EndRound();
            }
            else
            {
                // Debug.Log("Enemies still remain in queues. Round continues.");
            }
        }

        private bool TrySpawnEnemy(int cellIndex)
        {
            // Debug.Log($"Trying to spawn enemy at cellIndex {cellIndex}...");

            // xPos에 해당하는 yPos를 모두 확인 (1, 2, 3)
            for (int y = 1; y <= 3; y++)
            {
                // GridManager의 IsCellAvailable에 xPos와 yPos를 전달
                if (GameManager.Instance.gridManager.IsCellAvailable(cellIndex, y))
                {
                    // 대기열에서 적 ID를 가져옴
                    int enemyId = _spawnQueues[cellIndex].Dequeue();

                    // GridManager의 SpawnEnemy에 xPos, yPos, enemyId 전달
                    GameManager.Instance.gridManager.SpawnUnit(cellIndex, y, true, enemyId);

                    // Debug.Log($"Enemy {enemyId} spawned at Cell ({cellIndex}, {y}).");
                    return true; // 적 소환 성공
                }
            }

            // Debug.Log($"No available space to spawn enemy at cellIndex {cellIndex}.");
            return false; // 적 소환 실패
        }

        private bool AreAllQueuesEmpty()
        {
            // 스폰 큐가 비어있는지 확인
            foreach (var queue in _spawnQueues.Values)
            {
                if (queue.Count > 0) return false;
            }
            
            // 적 측 필드 셀들이 모두 비어있는지 확인
            return GameManager.Instance.gridManager.AreAllEnemySideCellsEmpty();
        }

        private void EndRound()
        {
            IsRoundInProgress = false;
            // 현재 HeroList에 있는 모든 영웅들을 다시 Initialize
            foreach (Unit hero in GridManager.Instance.heroList.ToList())
            {
                if (hero != null && hero.isActive)
                {
                    hero.InitializeUnit(hero.IsEnemy, hero.ID);
                    Debug.Log($"영웅 {hero.UnitName}을(를) 다시 초기화했습니다.");
                }
            }
            
            // Debug.Log($"Round {ROUND} completed.");
            // 적 전멸로 인한 라운드 종료를 GameManager에 알림
            GameManager.Instance.EndRoundByEnemyDefeat();
            LoadRound((Round + 1) % 2 + 1);
            IsRoundInProgress = true;
        }

        public void NotifyCellAvailable(int cellIndex)
        {
            // Debug.Log($"Cell {cellIndex} is now available. Checking queue...");
            if (_spawnQueues.ContainsKey(cellIndex) && _spawnQueues[cellIndex].Count > 0)
            {
                TrySpawnEnemy(cellIndex);
            }
        }

        public int GetTotalQueuedEnemies()
        {
            int total = 0;
            foreach (var queue in _spawnQueues.Values)
            {
                total += queue.Count;
            }
            return total;
        }
    }
}
