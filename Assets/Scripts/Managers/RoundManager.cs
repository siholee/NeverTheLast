using System.Collections.Generic;
using System.Linq;
using Entities;
using StatusEffects.Base;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 라운드 데이터 로드 + 적 소환 큐 관리.<br/>
    /// 전열 큐(frontlineQueue)와 후열 큐(backlineQueue)를 별도로 유지.
    /// 빈 슬롯이 생기면 BattleManager.TurnCleanup에서 TrySpawnPending()이 호출됨.
    /// </summary>
    public class RoundManager
    {
        private readonly DataManager _dataManager;
        public int  Round            { get; private set; }
        public bool IsRoundInProgress { get; private set; }

        private Queue<int> _frontlineQueue = new();
        private Queue<int> _backlineQueue  = new();
        private RoundData  _currentRound;

        public RoundManager(DataManager manager) { _dataManager = manager; }

        // ── 라운드 로드 ────────────────────────────────────────────────────────

        public void LoadRound(int roundNumber)
        {
            Round = roundNumber;

            var dataList = _dataManager.FetchRoundDataList();
            _currentRound = dataList?.rounds?.Find(r => r.roundNumber == roundNumber);

            if (_currentRound == null)
            {
                Debug.LogWarning($"[RoundManager] roundNumber {roundNumber} 데이터 없음 — 라운드 생략");
                return;
            }

            // 전열/후열 큐 초기화
            _frontlineQueue = new Queue<int>(_currentRound.frontlineEnemies ?? new List<int>());
            _backlineQueue  = new Queue<int>(_currentRound.backlineEnemies  ?? new List<int>());

            GameManager.Instance.CalculateSynergies();
            ApplySynergyEffects();

            IsRoundInProgress = true;
            Debug.Log($"[RoundManager] 라운드 {roundNumber} 로드 — 전열 {_frontlineQueue.Count}명, 후열 {_backlineQueue.Count}명 대기");
        }

        private void ApplySynergyEffects()
        {
            foreach (var synergy in GameManager.Instance.SynergyCounts)
            {
                foreach (var unit in GridManager.Instance.heroList)
                {
                    var effect = SynergyEffectFactory.CreateSynergyEffect(synergy.Key);
                    effect.SetStack(Mathf.Min(synergy.Value.Count, synergy.Value.MaxCount));
                    unit.SetSynergyEffect(synergy.Key, effect);
                }
            }
        }

        // ── 적 소환 ────────────────────────────────────────────────────────────

        /// <summary>BattleManager TurnCleanup에서 호출. 빈 슬롯에 대기 적을 소환.</summary>
        public void TrySpawnPending()
        {
            TrySpawnFromQueue(isFrontline: true,  _frontlineQueue);
            TrySpawnFromQueue(isFrontline: false, _backlineQueue);
        }

        private void TrySpawnFromQueue(bool isFrontline, Queue<int> queue)
        {
            while (queue.Count > 0)
            {
                int slot = GridManager.Instance.FindAvailableEnemySlot(isFrontline);
                if (slot < 0) break; // 빈 슬롯 없음

                int unitId = queue.Dequeue();
                GridManager.Instance.SpawnEnemy(unitId, isFrontline, slot);
            }
        }

        // ── 큐 상태 확인 ───────────────────────────────────────────────────────

        /// <summary>전열/후열 모든 큐가 비었는지 (BattleManager의 라운드 완료 조건).</summary>
        public bool AreAllQueuesEmpty()
            => _frontlineQueue.Count == 0 && _backlineQueue.Count == 0;

        private void EndRound()
        {
            IsRoundInProgress = false;
            Debug.Log($"[RoundManager] 라운드 {Round} 종료");
        }

        // ── 레거시 호환 ───────────────────────────────────────────────────────

        /// <summary>사용하지 않음 (CharacterSelectionManager가 아군 소환 담당). 호환용으로 유지.</summary>
        public void SpawnHeroesForTest() { }

        public void NotifyCellAvailable(int cellIndex) { TrySpawnPending(); }
    }
}
