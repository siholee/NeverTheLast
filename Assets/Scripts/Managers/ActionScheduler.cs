using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 전투의 모든 행동을 중앙에서 예약하고 <b>한 번에 하나씩</b> 실행하는 턴제 스케줄러.
    ///
    /// 시간 축은 초가 아니라 <b>행동 카운트</b>다. 벽시계는 연출(투사체 비행·시전 모션)에만 쓴다.
    ///
    /// 행동치(AV) — 붕괴: 스타레일 방식이되 이산적으로 굴린다.
    ///   속도 = 100 × ActionSpeedCurr   (ActionSpeedCurr = 1 + 최종 DEX × 0.01)
    ///   AV   = 10000 / 속도            (작을수록 먼저 행동)
    /// 시간을 흘려보내지 않고 <b>다음 행동자에게 필요한 만큼만 전원의 AV를 한 번에 깎는다.</b>
    /// 그래서 프레임 수·연출 길이가 순서에 영향을 주지 않는다.
    ///
    /// 우선순위 — 낮을수록 먼저다.
    ///   0 패시브 발동 → 1 추가행동 → 2 궁극기 → 3 일반행동
    /// 유닛의 턴이 열리면 상태·주기 효과가 먼저 진행되고(패시브가 여기서 줄을 선다),
    /// 그 다음 추가행동, 마지막으로 그 턴의 일반행동이 나간다.
    ///
    /// <b>궁극기는 턴을 쓰지 않는다.</b> 자원이 차는 즉시 누구의 턴이든 상관없이 줄을 서고,
    /// 발동해도 AV가 리셋되지 않아 원래 예정된 일반행동 차례는 그대로 남는다.
    /// 행동 횟수가 적은 저DEX·고INT 유닛도 궁극기로 화력을 낼 수 있게 하기 위한 구조다.
    ///
    /// 중복 방지 — 같은 (유닛 + 종류 + 이름) 조합은 큐에 하나만 존재한다.
    /// 추가행동이 여러 번 겹쳐 터지거나, 한 턴에 본 행동이 두 번 잡히지 않는다.
    /// </summary>
    public class ActionScheduler
    {
        /// <summary>AV 산출의 분자. 스타레일과 동일하게 10000을 쓴다.</summary>
        public const float BaseActionValue = 10000f;

        /// <summary>속도 100 = DEX 0 기준. DEX 1당 속도 1이 오른다.</summary>
        public const float SpeedScale = 100f;

        /// <summary>
        /// AV → 초 환산 상수. AV 40이 전투 시간 1초다.
        ///
        /// 실시간 모델이던 시절의 초당 AV 감소량을 그대로 환산 계수로 쓴다.
        /// 속도 100(DEX 0)이면 AV 100 → 2.5초마다 행동한다는 감각이 유지된다.
        /// <b>남은 용도는 궁극기 자원 충전 하나뿐이다.</b> 지속시간·주기·내부 쿨다운은
        /// 전부 <see cref="Unit.TurnCount"/> 축으로 옮겼다.
        /// </summary>
        public const float ActionValuePerSecond = 40f;

        /// <summary>
        /// 행동이 이 시간을 넘겨도 끝나지 않으면 강제로 종료 처리한다.
        /// 코드가 isCasting을 되돌리지 못해도 전투가 멈추지 않게 하는 안전장치다.
        /// 순서 판정이 아니라 연출 대기의 상한이므로 유일하게 초 단위로 남는다.
        /// </summary>
        private const float ActionWatchdogSeconds = 8f;

        /// <summary>
        /// <b>행동</b>의 종류. 값이 작을수록 먼저 실행된다.
        ///
        /// 여기 있는 것들은 전부 <b>행동을 하는 영역</b>이다 — 스케줄러 슬롯을 차지하고 실제로 해결된다.
        /// 패시브 코드처럼 <b>행동을 호출하는 영역</b>은 이 목록에 없다. 패시브는 스스로 행동하지 않고
        /// 여기에 행동을 하나 얹을 뿐이다.
        ///
        /// <see cref="PriorityAdditional"/>은 패시브가 부른 추가행동이라 먼저 나갈 뿐,
        /// <b>성질은 추가행동과 같다</b>. 둘 다 턴을 쓰지 않고 <c>OnAdditionalActivates</c>를 발행한다.
        /// </summary>
        public enum ActionKind
        {
            /// <summary>패시브가 부른 추가행동. 같은 턴의 다른 행동보다 먼저 나간다.</summary>
            PriorityAdditional = 0,

            /// <summary>
            /// 특수행동. 인드라의 궁극기만이 부르며, 열리는 즉시 나가야 하므로 추가행동보다 앞이다.
            /// 턴을 쓰지 않는 것은 추가행동과 같지만 <b>판정은 별개</b>다.
            /// </summary>
            Special = 1,

            /// <summary>추가행동. 턴을 쓰지 않는다.</summary>
            Additional = 2,

            /// <summary>
            /// 협동행동. 짝이 행동할 때 끼어들어 함께 친다. 턴을 쓰지 않는다.
            ///
            /// <b>추가행동과 판정을 나눈 이유는 트리거다.</b> <c>OnAdditionalActivates</c>를 듣는 코드가
            /// 이미 여럿인데, 협공이 그것들을 전부 켜면 듀오 하나 때문에 다른 코드의 균형이 흔들린다.
            /// 특수행동이 성질은 추가행동과 같으면서 판정을 따로 가진 것과 같은 이유다.
            ///
            /// 자기를 부른 일반행동 <b>바로 뒤에</b> 붙어야 하므로 추가행동보다 뒤, 궁극기보다 앞이다.
            /// </summary>
            Coordinated = 3,

            /// <summary>궁극기. 턴을 쓰지 않고 AV도 리셋하지 않는다.</summary>
            Ultimate = 4,

            /// <summary>일반행동 — 또는 그 자리를 대신 쓰는 <b>대체행동</b>. 턴을 쓴다.</summary>
            Normal = 5,
        }

        /// <summary>
        /// 행동 하나가 막 나가려 한다(유닛 · 종류 · 예약 이름). 전투 로그가 듣는다.
        /// 트리거가 아니다 — 여기서 게임 상태를 바꾸면 안 된다.
        /// </summary>
        public static event Action<Unit, ActionKind, string> AnyActionStarted;

        /// <summary>턴을 쓰지 않는 추가행동 계열인가. 우선 추가행동도 성질은 같다.</summary>
        public static bool IsAdditional(ActionKind kind)
            => kind is ActionKind.Additional or ActionKind.PriorityAdditional;

        /// <summary>큐에 올라간 행동 하나.</summary>
        private sealed class PendingAction
        {
            public Unit Unit;
            public ActionKind Kind;
            public string Key;
            public string Label;
            public Action Run;
            public int Sequence;   // 같은 우선순위 안에서는 먼저 예약된 것이 먼저다
        }

        /// <summary>행동 예약 상태. UI가 읽는다.</summary>
        public sealed class Reservation
        {
            public Unit Unit;
            public bool IsUltimate;

            /// <summary>
            /// 지금부터 몇 번째 행동인지. 0이면 바로 다음이다.
            ///
            /// 초로 보여 주지 않는 이유는, 행동에 밀리면 남은 초가 계속 어긋나기 때문이다.
            /// 순서는 밀려도 '몇 번째'는 어긋나지 않으므로 카운트로 읽는다.
            /// </summary>
            public int ActionsAhead;
        }

        private readonly Dictionary<Unit, float> _actionValues = new();

        /// <summary>
        /// 이번 라운드에 각 유닛이 연 턴 수. <b>추월 제한</b>에 쓴다.
        ///
        /// AV만으로 돌리면 속도 1.3배 차이에서도 빠른 쪽이 몇 턴마다 한 바퀴를 앞질러
        /// 느린 쪽이 한 번 행동하는 사이에 두 번 행동한다. 초반처럼 DEX 차이가 작은 구간에서
        /// "한 캐릭터가 턴을 잡으면 다른 캐릭터도 잡는다"가 깨졌다(기획 요청).
        /// 그래서 A는 B보다 <c>floor(A속도 / B속도)</c>번까지만 앞설 수 있다 —
        /// 2배 미만이면 번갈아 행동하고 DEX는 <b>순서</b>만 정한다. 2배 이상부터 두 번씩 행동한다.
        /// </summary>
        private readonly Dictionary<Unit, int> _turnCounts = new();

        /// <summary>
        /// 행동 게이지를 앞당기는 효과(<see cref="AdvanceAction"/>)가 준 추월 몫.
        /// 추월 제한에 막혀 앞당김이 사라지지 않도록, 앞당긴 비율만큼 한도를 넘어 설 수 있게 한다.
        /// </summary>
        private readonly Dictionary<Unit, float> _lapCredit = new();
        private readonly List<PendingAction> _queue = new();
        private readonly HashSet<string> _queuedKeys = new();

        private Unit _acting;
        private float _actingElapsed;
        private int _sequence;
        private int _currentActionId = -1;
        public ActionKind ActingKind { get; private set; }
        private Unit _executingUnit;
        /// <summary>코루틴뿐 아니라 즉발 행동의 콜백 안에서도 현재 실행자를 식별한다.</summary>
        public Unit ExecutingUnit => _currentActionId >= 0 ? _executingUnit : null;

        /// <summary>이번 라운드에 해결된 행동 수. 전역 행동 카운트다.</summary>
        public int ActionCount { get; private set; }
        /// <summary>현재 실행 중인 행동의 고유 번호. 즉발 행동과 코루틴 행동 모두 같은 번호를 유지한다.</summary>
        public int CurrentActionId => _currentActionId >= 0 ? _currentActionId : ActionCount;

        /// <summary>
        /// 이번 라운드에 열린 <b>턴</b>의 수(전역). 추가행동·패시브 발동은 세지 않는다.
        /// 라운드 제한 시간을 대체하는 축이다.
        /// </summary>
        public int TurnsTaken { get; private set; }

        /// <summary>
        /// 이번 라운드에서 행동치가 실제로 전진한 전투 시간(초).
        /// 연출의 벽시계와 무관하며, 궁극기 자원 충전에 사용한 것과 정확히 같은 축이다.
        /// 밸런스 검증은 이 값을 기준으로 DPM을 환산한다.
        /// </summary>
        public float CombatSecondsElapsed { get; private set; }

        /// <summary>현재 행동 중인 유닛. 없으면 null.</summary>
        public Unit ActingUnit => _acting;

        /// <summary>지금 턴을 진행 중인 유닛. 본 행동이 끝날 때까지 유지된다.</summary>
        public Unit TurnOwner { get; private set; }

        // ══════════════════════════════════════════════════════════
        // 라운드 수명
        // ══════════════════════════════════════════════════════════

        public void BeginRound()
        {
            _actionValues.Clear();
            _turnCounts.Clear();
            _lapCredit.Clear();
            ClearQueue();
            _acting = null;
            TurnOwner = null;
            _actingElapsed = 0f;
            _currentActionId = -1;
            ActionCount = 0;
            TurnsTaken = 0;
            CombatSecondsElapsed = 0f;

            foreach (Unit unit in Participants())
            {
                _actionValues[unit] = FullActionValue(unit);
            }

            ApplyRoundStartActionAdjustments();
        }

        /// <summary>
        /// 전투 시작 시 행동 게이지를 조정하는 효과(에퀴테스 '야습' 등)를 반영한다.
        ///
        /// 패시브는 <c>GridManager.OnRoundStart()</c>에서 걸리고 이 메서드는 그 <b>뒤</b>에 도는
        /// <see cref="BeginRound"/>의 마지막 단계다. 패시브가 직접 AV를 만졌다면
        /// 위의 초기화에 덮여 사라지므로, 조정은 반드시 여기서 한 번에 처리한다.
        /// </summary>
        private void ApplyRoundStartActionAdjustments()
        {
            List<Unit> participants = _actionValues.Keys.ToList();
            if (participants.Count == 0) return;

            foreach (Unit owner in participants)
            {
                if (owner?.ActiveStatuses == null) continue;

                foreach (var status in owner.ActiveStatuses)
                {
                    foreach (var effect in status.Effects)
                    {
                        if (effect?.EffectObject == null) continue;

                        foreach (Unit participant in participants)
                        {
                            float ratio = effect.EffectObject.RoundStartActionAdjustment(participant);
                            if (Mathf.Approximately(ratio, 0f)) continue;

                            float full = FullActionValue(participant);
                            // 양수는 앞당기고(에퀴테스 '야습') 음수는 늦춘다.
                            // 전투 시작 시점에는 모두가 가득 찬 AV로 서 있으므로,
                            // 늦추는 쪽은 한 행동분을 넘겨 밀 수 있어야 한다. 상한은 두 행동분이다.
                            _actionValues[participant] =
                                Mathf.Clamp(_actionValues[participant] - full * ratio, 0f, full * 2f);
                        }
                    }
                }
            }
        }

        public void EndRound()
        {
            _acting?.ActiveSpecialCode?.StopCode();
            _actionValues.Clear();
            _turnCounts.Clear();
            _lapCredit.Clear();
            ClearQueue();
            Combat.SpecialAction.Reset();
            _acting = null;
            _currentActionId = -1;
            TurnOwner = null;
        }

        private void ClearQueue()
        {
            foreach (var pending in _queue)
                if (pending.Kind == ActionKind.Special) Combat.SpecialAction.NotifyResolved(pending.Unit);
            _queue.Clear();
            _queuedKeys.Clear();
        }

        // ══════════════════════════════════════════════════════════
        // AV
        // ══════════════════════════════════════════════════════════

        /// <summary>속도(스탯). DEX가 오르면 그대로 올라간다.</summary>
        public static float SpeedOf(Unit unit)
        {
            // 독립 공격속도 스탯은 없으며 ActionSpeedCurr는 최종 DEX에서만 파생된다.
            return Mathf.Max(1f, SpeedScale * unit.ActionSpeedCurr);
        }

        /// <summary>이 유닛이 한 번 행동하는 데 필요한 AV.</summary>
        public static float FullActionValue(Unit unit) => BaseActionValue / SpeedOf(unit);

        /// <summary>행동 게이지 진행도 0~1. 1이 되면 행동한다. UI의 Action Bar가 쓴다.</summary>
        public float ActionProgress(Unit unit)
        {
            if (unit == null) return 0f;
            if (ReferenceEquals(unit, _acting) || ReferenceEquals(unit, TurnOwner)) return 1f;
            if (!_actionValues.TryGetValue(unit, out float remaining)) return 0f;

            float full = FullActionValue(unit);
            if (full <= 0f) return 0f;
            return Mathf.Clamp01(1f - remaining / full);
        }

        /// <summary>
        /// 유닛의 다음 행동을 앞당긴다. 턴제에서 '쿨다운 감소'에 해당하는 조작이다.
        /// </summary>
        /// <param name="ratio">한 번의 행동에 필요한 AV 대비 비율. 0.25면 4분의 1만큼 앞당긴다.</param>
        public void AdvanceAction(Unit unit, float ratio)
        {
            if (unit == null || ratio <= 0f) return;
            if (!_actionValues.TryGetValue(unit, out float remaining)) return;
            _actionValues[unit] = Mathf.Max(0f, remaining - FullActionValue(unit) * ratio);
            _lapCredit[unit] = _lapCredit.GetValueOrDefault(unit) + ratio;
        }

        /// <summary>
        /// 유닛의 다음 행동을 늦춘다. <see cref="AdvanceAction"/>의 반대다.
        /// 한 번의 행동에 필요한 AV를 넘겨 밀리지는 않는다.
        /// </summary>
        /// <param name="ratio">한 번의 행동에 필요한 AV 대비 비율. 0.5면 절반만큼 늦춘다.</param>
        public void DelayAction(Unit unit, float ratio)
        {
            if (unit == null || ratio <= 0f) return;
            if (!_actionValues.TryGetValue(unit, out float remaining)) return;
            float full = FullActionValue(unit);
            _actionValues[unit] = Mathf.Min(full, remaining + full * ratio);
        }

        /// <summary>궁극기가 큐에 올라가 있는지.</summary>
        public bool HasUltimateReserved(Unit unit)
            => unit != null && _queue.Any(entry => entry.Unit == unit && entry.Kind == ActionKind.Ultimate);

        // ══════════════════════════════════════════════════════════
        // 외부 예약 API — 패시브와 추가행동이 여기로 들어온다
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 추가행동을 예약한다. 대기 중인 본 행동보다 먼저 나가지만
        /// <b>진행 중인 행동을 끊지는 않는다.</b>
        /// </summary>
        /// <param name="key">중복 판정용 식별자. 같은 키가 큐에 있으면 무시된다.</param>
        public bool EnqueueAdditional(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.Additional, key, label, run);

        /// <summary>조건을 만족한 패시브 발동을 예약한다. 모든 행동보다 먼저 실행된다.</summary>
        /// <summary>패시브가 부르는 추가행동. 같은 턴의 다른 행동보다 먼저 나간다.</summary>
        public bool EnqueuePriorityAdditional(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.PriorityAdditional, key, label, run);

        /// <summary>
        /// 협동행동을 예약한다. 짝의 행동에 끼어들어 함께 치는 자리다.
        /// 추가행동 카운터에 잡히지 않도록 <c>OnCoordinatedActivates</c>만 발행한다.
        /// </summary>
        public bool EnqueueCoordinated(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.Coordinated, key, label, run);

        /// <summary>
        /// 특수행동을 예약한다. <b>인드라의 궁극기만이</b> 이 문을 연다.
        /// 열린 순간 전원이 함께 나가야 하므로 추가행동보다 앞 순위다.
        /// </summary>
        public bool EnqueueSpecial(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.Special, key, label, run);

        private bool Enqueue(Unit unit, ActionKind kind, string key, string label, Action run)
        {
            if (unit == null || run == null) return false;
            if (!unit.isActive) return false;

            string fullKey = $"{unit.GetEntityId()}:{kind}:{key}";
            if (!_queuedKeys.Add(fullKey)) return false;   // 이미 같은 행동이 줄 서 있다

            _queue.Add(new PendingAction
            {
                Unit = unit,
                Kind = kind,
                Key = fullKey,
                Label = label,
                Run = run,
                Sequence = _sequence++,
            });
            return true;
        }

        // ══════════════════════════════════════════════════════════
        // 진행
        // ══════════════════════════════════════════════════════════

        /// <summary>매 프레임 호출. 라운드 진행 중에만 돌린다.</summary>
        public void Tick(float deltaTime)
        {
            SyncParticipants();

            // 1) 누군가 행동 중이면 끝날 때까지 기다린다. 한 번에 하나만 행동한다.
            if (_acting != null)
            {
                _actingElapsed += deltaTime;

                bool finished = !_acting.isCasting || !_acting.isActive;
                if (!finished && _actingElapsed < ActionWatchdogSeconds) return;

                if (!finished)
                {
                    Debug.LogWarning($"[ActionScheduler] {_acting.UnitName}의 행동이 {ActionWatchdogSeconds}초를 넘겨 강제 종료합니다.");
                    _acting.isCasting = false;
                    _acting.ActiveSpecialCode?.StopCode();
                }

                _acting = null;
                _actingElapsed = 0f;
                _currentActionId = -1;
            }

            // 2) 자원이 찬 궁극기를 예약한다. 턴과 무관하므로 매번 훑는다.
            ReserveReadyUltimates();

            // 3) 큐에 남은 행동을 우선순위대로 하나 실행한다.
            if (RunNextQueued()) return;

            // 4) 큐가 비었으면 진행 중이던 턴을 닫는다.
            if (TurnOwner != null)
            {
                Unit finishedOwner = TurnOwner;
                TurnOwner = null;
                if (finishedOwner.isActive) finishedOwner.EndTurn();
                return;
            }

            // 5) 다음 행동자에게 턴을 넘긴다.
            OpenNextTurn();
        }

        /// <summary>전투에 참여 중인 유닛(필드 위 · 활성 · 벤치 제외).</summary>
        private static IEnumerable<Unit> Participants()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return Enumerable.Empty<Unit>();

            // 소환수도 자기 DEX로 행동 순서를 얻는다. IsOnField가 칸 유무를 흡수한다.
            return grid.heroList.Concat(grid.enemyList)
                .Where(unit => unit != null && unit.IsOnField);
        }

        /// <summary>새로 등장했거나 사라진 유닛을 AV 표에 반영한다.</summary>
        private void SyncParticipants()
        {
            List<Unit> current = Participants().ToList();

            foreach (Unit unit in current)
            {
                // 라운드 도중 소환된 유닛은 가득 찬 AV로 합류한다.
                if (_actionValues.ContainsKey(unit)) continue;
                _actionValues[unit] = FullActionValue(unit);
                // 턴 수는 지금 가장 적게 행동한 유닛에 맞춘다. 0으로 들이면 모두가 새 유닛을
                // 기다리느라 멈추고, 가장 많이 행동한 쪽에 맞추면 새 유닛이 한 바퀴 늦게 선다.
                _turnCounts[unit] = _turnCounts.Count > 0 ? _turnCounts.Values.Min() : 0;
            }

            // 죽거나 벤치로 내려간 유닛은 제외한다.
            List<Unit> stale = _actionValues.Keys.Where(unit => unit == null || !current.Contains(unit)).ToList();
            foreach (Unit unit in stale)
            {
                _actionValues.Remove(unit);
                _turnCounts.Remove(unit);
                _lapCredit.Remove(unit);
            }

            // 쓰러진 유닛의 예약은 큐에서 걷어낸다.
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].Unit != null && _queue[i].Unit.isActive) continue;
                if (_queue[i].Kind == ActionKind.Special) Combat.SpecialAction.NotifyResolved(_queue[i].Unit);
                _queuedKeys.Remove(_queue[i].Key);
                _queue.RemoveAt(i);
            }

            if (_acting != null && !_acting.isActive)
            {
                _acting.ActiveSpecialCode?.StopCode();
                _acting = null;
                _currentActionId = -1;
            }
            if (TurnOwner != null && !TurnOwner.isActive) TurnOwner = null;
        }

        /// <summary>우선순위가 가장 높은 예약 하나를 실행한다.</summary>
        private bool RunNextQueued()
        {
            while (_queue.Count > 0)
            {
                PendingAction next = _queue
                    .OrderBy(entry => (int)entry.Kind)
                    .ThenBy(entry => entry.Sequence)
                    .First();

                _queue.Remove(next);
                _queuedKeys.Remove(next.Key);

                if (next.Unit == null || !next.Unit.isActive || next.Unit.isControlled)
                {
                    if (next.Kind == ActionKind.Special) Combat.SpecialAction.NotifyResolved(next.Unit);
                    continue;
                }

                _actingElapsed = 0f;
                _currentActionId = ActionCount + 1;
                ActingKind = next.Kind;
                _executingUnit = next.Unit;

                // 추가행동은 자기 턴을 쓰지 않아 OnTurnStart로는 잡히지 않는다.
                // '행동마다' 도는 효과(풍화·니콜 일렉트릭 필드·바스테트 야수의 시선)가 셀 수 있도록
                // 시작을 알린다. 패시브가 부른 우선 추가행동도 성질이 같으므로 함께 발행한다.
                if (IsAdditional(next.Kind))
                {
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnAdditionalActivates,
                        new EventContext(next.Unit));
                }
                else if (next.Kind == ActionKind.Special)
                {
                    // 추가행동 카운터에는 잡히지 않아야 하므로 전용 신호를 따로 낸다.
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnSpecialActivates,
                        new EventContext(next.Unit));
                }
                else if (next.Kind == ActionKind.Coordinated)
                {
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnCoordinatedActivates,
                        new EventContext(next.Unit));
                }

                AnyActionStarted?.Invoke(next.Unit, next.Kind, next.Label);
                next.Run();
                if (next.Kind == ActionKind.Special && !next.Unit.isCasting)
                    Combat.SpecialAction.NotifyResolved(next.Unit);

                // 코드에 따라서는 코루틴 없이 즉시 끝난다(자원만 쌓는 궁극기, 즉발 추가행동 등).
                // 그런 경우 isCasting이 서지 않으므로 붙잡지 않고 바로 다음으로 넘어간다.
                _acting = next.Unit.isCasting ? next.Unit : null;
                ActionCount++;
                if (_acting == null) _currentActionId = -1;
                return true;
            }

            return false;
        }

        /// <summary>
        /// AV가 가장 적은 유닛에게 턴을 넘긴다.
        /// 시간을 흘려보내지 않고 <b>필요한 만큼만</b> 전원의 AV를 깎는다.
        /// </summary>
        private void OpenNextTurn()
        {
            if (_actionValues.Count == 0) return;

            Unit next = PickNext(_actionValues, _turnCounts, _lapCredit, out float lowest);
            if (next == null) return;

            // 추월 몫을 써서 선 것이면 그만큼 깎는다.
            if (NeedsCredit(next, _turnCounts, _actionValues.Keys))
                _lapCredit[next] = Mathf.Max(0f, _lapCredit.GetValueOrDefault(next) - 1f);
            _turnCounts[next] = _turnCounts.GetValueOrDefault(next) + 1;

            // 다음 행동자가 0에 닿을 만큼만 전원을 앞당긴다.
            // 이때 깎인 AV가 곧 흐른 전투 시간이다. 시계는 여기서만 움직인다.
            if (lowest > 0f)
            {
                foreach (Unit unit in _actionValues.Keys.ToList())
                {
                    _actionValues[unit] = Mathf.Max(0f, _actionValues[unit] - lowest);
                }
                // 궁극기 자원은 행동 횟수가 아니라 흐른 전투 시간으로 찬다.
                float combatSeconds = lowest / ActionValuePerSecond;
                CombatSecondsElapsed += combatSeconds;
                AccrueUltimateResources(combatSeconds);
            }

            // 행동을 소비했으므로 AV를 다시 채운다.
            _actionValues[next] = FullActionValue(next);

            TurnOwner = next;
            TurnsTaken++;

            // 턴을 연다. 여기서 상태 지속시간이 줄고 주기 효과가 돌며,
            // 조건을 만족한 패시브가 큐에 줄을 선다.
            next.BeginTurn();

            // 턴 시작 처리 중에 쓰러졌거나 제어에 걸렸으면 본 행동은 건너뛴다.
            if (!next.isActive)
            {
                TurnOwner = null;
                return;
            }
            if (next.isControlled) return;   // 큐에 쌓인 패시브만 처리하고 턴이 닫힌다

            ReserveMainAction(next);
        }

        /// <summary>
        /// 다음 턴의 주인을 고른다. 추월 제한을 지키는 유닛 가운데 AV가 가장 적은 쪽이다.
        /// 동점이면 속도가 빠른 쪽. 가장 적게 행동한 유닛은 늘 자격이 있으므로 비는 일은 없다.
        /// </summary>
        private static Unit PickNext(Dictionary<Unit, float> values, Dictionary<Unit, int> counts,
            Dictionary<Unit, float> credit, out float lowest)
        {
            Unit next = null;
            lowest = float.MaxValue;

            foreach (KeyValuePair<Unit, float> pair in values)
            {
                if (!MayTakeTurn(pair.Key, counts, credit, values.Keys)) continue;
                if (pair.Value > lowest) continue;
                if (Mathf.Approximately(pair.Value, lowest) &&
                    next != null && SpeedOf(pair.Key) <= SpeedOf(next)) continue;

                lowest = pair.Value;
                next = pair.Key;
            }
            return next;
        }

        /// <summary>이 유닛이 지금 턴을 열어도 누구도 허용치 넘게 추월하지 않는가.</summary>
        private static bool MayTakeTurn(Unit unit, Dictionary<Unit, int> counts,
            Dictionary<Unit, float> credit, IEnumerable<Unit> participants)
        {
            int mine = counts.GetValueOrDefault(unit);
            int bonus = Mathf.FloorToInt(credit.GetValueOrDefault(unit));
            foreach (Unit other in participants)
            {
                if (other == null || ReferenceEquals(other, unit)) continue;
                if (mine - counts.GetValueOrDefault(other) >= LapAllowance(unit, other) + bonus) return false;
            }
            return true;
        }

        /// <summary>추월 몫 없이는 설 수 없었는가 — 몫을 깎을지 판단한다.</summary>
        private static bool NeedsCredit(Unit unit, Dictionary<Unit, int> counts, IEnumerable<Unit> participants)
        {
            int mine = counts.GetValueOrDefault(unit);
            foreach (Unit other in participants)
            {
                if (other == null || ReferenceEquals(other, unit)) continue;
                if (mine - counts.GetValueOrDefault(other) >= LapAllowance(unit, other)) return true;
            }
            return false;
        }

        /// <summary>A가 B보다 앞설 수 있는 턴 수. 속도 2배 미만이면 1(번갈아), 2배면 2.</summary>
        private static int LapAllowance(Unit fast, Unit slow)
            => Mathf.Max(1, Mathf.FloorToInt(SpeedOf(fast) / SpeedOf(slow) + 0.0001f));

        /// <summary>
        /// 그 턴의 본 행동을 예약한다. <b>일반행동만</b> 여기서 잡는다.
        /// 궁극기는 턴을 쓰지 않으므로 <see cref="ReserveReadyUltimates"/>가 따로 관리한다.
        /// </summary>
        private void ReserveMainAction(Unit unit)
        {
            if (!CanAct(unit)) return;

            Enqueue(unit, ActionKind.Normal, "main", unit.ActiveNormalCode.CodeName,
                unit.CastNormalCode);
        }

        /// <summary>
        /// 자원이 찬 궁극기를 줄 세운다. 누구의 턴인지와 무관하며 AV도 소비하지 않는다.
        /// 같은 키라 한 번 예약된 궁극기가 중복으로 쌓이지 않는다.
        /// </summary>
        private void ReserveReadyUltimates()
        {
            foreach (Unit unit in _actionValues.Keys.ToList())
            {
                if (!CanUseUltimate(unit)) continue;
                Enqueue(unit, ActionKind.Ultimate, "ult", unit.ActiveUltimateCode.CodeName,
                    unit.CastUltimateCode);
            }
        }

        /// <summary>흐른 전투 시간만큼 참가자 전원의 궁극기 자원을 채운다.</summary>
        private void AccrueUltimateResources(float combatSeconds)
        {
            if (combatSeconds <= 0f) return;
            foreach (Unit unit in _actionValues.Keys.ToList())
            {
                unit?.AccrueUltimateResource(combatSeconds);
            }
        }

        private static bool CanUseUltimate(Unit unit)
        {
            return unit != null
                   && unit.isActive
                   && !unit.isControlled
                   && !unit.isCasting
                   && unit.ActiveUltimateCode != null
                   && unit.ActiveUltimateCode.IsAutoCast
                   && unit.ActiveUltimateCode.HasValidTarget()
                   && unit.CanCastUltimateCode();
        }

        private static bool CanAct(Unit unit)
        {
            return unit != null
                   && unit.isActive
                   && !unit.isControlled
                   && !unit.isCasting
                   && !unit.IsNormalAttackBlocked
                   && unit.ActiveNormalCode != null
                   && unit.ActiveNormalCode.HasValidTarget();
        }

        // ══════════════════════════════════════════════════════════
        // 예측 (행동서열 UI)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 앞으로의 행동 순서를 예측한다. 좌상단 행동서열 UI가 쓴다.
        /// 실제 진행을 건드리지 않고 사본으로만 시뮬레이션한다.
        /// </summary>
        public List<Reservation> ForecastOrder(int count)
        {
            var result = new List<Reservation>();
            if (count <= 0) return result;

            // 큐에 이미 올라간 행동이 가장 먼저다.
            foreach (PendingAction entry in _queue
                         .OrderBy(item => (int)item.Kind)
                         .ThenBy(item => item.Sequence))
            {
                if (result.Count >= count) return result;
                if (entry.Unit == null || !entry.Unit.isActive) continue;

                result.Add(new Reservation
                {
                    Unit = entry.Unit,
                    IsUltimate = entry.Kind == ActionKind.Ultimate,
                    ActionsAhead = result.Count,
                });
            }

            // 나머지는 AV를 사본으로 굴려 순서를 뽑는다.
            var simulated = _actionValues
                .Where(pair => pair.Key != null && pair.Key.isActive)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            // 추월 제한도 사본으로 함께 굴린다. 서열 UI가 실제 순서와 어긋나지 않아야 한다.
            var simCounts = simulated.Keys.ToDictionary(unit => unit, unit => _turnCounts.GetValueOrDefault(unit));
            var simCredit = simulated.Keys.ToDictionary(unit => unit, unit => _lapCredit.GetValueOrDefault(unit));

            float elapsed = 0f;
            while (result.Count < count && simulated.Count > 0)
            {
                Unit next = PickNext(simulated, simCounts, simCredit, out float lowest);
                if (next == null) break;
                if (NeedsCredit(next, simCounts, simulated.Keys))
                    simCredit[next] = Mathf.Max(0f, simCredit[next] - 1f);
                simCounts[next] = simCounts[next] + 1;

                elapsed += lowest;
                foreach (Unit unit in simulated.Keys.ToList())
                {
                    simulated[unit] = Mathf.Max(0f, simulated[unit] - lowest);
                }
                simulated[next] = FullActionValue(next);

                result.Add(new Reservation
                {
                    Unit = next,
                    IsUltimate = false,
                    ActionsAhead = result.Count,
                });
            }

            return result;
        }
    }
}
