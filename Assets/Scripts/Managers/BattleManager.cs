using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Entities;
using StatusEffects.Effects;
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

        // 타겟 선택 (단일 타겟 동률 시 플레이어 선택)
        private bool _targetSelectionReceived;
        private Unit _playerSelectedTarget;

        // Phase 6: 턴 행동 예산
        private int _actionsLeft;
        private int _bonusActionsLeft;
        public const int DefaultMainActions  = 1;
        public const int DefaultBonusActions = 1;

        // 현재 행동 중인 유닛
        public Unit CurrentActor { get; private set; }

        // Phase 5: 원소 시스템
        public ElementSystem  ElementSystem  { get; private set; }
        public ReactionHandler ReactionHandler { get; private set; }

        // BattleUI 캐시 (같은 GO에 부착됨 — 전투 로그 및 패널 갱신용)
        private Managers.UI.BattleUI _battleUI;
        private Managers.UI.BattleUI BattleUI => _battleUI ??= GetComponent<Managers.UI.BattleUI>();

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
            // Phase 5: 원소 시스템 초기화
            ElementSystem   = new ElementSystem();
            ReactionHandler = new ReactionHandler(this, ElementSystem);

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

        /// <summary>Basic 공격 선택 (SP +1 생성, 코스트 없음)</summary>
        public void OnPlayerSelectBasic()
        {
            _selectedAction = ActionType.Basic;
            _playerInputReceived = true;
        }

        /// <summary>클래스 스킬 선택 (명상 등 — 클래스 고유 행동)</summary>
        public void OnPlayerSelectClass()
        {
            _selectedAction      = ActionType.ClassSkill;
            _playerInputReceived = true;
        }

        /// <summary>Normal(Skill) 공격 선택 (SP 소모)</summary>
        public void OnPlayerSelectNormal()
        {
            // SP 체크: 부족하면 무시
            if (CurrentActor != null && CurrentActor.NormalCode != null)
            {
                int cost = CurrentActor.NormalCode.SpCost;
                if (!GameManager.Instance.spManager.CanAfford(cost))
                {
                    Debug.Log($"[SP] SP 부족 — 필요 {cost}, 현재 {GameManager.Instance.spManager.Current}");
                    return;
                }
            }
            _selectedAction = ActionType.Normal;
            _playerInputReceived = true;
        }

        public void OnPlayerSelectUltimate()
        {
            _selectedAction = ActionType.Ultimate;
            _playerInputReceived = true;
        }

        /// <summary>단일 타겟 동률 시 플레이어가 타겟 클릭 → 호출됨.</summary>
        public void OnPlayerSelectTarget(Unit target)
        {
            _playerSelectedTarget   = target;
            _targetSelectionReceived = true;
        }

        /// <summary>
        /// 행동순서 목록 반환 (UI 표시용)
        /// </summary>
        public List<ActionEntry> GetActionQueue()
        {
            return new List<ActionEntry>(_actionQueue);
        }

        // === 타겟 사전 결정 ===

        /// <summary>ActionType → 해당 Code 객체 반환.</summary>
        private Codes.Base.Code GetCodeForAction(Unit unit, ActionType action)
        {
            return action switch
            {
                ActionType.Basic       => (Codes.Base.Code)unit.BasicCode ?? unit.NormalCode,
                ActionType.ClassSkill  => unit.ClassCode,
                ActionType.Normal      => unit.NormalCode,
                ActionType.Ultimate    => unit.UltimateCode,
                _                      => null
            };
        }

        /// <summary>
        /// 스킬 시전 전 TargetUnits를 결정하는 코루틴.<br/>
        /// Single 타겟에 동률이 있고 플레이어 턴이면 타겟 선택 UI를 대기.
        /// </summary>
        private IEnumerator ResolveTargetsCoroutine(Unit actor, Code code, bool isPlayerTurn)
        {
            switch (code.TargetType)
            {
                case BaseEnums.TargetType.Self:
                    code.TargetUnits = GridManager.Instance.ResolveSelf(actor);
                    break;

                case BaseEnums.TargetType.Single:
                    var (autoTarget, tied) = GridManager.Instance.ResolveSingleTarget(actor);
                    if (tied.Count == 0)
                    {
                        // 동률 없음 → 자동 선택
                        code.TargetUnits = autoTarget;
                    }
                    else if (isPlayerTurn && !actor.IsEnemy)
                    {
                        // 플레이어 턴 + 동률 → UI로 선택 요청
                        _targetSelectionReceived = false;
                        _playerSelectedTarget    = null;
                        GameManager.Instance.uiManager?.ShowTargetSelection(tied);
                        yield return new WaitUntil(() => _targetSelectionReceived);
                        GameManager.Instance.uiManager?.HideTargetSelection();
                        code.TargetUnits = _playerSelectedTarget != null
                            ? new List<Unit> { _playerSelectedTarget }
                            : new List<Unit> { tied[0] }; // fallback
                    }
                    else
                    {
                        // AI 또는 아군 단일 → 무작위
                        code.TargetUnits = new List<Unit> { tied[Random.Range(0, tied.Count)] };
                    }
                    break;

                case BaseEnums.TargetType.Range:
                    code.TargetUnits = GridManager.Instance.ResolveRangeTarget(actor, code.RangeTargetsEnemies);
                    break;

                case BaseEnums.TargetType.AoE:
                    code.TargetUnits = GridManager.Instance.ResolveAoETarget();
                    break;

                case BaseEnums.TargetType.Bounce:
                    // 바운스: 스킬 내부에서 히트마다 자동 랜덤 선택. 사전 결정 불필요.
                    code.TargetUnits = new System.Collections.Generic.List<Unit>();
                    break;
            }

            if (code.TargetUnits == null || code.TargetUnits.Count == 0)
                Debug.LogWarning($"[BattleManager] {actor.UnitName} → {code.CodeName}: 유효한 타겟 없음");

            yield break;
        }

        // === 핵심 전투 루프 ===

        private IEnumerator RunBattle()
        {
            currentPhase = BattlePhase.Initializing;
            Debug.Log("[BattleManager] 전투 시작");

            // 초기 스폰
            GameManager.Instance.roundManager.TrySpawnPending();
            yield return null; // 스폰 완료 대기

            // BattleUI 캐릭터 패널 갱신 (BattleManager와 같은 GO에 부착됨)
            GetComponent<Managers.UI.BattleUI>()?.RefreshUnits();

            while (true)
            {
                // 라운드 종료 체크
                if (IsRoundComplete())
                {
                    currentPhase = BattlePhase.RoundComplete;
                    Debug.Log("[BattleManager] 라운드 완료");
                    GameManager.Instance.NextGameState(false); // RoundInProgress → RoundEnd
                    GameManager.Instance.NextGameState(false); // RoundEnd → RewardSelection
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

                // 전투 로그: 유닛 턴 시작
                BattleUI?.AddLog($"── {CurrentActor.UnitName}의 차례 ──");

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
                    // Phase 6: 턴 행동 예산 초기화
                    _actionsLeft = DefaultMainActions;
                    _bonusActionsLeft = DefaultBonusActions;

                    // 히어로: 주요 행동 또는 보너스 행동이 남아 있는 동안 반복
                    while (_actionsLeft > 0 || _bonusActionsLeft > 0)
                    {
                        currentPhase = BattlePhase.WaitingForPlayerInput;
                        _playerInputReceived = false;
                        _selectedAction = ActionType.None;

                        // UI에 행동 선택 패널 표시 (남은 액션 수 전달)
                        GameManager.Instance.uiManager.ShowActionPanel(CurrentActor, _actionsLeft, _bonusActionsLeft);

                        Debug.Log($"[BattleManager] {CurrentActor.UnitName} 턴 - 플레이어 입력 대기 (주요: {_actionsLeft}, 보너스: {_bonusActionsLeft})");
                        yield return new WaitUntil(() => _playerInputReceived);

                        // UI 패널 숨기기
                        GameManager.Instance.uiManager.HideActionPanel();

                        // 타겟 사전 결정: TargetUnits를 코드에 미리 설정
                        currentPhase = BattlePhase.ExecutingTurn;
                        var resolveCode = GetCodeForAction(CurrentActor, _selectedAction);
                        if (resolveCode != null)
                            yield return ResolveTargetsCoroutine(CurrentActor, resolveCode, isPlayerTurn: true);

                        // 선택된 행동 실행; true = 행동 소모
                        bool consumed = ExecuteAction(CurrentActor, _selectedAction);
                        if (consumed)
                        {
                            if (_actionsLeft > 0) _actionsLeft--;
                            else if (_bonusActionsLeft > 0) _bonusActionsLeft--;
                        }

                        // 시전 애니메이션 완료 대기 (동시 시전 방지)
                        yield return new WaitUntil(() => !CurrentActor.isCasting || !CurrentActor.isActive);

                        // 유닛이 사망하면 루프 탈출
                        if (!CurrentActor.isActive) break;
                    }
                }
                else
                {
                    // 적: AI 자동 행동
                    currentPhase = BattlePhase.ExecutingTurn;
                    ActionType aiAction = DecideEnemyAction(CurrentActor);
                    var aiCode = GetCodeForAction(CurrentActor, aiAction);
                    if (aiCode != null)
                        yield return ResolveTargetsCoroutine(CurrentActor, aiCode, isPlayerTurn: false);
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

                // Phase 5: ITemporalEffect 틱 (DoT 및 디버프 지속시간 처리)
                TickTemporalEffects();

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

        /// <summary>
        /// 유닛의 선택된 행동을 실행한다.
        /// Phase 6: 주요 행동을 소모하면 true, 소모하지 않으면 false 반환.
        /// </summary>
        private bool ExecuteAction(Unit unit, ActionType action)
        {
            var sp = GameManager.Instance.spManager;
            switch (action)
            {
                case ActionType.Basic:
                    // Basic 공격: SP +1 생성, BasicCode(마력탄 등) 실행
                    unit.CastBasicCode();
                    sp.Generate(SPManager.BasicAttackGain);
                    BattleUI?.AddLog($"{unit.UnitName}: {unit.BasicCode?.CodeName ?? "기본 공격"} · SP+1");
                    return true;

                case ActionType.ClassSkill:
                    // 클래스 스킬: 내부에서 SP/마나 처리 (명상: SP+2)
                    unit.CastClassCode();
                    BattleUI?.AddLog($"{unit.UnitName}: {unit.ClassCode?.CodeName ?? "클래스 스킬"}");
                    return true;

                case ActionType.Normal:
                    // Skill 공격: SP 소모 후 실행
                    if (unit.NormalCode != null)
                    {
                        int cost = unit.NormalCode.SpCost;
                        if (!sp.CanAfford(cost))
                        {
                            Debug.Log($"[SP] {unit.UnitName} SP 부족 ({sp.Current}/{cost}) — Normal 스킬 취소");
                            return false; // SP 부족 → 행동 미소모, 다시 선택
                        }
                        sp.Spend(cost);
                        BattleUI?.AddLog($"{unit.UnitName}: {unit.NormalCode.CodeName} · SP-{cost}");
                    }
                    unit.CastNormalCode();
                    return true;

                case ActionType.Ultimate:
                    unit.CastUltimateCode();
                    BattleUI?.AddLog($"{unit.UnitName}: {unit.UltimateCode?.CodeName ?? "궁극기"} ★");
                    return true;

                default:
                    return true;
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

        // Phase 5: 원소 반응 헬퍼

        /// <summary>
        /// Overloaded 반응: 공격자 ATK 기반 Pyro AoE 피해를 모든 적에게 적용.
        /// </summary>
        public void HandleOverloaded(Unit attacker, Unit primaryTarget)
        {
            int overloadDmg = (int)(attacker.AtkCurr * 0.5f);
            var enemies = GridManager.Instance.TargetAllEnemies(attacker);
            var ctx = new DamageContext(attacker, overloadDmg, CodeType.Normal,
                new List<int>(), ElementType.Pyro);
            foreach (var enemy in enemies)
            {
                if (enemy.isActive)
                {
                    Debug.Log($"[Overloaded] {enemy.UnitName}에게 {overloadDmg} Pyro AoE");
                    enemy.TakeDamage(ctx);
                }
            }
        }

        /// <summary>
        /// Phase 5: 모든 활성 유닛의 ITemporalEffect(DoT/디버프) 틱 처리.
        /// TurnCleanup 단계에서 매 턴 호출.
        /// </summary>
        private void TickTemporalEffects()
        {
            var allUnits = GridManager.Instance.heroList
                .Concat(GridManager.Instance.enemyList)
                .Where(u => u.isActive)
                .ToList();

            foreach (var unit in allUnits)
            {
                // 딕셔너리 스냅샷: 틱 중 반응으로 새 상태이상이 추가돼도 안전하게 순회
                var snapshot = unit.GetStatusEffects().ToList();
                var expiredKeys = new List<string>();

                foreach (var pair in snapshot)
                {
                    if (pair.Value is BurningDoT dot)
                    {
                        int dmg = dot.CalculateTick();
                        if (dmg > 0)
                        {
                            Debug.Log($"[DoT] {unit.UnitName}에게 {dmg} {dot.Element} 지속 피해");
                            // dot.Element 사용: Burning = Pyro, Electrocharged = Electro
                            // Physical로 전달해 DoT 틱이 연쇄 반응을 유발하지 않도록 함
                            var ctx = new DamageContext(dot.Source, dmg, CodeType.Normal,
                                new List<int>(), ElementType.Physical);
                            unit.TakeDamage(ctx);
                        }
                        dot.UpdateDuration(1f);
                        if (dot.IsExpired) expiredKeys.Add(pair.Key);
                    }
                    else if (pair.Value is SuperconductDebuff sc)
                    {
                        sc.UpdateDuration(1f);
                        if (sc.IsExpired) expiredKeys.Add(pair.Key);
                    }
                    else if (pair.Value is FrozenDebuff frozen)
                    {
                        frozen.UpdateDuration(1f);
                        if (frozen.IsExpired) expiredKeys.Add(pair.Key);
                    }
                }

                foreach (var key in expiredKeys)
                    unit.RemoveStatusEffect(key);
            }
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
