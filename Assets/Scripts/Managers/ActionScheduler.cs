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
    ///   속도 = 유닛의 속도 스탯        (기본 100)
    ///   AV   = 10000 / 속도            (작을수록 먼저 행동)
    /// 시간을 흘려보내지 않고 <b>다음 행동자에게 필요한 만큼만 전원의 AV를 한 번에 깎는다.</b>
    /// 그래서 프레임 수·연출 길이가 순서에 영향을 주지 않는다.
    ///
    /// 우선순위 — 낮을수록 먼저다.
    ///   0 우선 추가행동 → 2 추가행동 → 3 협동행동 → 4 특수행동 → 5 궁극기 → 6 일반행동
    /// 유닛의 턴이 열리면 상태·주기 효과가 먼저 진행되고(패시브가 여기서 줄을 선다),
    /// 그 다음 추가행동, 마지막으로 그 턴의 일반행동이 나간다.
    ///
    /// <b>궁극기는 턴을 쓰지 않는다.</b> 자원이 차는 즉시 누구의 턴이든 상관없이 줄을 서고,
    /// 발동해도 AV가 리셋되지 않아 원래 예정된 일반행동 차례는 그대로 남는다.
    ///
    /// 중복 방지 — 같은 (유닛 + 종류 + 이름) 조합은 큐에 하나만 존재한다.
    /// 추가행동이 여러 번 겹쳐 터지거나, 한 턴에 본 행동이 두 번 잡히지 않는다.
    /// </summary>
    public class ActionScheduler
    {
        /// <summary>AV 산출의 분자. 스타레일과 동일하게 10000을 쓴다.</summary>
        public const float BaseActionValue = 10000f;

        /// <summary>AV → 전투 시간(초) 환산. 로그 · 통계에만 쓴다 — 판정은 턴으로만 흐른다.</summary>
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

            /// <summary>추가행동. 턴을 쓰지 않는다.</summary>
            Additional = 2,

            /// <summary>
            /// 협동행동. 짝이 행동할 때 끼어들어 함께 친다. 턴을 쓰지 않는다.
            ///
            /// <b>추가행동과 판정을 나눈 이유는 트리거다.</b> <c>OnAdditionalActivates</c>를 듣는 코드가
            /// 이미 여럿인데, 협공이 그것들을 전부 켜면 듀오 하나 때문에 다른 코드의 균형이 흔들린다.
            ///
            /// 자기를 부른 일반행동 <b>바로 뒤에</b> 붙어야 하므로 추가행동보다 뒤, 궁극기보다 앞이다.
            /// </summary>
            Coordinated = 3,

            /// <summary>특정 트리거가 여는 고유 SP. 턴과 AV를 쓰지 않으며 추가행동으로 세지 않는다.</summary>
            Special = 4,

            /// <summary>궁극기. 턴을 쓰지 않고 AV도 리셋하지 않는다.</summary>
            Ultimate = 5,

            /// <summary>일반행동 — 또는 그 자리를 대신 쓰는 <b>대체행동</b>. 턴을 쓴다.</summary>
            Normal = 6,
        }

        /// <summary>
        /// 행동 하나가 막 나가려 한다(유닛 · 종류 · 예약 이름).
        /// 전투 로그와 행동 시작에 반응하는 패시브가 듣는다. 같은 행동을 다시 예약하는 재진입은 금지한다.
        /// </summary>
        public static event Action<Unit, ActionKind, string> AnyActionStarted;

        /// <summary>행동치 조작을 행동서열이 작은 표식과 보간 이동으로 설명할 때 쓴다.</summary>
        public static event Action<Unit, bool> AnyActionOrderAdjusted;

        /// <summary>
        /// 실행된 행동 하나가 끝났다(유닛 · 종류). 코루틴 행동은 시전이 끝날 때, 즉발 행동은 실행 직후 발행한다.
        /// "특수행동을 마칠 때마다"(인드라 소집 순환 · 찬드라 달빛 순환)처럼 <b>종류별 종료</b>를 세는 코드가 듣는다.
        /// </summary>
        public static event Action<Unit, ActionKind> AnyActionFinished;

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
            public Func<bool> CanRun;
            public Action Cancel;
            public int Sequence;   // 같은 우선순위 안에서는 먼저 예약된 것이 먼저다
        }

        /// <summary>행동 예약 상태. UI가 읽는다.</summary>
        public sealed class Reservation
        {
            public Unit Unit;
            public bool IsUltimate;
            public ActionKind Kind;
            public string Label;
            public string StableKey;
            public bool IsQueued;

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
        private List<Reservation> _forecastCache = new();
        private readonly List<ForecastUnitInput> _forecastUnits = new();
        private readonly List<ForecastQueueInput> _forecastQueue = new();
        private bool _forecastValid;
        private int _forecastRequestedCount;

        private struct ForecastUnitInput
        {
            public Unit Unit;
            public float Value, Credit, Speed;
            public int Turns;
            public bool Active, Summon;
            public string Label;
        }
        private struct ForecastQueueInput
        {
            public PendingAction Entry;
            public Unit Unit;
            public ActionKind Kind;
            public int Sequence;
            public string Label;
            public bool Active;
        }


        private PendingAction _previewing;
        private float _previewRemaining;
        private float _resultRemaining;
        // 초 단위 대기는 연출 전용이다. AV·턴·상태 기간·발동 횟수는 바꾸지 않는다.
        public const float PreviewSeconds = .36f;
        public const float ResultSeconds = .42f;
        public bool IsPresenting => _previewing != null || _resultRemaining > 0 || _acting != null;
        private Unit _acting;
        // 선제 개입이 ActingKind를 잠시 바꿔도 끝난 행동의 종류를 정확히 알리기 위해 따로 든다.
        private ActionKind _actingRootKind;
        private int _actingRootActionId = -1;
        private int _interruptDepth;
        private int _roundGeneration;
        private float _interruptElapsed;
        private sealed class InterruptFrame
        {
            public Unit Unit;
            public int ActionId;
            public bool Started;
        }
        private readonly List<InterruptFrame> _interrupts = new();
        private float _actingElapsed;
        private int _sequence;
        private int _actionIdSequence;
        private int _currentActionId = -1;
        public ActionKind ActingKind { get; private set; }
        private Unit _executingUnit;
        /// <summary>코루틴뿐 아니라 즉발 행동의 콜백 안에서도 현재 실행자를 식별한다.</summary>
        public Unit ExecutingUnit => _currentActionId >= 0 ? _executingUnit : null;
        /// <summary>예고 뒤 실행을 기다리는 유닛. 취소된 예고 UI를 정리할 때만 조회한다.</summary>
        public Unit PreviewUnit => _previewing?.Unit;

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
        public Unit ActingUnit => _acting ?? _previewing?.Unit;

        /// <summary>지금 턴을 진행 중인 유닛. 본 행동이 끝날 때까지 유지된다.</summary>
        public Unit TurnOwner { get; private set; }

        // ══════════════════════════════════════════════════════════
        // 라운드 수명
        // ══════════════════════════════════════════════════════════

        public void BeginRound()
        {
            _roundGeneration++;
            _interruptDepth = 0;
            _interruptElapsed = 0f;
            _interrupts.Clear();
            _actingRootActionId = -1;
            Combat.ActionDot.ResetClock();
            Combat.SpecialAction.Reset();
            Combat.AttackTargetRouter.ResetRound();
            _actionValues.Clear();
            _turnCounts.Clear();
            _lapCredit.Clear();
            ClearQueue();
            _acting = null;
            _previewing = null; _resultRemaining = 0;
            TurnOwner = null;
            _actingElapsed = 0f;
            _currentActionId = -1;
            _actionIdSequence = 0;
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

        /// <summary>
        /// 유한 다단 초월기의 실제 효과 진행을 알린다. 예고·예약·매 프레임은 진행이 아니다.
        /// 현재 실행 중인 초월기 본체만 갱신하고, 다음 유효 효과가 8초간 없으면 기존 감시가 중단한다.
        /// </summary>
        public void NotifyUltimateProgress(Unit unit)
        {
            if (unit == null || _acting != unit || _actingRootKind != ActionKind.Ultimate || _interruptDepth > 0) return;
            _actingElapsed = 0f;
        }

        public void EndRound()
        {
            _forecastValid = false;
            _forecastCache.Clear();
            _forecastUnits.Clear();
            _forecastQueue.Clear();
            _roundGeneration++;
            _interruptDepth = 0;
            _interruptElapsed = 0f;
            _interrupts.Clear();
            _actingRootActionId = -1;
            _executingUnit = null;
            Combat.SpecialAction.Reset();
            _actionValues.Clear();
            _turnCounts.Clear();
            _lapCredit.Clear();
            ClearQueue();
            _acting = null;
            _previewing = null; _previewRemaining = _resultRemaining = 0;
            _currentActionId = -1;
            TurnOwner = null;
        }

        private void ClearQueue()
        {
            _queue.Clear();
            _queuedKeys.Clear();
        }

        // ══════════════════════════════════════════════════════════
        // AV
        // ══════════════════════════════════════════════════════════

        /// <summary>속도 스탯. 행동값 = 10000 ÷ 속도.</summary>
        public static float SpeedOf(Unit unit) => Mathf.Max(1f, unit.SpeedCurr);

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
            AnyActionOrderAdjusted?.Invoke(unit, true);
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
            AnyActionOrderAdjusted?.Invoke(unit, false);
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

        /// <summary>실행 직전 대상이 사라지면 공지 · 이벤트 없이 버리는 추가행동(아그니 · 수리야의 추공).</summary>
        public bool EnqueueAdditional(Unit unit, string key, string label, Action run, Func<bool> canRun, Action cancelled = null)
            => Enqueue(unit, ActionKind.Additional, key, label, run, canRun, cancelled);

        /// <summary>조건을 만족한 패시브 발동을 예약한다. 모든 행동보다 먼저 실행된다.</summary>
        /// <summary>패시브가 부르는 추가행동. 같은 턴의 다른 행동보다 먼저 나간다.</summary>
        public bool EnqueuePriorityAdditional(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.PriorityAdditional, key, label, run, null);

        /// <summary>실행 직전 조건이 깨지면 공지·이벤트 없이 버리는 우선 추가행동.</summary>
        public bool EnqueuePriorityAdditional(Unit unit, string key, string label, Action run,
            Func<bool> canRun, Action cancelled = null)
            => Enqueue(unit, ActionKind.PriorityAdditional, key, label, run, canRun, cancelled);

        /// <summary>
        /// 협동행동을 예약한다. 짝의 행동에 끼어들어 함께 치는 자리다.
        /// 추가행동 카운터에 잡히지 않도록 <c>OnCoordinatedActivates</c>만 발행한다.
        /// </summary>
        public bool EnqueueCoordinated(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.Coordinated, key, label, run);

        /// <summary>특정 트리거가 연 SP를 예약한다. 추가행동 판정과 이벤트를 공유하지 않는다.</summary>
        public bool EnqueueSpecial(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.Special, key, label, run);

        /// <summary>실행 직전 처치각·대상이 사라지면 공지 없이 버리는 특수행동.</summary>
        public bool EnqueueSpecial(Unit unit, string key, string label, Action run, Func<bool> canRun)
            => Enqueue(unit, ActionKind.Special, key, label, run, canRun);

        private bool Enqueue(Unit unit, ActionKind kind, string key, string label, Action run,
            Func<bool> canRun = null, Action cancelled = null)
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
                CanRun = canRun,
                Cancel = cancelled,
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
            // 일시정지는 즉발 코드·AV·상태 턴 진행까지 함께 멈춘다.
            if(deltaTime<=0) return;
            SyncParticipants();
            // 부모가 선제 반격의 정점에서 쓰러져도 자식의 잔여 연출·완료 판정을 먼저 끝낸다.
            if (_interruptDepth > 0)
            {
                _interruptElapsed += deltaTime;
                if (_interruptElapsed < ActionWatchdogSeconds) return;
                AbortInterruptedAction();
            }
            if(_resultRemaining>0) { _resultRemaining=Mathf.Max(0,_resultRemaining-deltaTime); return; }
            if(_previewing!=null)
            {
                _previewRemaining-=deltaTime;
                if(_previewRemaining>0) return;
                var ready=_previewing; _previewing=null;
                if(ready.Unit!=null && ready.Unit.isActive && !ready.Unit.isControlled &&
                    (ready.CanRun==null || ready.CanRun())) ExecuteQueued(ready);
                else { ready.Cancel?.Invoke(); if(ready.Kind==ActionKind.Special)Combat.SpecialAction.NotifyResolved(ready.Unit); }
                return;
            }

            // 1) 누군가 행동 중이면 끝날 때까지 기다린다. 한 번에 하나만 행동한다.
            if (_acting != null)
            {
                _actingElapsed += deltaTime;

                bool finished = !_acting.isCasting || !_acting.isActive;
                if (!finished && _actingElapsed < ActionWatchdogSeconds) return;

                if (!finished)
                {
                    Debug.LogWarning($"[ActionScheduler] {_acting.UnitName}의 행동이 {ActionWatchdogSeconds}초를 넘겨 강제 종료합니다.");
                    StopActingCoroutine();
                    _acting.isCasting = false;
                }

                Unit finishedUnit = _acting;
                _acting = null;
                _currentActionId = _actingRootActionId;
                ActingKind = _actingRootKind;
                _executingUnit = finishedUnit;
                Combat.ActionDot.OnActionFinished(finishedUnit, _actingRootKind);
                AnyActionFinished?.Invoke(finishedUnit, _actingRootKind);
                _actingElapsed = 0f;
                _currentActionId = -1;
                _actingRootActionId = -1;
                _executingUnit = null;
                _resultRemaining=ResultSeconds;
                return;
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
        private void StopActingCoroutine()
        {
            if (_acting == null) return;
            // 연출 상한을 넘긴 행동의 뒤늦은 타격이 다음 행동에 섞이지 않게 판정 루틴도 중단한다.
            _acting.StopAllCoroutines();
            if (_actingRootKind == ActionKind.Normal) _acting.ActiveNormalCode?.StopCode();
            else if (_actingRootKind == ActionKind.Special) _acting.ActiveSpecialCode?.StopCode();
            else if (_actingRootKind == ActionKind.Ultimate) _acting.ActiveUltimateCode?.StopCode();
        }

        private void AbortInterruptedAction()
        {
            Debug.LogWarning($"[ActionScheduler] 선제 개입이 {ActionWatchdogSeconds}초를 넘겨 중단합니다.");
            _roundGeneration++;
            _interruptDepth = 0;
            _interruptElapsed = 0f;
            StopActingCoroutine();
            foreach (InterruptFrame frame in _interrupts.AsEnumerable().Reverse())
            {
                if (!frame.Started) continue;
                _currentActionId = frame.ActionId;
                _executingUnit = frame.Unit;
                ActingKind = ActionKind.PriorityAdditional;
                Combat.ActionDot.OnActionFinished(frame.Unit, ActionKind.PriorityAdditional);
                AnyActionFinished?.Invoke(frame.Unit, ActionKind.PriorityAdditional);
            }
            _interrupts.Clear();
            _currentActionId = _actingRootActionId;
            _executingUnit = _acting;
            ActingKind = _actingRootKind;
            if (_acting != null) _acting.isCasting = false;
        }

        /// <summary>전투에 참여 중인 유닛(필드 위 · 활성 · 벤치 제외).</summary>
        private static IEnumerable<Unit> Participants()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return Enumerable.Empty<Unit>();

            // 소환수는 칸이 없어 별도 목록에 있다. 행동치를 가진 소환수만 순서에 낀다 —
            // 소환자 코드가 지시할 때만 움직이는 소환수는 IsOnField여도 스케줄러가 부르지 않는다.
            return grid.heroList.Concat(grid.enemyList)
                .Where(unit => unit != null && unit.IsOnField)
                .Concat(grid.summonList.Where(unit => unit != null && unit.IsOnField && unit.TakesScheduledTurns));
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
                _turnCounts[unit] = current.Where(other => !other.IsSummon && _turnCounts.ContainsKey(other))
                    .Select(other => _turnCounts[other]).DefaultIfEmpty(0).Min();
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

            if (TurnOwner != null && !TurnOwner.isActive) TurnOwner = null;
        }

        /// <summary>우선순위가 가장 높은 예약 하나를 실행한다.</summary>
        private bool RunNextQueued(bool immediate = false)
        {
            while (_queue.Count > 0)
            {
                PendingAction next = _queue
                    .OrderBy(entry => (int)entry.Kind)
                    .ThenBy(entry => entry.Sequence)
                    .First();

                _queue.Remove(next);
                _queuedKeys.Remove(next.Key);

                if (next.Unit == null || !next.Unit.isActive || next.Unit.isControlled ||
                    (next.CanRun != null && !next.CanRun()))
                {
                    next.Cancel?.Invoke();
                    if (next.Kind == ActionKind.Special) Combat.SpecialAction.NotifyResolved(next.Unit);
                    continue;
                }

                Combat.ActionAnnouncer.AnnounceScheduled(next.Unit,next.Kind,next.Label);
                if(immediate) ExecuteQueued(next);
                else { _previewing=next; _previewRemaining=PreviewSeconds; }
                return true;
            }

            return false;
        }

        private void ExecuteQueued(PendingAction next)
        {
                _actingElapsed = 0f;
                _currentActionId = ++_actionIdSequence;
                _actingRootActionId = _currentActionId;
                ActingKind = next.Kind;
                _actingRootKind = next.Kind;
                _executingUnit = next.Unit;

                // 추가행동은 자기 턴을 쓰지 않아 OnTurnStart로는 잡히지 않는다.
                // '행동마다' 도는 효과(풍화·니콜 일렉트릭 필드·바스테트 야수의 시선)가 셀 수 있도록
                // 시작을 알린다. 패시브가 부른 우선 추가행동도 성질이 같으므로 함께 발행한다.
                if (IsAdditional(next.Kind))
                {
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnAdditionalActivates,
                        new EventContext(next.Unit));
                }
                else if (next.Kind == ActionKind.Coordinated)
                {
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnCoordinatedActivates,
                        new EventContext(next.Unit));
                }
                else if (next.Kind == ActionKind.Special)
                {
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnSpecialActivates,
                        new EventContext(next.Unit));
                }

                Combat.ActionDot.OnActionStarted(next.Unit, next.Kind);
                if (!next.Unit.isActive)
                {
                    if (next.Kind == ActionKind.Special) Combat.SpecialAction.NotifyResolved(next.Unit);
                    _currentActionId = -1;
                    _executingUnit = null;
                    return;
                }
                AnyActionStarted?.Invoke(next.Unit, next.Kind, next.Label);
                next.Run();

                // 코드에 따라서는 코루틴 없이 즉시 끝난다(자원만 쌓는 궁극기, 즉발 추가행동 등).
                // 그런 경우 isCasting이 서지 않으므로 붙잡지 않고 바로 다음으로 넘어간다.
                _acting = next.Unit.isCasting ? next.Unit : null;
                ActionCount++;
                if (_acting == null)
                {
                    // 비동기 행동과 동일하게 완료 훅까지 실제 행동 ID/실행 문맥을 보존한다.
                    Combat.ActionDot.OnActionFinished(next.Unit, next.Kind);
                    AnyActionFinished?.Invoke(next.Unit, next.Kind);
                    _currentActionId = -1;
                    _actingRootActionId = -1;
                    _executingUnit = null;
                }
                if(_acting==null) _resultRemaining=ResultSeconds;
        }

        /// <summary>
        /// 진행 중인 공격 코루틴 안에서 선제 추가행동을 끝까지 해결한다. 부모 행동의 실행 문맥은
        /// 보존하고 자식에게 별도 행동 ID를 주어 반격 후속과 행동당 중복 판정이 섞이지 않게 한다.
        /// </summary>
        public System.Collections.IEnumerator RunPreAttackInterrupt(Unit unit, string label,
            Func<System.Collections.IEnumerator> routineFactory)
        {
            if (unit == null || !unit.isActive || unit.isControlled || routineFactory == null) yield break;

            int parentActionId = _currentActionId;
            ActionKind parentKind = ActingKind;
            Unit parentExecutingUnit = _executingUnit;
            int generation = _roundGeneration;
            bool started = false;
            var frame = new InterruptFrame { Unit = unit, ActionId = ++_actionIdSequence };
            _interrupts.Add(frame);
            if (_interruptDepth == 0) _interruptElapsed = 0f;
            _interruptDepth++;
            try
            {
                _currentActionId = frame.ActionId;
                ActingKind = ActionKind.PriorityAdditional;
                _executingUnit = unit;
                Combat.ActionDot.OnActionStarted(unit, ActionKind.PriorityAdditional);
                if (!unit.isActive) yield break;
                unit.Invoke(BaseEnums.UnitEventType.OnAdditionalActivates, new EventContext(unit));
                // 선제 개입은 행동 슬롯을 추가행동으로 빌리지만, 표시는 패시브로 고정한다.
                Combat.ActionAnnouncer.Announce(unit, Combat.ActionAnnouncementKind.Passive, label, followsAction: true);
                started = true;
                frame.Started = true;
                AnyActionStarted?.Invoke(unit, ActionKind.PriorityAdditional, label);

                System.Collections.IEnumerator routine = routineFactory();
                if (routine != null) yield return routine;
                if (_roundGeneration == generation) ActionCount++;
            }
            finally
            {
                // 라운드 전환·강제 종료 뒤에는 과거 개입이 새 행동 문맥을 덮어쓰지 않는다.
                if (_roundGeneration == generation)
                {
                    try
                    {
                        if (started)
                        {
                            Combat.ActionDot.OnActionFinished(unit, ActionKind.PriorityAdditional);
                            AnyActionFinished?.Invoke(unit, ActionKind.PriorityAdditional);
                        }
                    }
                    finally
                    {
                        _currentActionId = parentActionId;
                        ActingKind = parentKind;
                        _executingUnit = parentExecutingUnit;
                        _interrupts.Remove(frame);
                        _interruptDepth--;
                    }
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>멈춘 시각 검증 장면에서 예약 조건·공지까지 실제 경로로 한 건 실행한다.</summary>
        public bool DebugRunNextQueued() => RunNextQueued(immediate:true);

        public int DebugQueuedCount(ActionKind kind)
            => _queue.Count(entry => entry.Kind == kind);
#endif

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
                CombatSecondsElapsed += lowest / ActionValuePerSecond;
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
            // 소환수는 추월 제한 밖이다. 한 번 행동하고 사라지는 분신이 제한에 끼면 느린 분신을 기다리느라
            // 다른 유닛이 멈추거나 분신이 제 속도보다 일찍 서서, "고정 속도"라는 약속이 깨진다.
            if (unit.IsSummon) return true;
            int mine = counts.GetValueOrDefault(unit);
            int bonus = Mathf.FloorToInt(credit.GetValueOrDefault(unit));
            foreach (Unit other in participants)
            {
                if (other == null || ReferenceEquals(other, unit) || other.IsSummon) continue;
                if (mine - counts.GetValueOrDefault(other) >= LapAllowance(unit, other) + bonus) return false;
            }
            return true;
        }

        /// <summary>추월 몫 없이는 설 수 없었는가 — 몫을 깎을지 판단한다.</summary>
        private static bool NeedsCredit(Unit unit, Dictionary<Unit, int> counts, IEnumerable<Unit> participants)
        {
            if (unit.IsSummon) return false;
            int mine = counts.GetValueOrDefault(unit);
            foreach (Unit other in participants)
            {
                if (other == null || ReferenceEquals(other, unit) || other.IsSummon) continue;
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
        /// <summary>HUD용 예측. 입력이 같으면 이전 결과를 재사용한다. 반환값은 현재 프레임에서만 읽고 저장·수정하지 않는다.</summary>
        public IReadOnlyList<Reservation> ForecastOrderCached(int count)
        {
            if (ForecastInputsMatch(count)) return _forecastCache;
            _forecastCache = ForecastOrder(count);
            _forecastRequestedCount = count;
            _forecastUnits.Clear();
            foreach (var pair in _actionValues) _forecastUnits.Add(ForecastInput(pair.Key, pair.Value));
            _forecastQueue.Clear();
            foreach (var entry in _queue) _forecastQueue.Add(new ForecastQueueInput
                { Entry = entry, Unit = entry.Unit, Kind = entry.Kind, Sequence = entry.Sequence, Label = entry.Label,
                    Active = entry.Unit != null && entry.Unit.isActive });
            _forecastValid = true;
            return _forecastCache;
        }

        private ForecastUnitInput ForecastInput(Unit unit, float value) => new()
        {
            Unit = unit, Value = value, Turns = _turnCounts.GetValueOrDefault(unit),
            Credit = _lapCredit.GetValueOrDefault(unit), Speed = unit != null ? SpeedOf(unit) : 0f,
            Active = unit != null && unit.isActive, Summon = unit != null && unit.IsSummon,
            Label = unit != null ? unit.ActiveNormalCode?.CodeName : null,
        };

        private bool ForecastInputsMatch(int count)
        {
            if (!_forecastValid || _forecastRequestedCount != count || _forecastUnits.Count != _actionValues.Count ||
                _forecastQueue.Count != _queue.Count) return false;
            for (int i = 0; i < _queue.Count; i++)
            {
                var saved = _forecastQueue[i]; var current = _queue[i];
                if (!ReferenceEquals(saved.Entry, current) || !ReferenceEquals(saved.Unit, current.Unit) ||
                    saved.Kind != current.Kind || saved.Sequence != current.Sequence || saved.Label != current.Label ||
                    saved.Active != (current.Unit != null && current.Unit.isActive)) return false;
            }
            int at = 0;
            foreach (var pair in _actionValues)
            {
                var saved = _forecastUnits[at++]; var current = ForecastInput(pair.Key, pair.Value);
                if (!ReferenceEquals(saved.Unit, current.Unit) || saved.Value != current.Value ||
                    saved.Turns != current.Turns || saved.Credit != current.Credit || saved.Speed != current.Speed ||
                    saved.Active != current.Active || saved.Summon != current.Summon || saved.Label != current.Label)
                    return false;
            }
            return true;
        }

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
                    Kind = entry.Kind,
                    Label = entry.Label,
                    StableKey = $"queued:{entry.Sequence}",
                    IsQueued = true,
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
            var occurrences = new Dictionary<Unit, int>();
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
                    Kind = ActionKind.Normal,
                    Label = next.ActiveNormalCode?.CodeName,
                    StableKey = $"normal:{next.GetEntityId()}:{occurrences.GetValueOrDefault(next)}",
                    IsQueued = false,
                    ActionsAhead = result.Count,
                });
                occurrences[next] = occurrences.GetValueOrDefault(next) + 1;
            }

            return result;
        }
    }
}
