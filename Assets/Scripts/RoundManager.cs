using System.Collections.Generic;
using UnityEngine;

public class RoundManager
{
    public int ROUND { get; private set; }
    public bool IsRoundInProgress { get; private set; }

    private Dictionary<int, Queue<int>> spawnQueues; // 적 대기열
    private RoundData currentRound;

    public RoundManager()
    {
        spawnQueues = new Dictionary<int, Queue<int>>();
    }

    public void LoadRound(int roundNumber)
    {
        ROUND = roundNumber;

        TextAsset jsonFile = Resources.Load<TextAsset>("Data/07_Round");
        if (jsonFile == null)
        {
            Debug.LogError("Data/07_Round.json not found.");
            return;
        }

        RoundDataWrapper roundDataWrapper = JsonUtility.FromJson<RoundDataWrapper>(jsonFile.text);
        currentRound = roundDataWrapper.rounds.Find(r => r.roundNumber == ROUND);
        if (currentRound == null)
        {
            Debug.LogError($"No data found for round {ROUND}.");
            return;
        }

        foreach (var cell in currentRound.cells)
        {
            Queue<int> queue = new Queue<int>(cell.enemyIds);
            spawnQueues[cell.cellIndex] = queue;
            Debug.Log($"Cell {cell.cellIndex}: Initialized with {queue.Count} enemies in queue.");
        }

        Debug.Log($"Round {ROUND} data loaded successfully.");
        IsRoundInProgress = true;
    }

    public void UpdateRound()
    {
        Debug.Log("Updating round...");
        foreach (var cellIndex in spawnQueues.Keys)
        {
            Debug.Log($"Checking cellIndex {cellIndex}...");
            
            // 적을 소환하려 시도하고 실패하면 루프를 종료
            while (spawnQueues[cellIndex].Count > 0)
            {
                if (!TrySpawnEnemy(cellIndex))
                {
                    Debug.Log($"No space available for cellIndex {cellIndex}. Stopping spawn attempts.");
                    break; // 소환 실패 시 루프 종료
                }
            }

            if (spawnQueues[cellIndex].Count > 0)
            {
                Debug.Log($"Cell {cellIndex}: {spawnQueues[cellIndex].Count} enemies remain in queue.");
            }
        }

        if (AreAllQueuesEmpty())
        {
            Debug.Log("All queues are empty. Ending round.");
            EndRound();
        }
        else
        {
            Debug.Log("Enemies still remain in queues. Round continues.");
        }
    }
    
    private bool TrySpawnEnemy(int cellIndex)
    {
        Debug.Log($"Trying to spawn enemy at cellIndex {cellIndex}...");

        // xPos에 해당하는 yPos를 모두 확인 (1, 2, 3)
        for (int y = 1; y <= 3; y++)
        {
            // GridManager의 IsCellAvailable에 xPos와 yPos를 전달
            if (GridManager.Instance.IsCellAvailable(cellIndex, y))
            {
                // 대기열에서 적 ID를 가져옴
                int enemyId = spawnQueues[cellIndex].Dequeue();

                // GridManager의 SpawnEnemy에 xPos, yPos, enemyId 전달
                GridManager.Instance.SpawnEnemy(cellIndex, y, enemyId);

                Debug.Log($"Enemy {enemyId} spawned at Cell ({cellIndex}, {y}).");
                return true; // 적 소환 성공
            }
        }

        Debug.Log($"No available space to spawn enemy at cellIndex {cellIndex}.");
        return false; // 적 소환 실패
    }

    private bool AreAllQueuesEmpty()
    {
        foreach (var queue in spawnQueues.Values)
        {
            if (queue.Count > 0) return false;
        }
        return true;
    }

    private void EndRound()
    {
        Debug.Log($"Round {ROUND} completed.");
        IsRoundInProgress = false;
    }

    public void NotifyCellAvailable(int cellIndex)
    {
        Debug.Log($"Cell {cellIndex} is now available. Checking queue...");
        if (spawnQueues.ContainsKey(cellIndex) && spawnQueues[cellIndex].Count > 0)
        {
            TrySpawnEnemy(cellIndex);
        }
    }
}
