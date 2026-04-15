using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        // 행동순서 큐 엔트리
        public class ActionEntry
        {
            public Unit Unit;
            public float CurrentAV; // 현재 행동값 (낮을수록 빨리 행동)
            public float BaseAV;    // 기본 행동값 = 10000 / Speed
        }

        public enum BattlePhase
        {
            Idle,
            Initializing,
            AdvancingTimeline,
            WaitingForPlayerInput,
            ExecutingTurn,
            WaitingForAnimation,
            TurnCleanup,
            RoundComplete
        }

        [SerializeField] private BattlePhase currentPhase = BattlePhase.Idle;
        public BattlePhase CurrentPhase => currentPhase;

        private readonly List<ActionEntry> _actionQueue = new();
        private Coroutine _battleCoroutine;

        // 플레이어 입력
        private ActionType _selectedAction = ActionType.None;
        private bool _playerInputReceived;

        // 현재 행동 중인 유닛
        public Unit CurrentActor { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        /// <summary>
        /// 전투 시작. GameManager에서 RoundInProgress 진입 시 호출.
        /// </summary>
        public void StartBattle()
        {
            _actionQueue.Clear();

            // 모든 활성 유닛을 큐에 등록
            foreach (var hero in GridManager.Instance.heroList)
            {
                if (hero.isActive) RegisterUnit(hero);
            }
            foreach (var enemy in GridManager.Instance.enemyList)
            {
                if (enemy.isActive) RegisterUnit(enemy);
            }

            if (_battleCoroutine != null)
            {
                StopCoroutine(_battleCoroutine);
            }
            _battleCoroutine = StartCoroutine(RunBattle());
        }

        /// <summary>
        /// 전투 중 유닛 등록 (스폰 시)
        /// </summary>
        public void RegisterUnit(Unit unit)
        {
            // 이미 등록된 유닛은 무시
            if (_actionQueue.Any(e => e.Unit == unit)) return;

            float baseAV = 10000f / Mathf.Max(1f, unit.SpeedCurr);
            _actionQueue.Add(new ActionEntry
            {
                Unit = unit,
                CurrentAV = baseAV,
                BaseAV = baseAV
            });
            SortQueue();
        }

        /// <summary>
        /// 유닛 제거 (사망 시)
        /// </summary>
        public void UnregisterUnit(Unit unit)
        {
            _actionQueue.RemoveAll(e => e.Unit == unit);
        }

        /// <summary>
        /// 행동 가속: AV를 BaseAV의 percent만큼 감소
        /// </summary>
        public void ActionAdvance(Unit unit, float percent)
        {
            var entry = FindEntry(unit);
            if (entry == null) return;
            float reduction = entry.BaseAV * percent;
            entry.CurrentAV = Mathf.Max(0f, entry.CurrentAV - reduction);
            SortQueue();
        }

        /// <summary>
        /// 행동 지연: AV를 BaseAV의 percent만큼 증가
        /// </summary>
        public void ActionDelay(Unit unit, float percent)
        {
            var entry = FindEntry(unit);
            if (entry == null) return;
            float increase = entry.BaseAV * percent;
            entry.CurrentAV += increase;
            SortQueue();
        }

        /// <summary>
        /// 속도 변경 시 AV 재계산 (붕스타 공식)
        /// </summary>
        public void OnSpeedChanged(Unit unit, float oldSpeed, float newSpeed)
        {
            var entry = FindEntry(unit);
            if (entry == null) return;
            if (oldSpeed <= 0f) return;

            entry.CurrentAV = entry.CurrentAV * (oldSpeed / newSpeed);
            entry.BaseAV = 10000f / Mathf.Max(1f, newSpeed);
            SortQueue();
        }

        // 플레이어 입력 메소드 (UI 버튼에서 호출)
        public void OnPlayerSelectNormal()
        {
            _selectedAction = ActionType.Normal;
            _playerInputReceived = true;
        }

        public void OnPlayerSelectUltimate()
        {
            _selectedAction = ActionType.Ultimate;
            _playerInputReceived = true;
        }

        /// <summary>
        /// 행동순서 목록 반환 (UI 표시용)
        /// </summary>
        public List<ActionEntry> GetActionQueue()
        {
            return new List<ActionEntry>(_actionQueue);
        }

        // === 핵심 전투 루프 ===

        private IEnumerator RunBattle()
        {
            currentPhase = BattlePhase.Initializing;
            Debug.Log("[BattleManager] 전투 시작");

            // 초기 스폰
            GameManager.Instance.roundManager.TrySpawnPending();
            yield return null; // 스폰 완료 대기

            while (true)
            {
                // 라운드 종료 체크
                if (IsRoundComplete())
                {
                    currentPhase = BattlePhase.RoundComplete;
                    Debug.Log("[BattleManager] 라운드 완료");
                    GameManager.Instance.NextGameState(false);
                    yield break;
                }

                // 게임 오버 체크 (히어로 전멸)
                if (AreAllHeroesDead())
                {
                    currentPhase = BattlePhase.RoundComplete;
                    Debug.Log("[BattleManager] 게임 오버 - 히어로 전멸");
                    GameManager.Instance.NextGameState(true);
                    yield break;
                }

                // 큐가 비었으면 대기
                if (_actionQueue.Count == 0)
                {
                    yield return null;
                    continue;
                }

                // === AdvancingTimeline ===
                currentPhase = BattlePhase.AdvancingTimeline;

                // 최소 AV를 찾아 모든 유닛에서 차감
                float minAV = _actionQueue[0].CurrentAV;
                if (minAV > 0f)
                {
                    foreach (var entry in _actionQueue)
                    {
                        entry.CurrentAV -= minAV;
                    }
                }

                // 다음 행동 유닛
                ActionEntry actor = _actionQueue[0];
                CurrentActor = actor.Unit;

                // 비활성 유닛 건너뛰기
                if (!CurrentActor.isActive)
                {
                    UnregisterUnit(CurrentActor);
                    continue;
                }

                // OnTurnStart 이벤트
                CurrentActor.Invoke(UnitEventType.OnTurnStart, CurrentActor);

                // === 제어 상태 처리 ===
                if (CurrentActor.isControlled)
                {
                    CurrentActor.controlTurns--;
                    if (CurrentActor.controlTurns <= 0)
                    {
                        CurrentActor.ControlEnds();
                    }
                    Debug.Log($"[BattleManager] {CurrentActor.UnitName} 행동불능 (남은 턴: {CurrentActor.controlTurns})");

                    // AV 리셋 후 다음 턴
                    actor.BaseAV = 10000f / Mathf.Max(1f, CurrentActor.SpeedCurr);
                    actor.CurrentAV = actor.BaseAV;
                    CurrentActor.Invoke(UnitEventType.OnTurnEnd, CurrentActor);
                    SortQueue();
                    yield return null;
                    continue;
                }

                // === 행동 결정 ===
                if (!CurrentActor.IsEnemy)
                {
                    // 히어로: 플레이어 입력 대기
                    currentPhase = BattlePhase.WaitingForPlayerInput;
                    _playerInputReceived = false;
                    _selectedAction = ActionType.None;

                    // UI에 행동 선택 패널 표시
                    GameManager.Instance.uiManager.ShowActionPanel(CurrentActor);

                    Debug.Log($"[BattleManager] {CurrentActor.UnitName} 턴 - 플레이어 입력 대기");
                    yield return new WaitUntil(() => _playerInputReceived);

                    // UI 패널 숨기기
                    GameManager.Instance.uiManager.HideActionPanel();

                    // 선택된 행동 실행
                    currentPhase = BattlePhase.ExecutingTurn;
                    ExecuteAction(CurrentActor, _selectedAction);
                }
                else
                {
                    // 적: AI 자동 행동
                    currentPhase = BattlePhase.ExecutingTurn;
                    ActionType aiAction = DecideEnemyAction(CurrentActor);
                    ExecuteAction(CurrentActor, aiAction);
                    Debug.Log($"[BattleManager] {CurrentActor.UnitName}(적) 턴 - {aiAction}");
                }

                // === 애니메이션 대기 ===
                currentPhase = BattlePhase.WaitingForAnimation;
                yield return new WaitUntil(() => !CurrentActor.isCasting || !CurrentActor.isActive);

                // 약간의 대기 (연출 여유)
                yield return new WaitForSeconds(0.2f);

                // === TurnCleanup ===
                currentPhase = BattlePhase.TurnCleanup;

                // AV 리셋
                actor.BaseAV = 10000f / Mathf.Max(1f, CurrentActor.SpeedCurr);
                actor.CurrentAV = actor.BaseAV;

                // 사망한 유닛 큐에서 제거
                _actionQueue.RemoveAll(e => !e.Unit.isActive);

                // 스폰 큐 처리
                GameManager.Instance.roundManager.TrySpawnPending();

                // OnTurnEnd 이벤트
                if (CurrentActor.isActive)
                {
                    CurrentActor.Invoke(UnitEventType.OnTurnEnd, CurrentActor);
                }

                SortQueue();
                CurrentActor = null;

                yield return null;
            }
        }

        private void ExecuteAction(Unit unit, ActionType action)
        {
            switch (action)
            {
                case ActionType.Ultimate:
                    unit.CastUltimateCode();
                    break;
                case ActionType.Normal:
                    unit.CastNormalCode();
                    break;
                default:
                    // 행동 없음 (isCasting을 건드리지 않으므로 WaitUntil이 즉시 통과)
                    break;
            }
        }

        /// <summary>
        /// 적 AI 행동 결정: 궁극기 > 일반
        /// </summary>
        private ActionType DecideEnemyAction(Unit unit)
        {
            if (unit.ManaCurr >= unit.ManaMax && unit.UltimateCode != null && unit.UltimateCode.HasValidTarget())
            {
                return ActionType.Ultimate;
            }
            if (unit.NormalCode != null && unit.NormalCode.HasValidTarget())
            {
                return ActionType.Normal;
            }
            return ActionType.None;
        }

        private bool IsRoundComplete()
        {
            bool allEnemiesDead = GridManager.Instance.enemyList.All(e => !e.isActive);
            bool allQueuesEmpty = GameManager.Instance.roundManager.AreAllQueuesEmpty();
            return allEnemiesDead && allQueuesEmpty;
        }

        private bool AreAllHeroesDead()
        {
            return GridManager.Instance.heroList.All(h => !h.isActive);
        }

        private ActionEntry FindEntry(Unit unit)
        {
            return _actionQueue.FirstOrDefault(e => e.Unit == unit);
        }

        /// <summary>
        /// AV 오름차순 정렬. 동점: 히어로 우선, 그리드 좌→우 하→상, 유닛 ID
        /// </summary>
        private void SortQueue()
        {
            _actionQueue.Sort((a, b) =>
            {
                int avCompare = a.CurrentAV.CompareTo(b.CurrentAV);
                if (avCompare != 0) return avCompare;

                // 히어로 우선
                int heroCompare = a.Unit.IsEnemy.CompareTo(b.Unit.IsEnemy);
                if (heroCompare != 0) return heroCompare;

                // 그리드 위치: x 오름차순, y 오름차순
                int xCompare = a.Unit.currentCell.xPos.CompareTo(b.Unit.currentCell.xPos);
                if (xCompare != 0) return xCompare;

                int yCompare = a.Unit.currentCell.yPos.CompareTo(b.Unit.currentCell.yPos);
                if (yCompare != 0) return yCompare;

                return a.Unit.ID.CompareTo(b.Unit.ID);
            });
        }
    }
}
