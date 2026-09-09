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

        /// <summary>행동 종류. 값이 작을수록 먼저 실행된다.</summary>
        public enum ActionKind
        {
            Passive = 0,
            Additional = 1,
            Ultimate = 2,
            Normal = 3,
        }

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
        private readonly List<PendingAction> _queue = new();
        private readonly HashSet<string> _queuedKeys = new();

        private Unit _acting;
        private float _actingElapsed;
        private int _sequence;
        private int _currentActionId = -1;

        /// <summary>이번 라운드에 해결된 행동 수. 전역 행동 카운트다.</summary>
        public int ActionCount { get; private set; }
        /// <summary>현재 실행 중인 행동의 고유 번호. 즉발 행동과 코루틴 행동 모두 같은 번호를 유지한다.</summary>
        public int CurrentActionId => _currentActionId >= 0 ? _currentActionId : ActionCount;

        /// <summary>
        /// 이번 라운드에 열린 <b>턴</b>의 수(전역). 추가행동·패시브 발동은 세지 않는다.
        /// 라운드 제한 시간을 대체하는 축이다.
        /// </summary>
        public int TurnsTaken { get; private set; }

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
            ClearQueue();
            _acting = null;
            TurnOwner = null;
            _actingElapsed = 0f;
            _currentActionId = -1;
            ActionCount = 0;
            TurnsTaken = 0;

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
            _actionValues.Clear();
            ClearQueue();
            _acting = null;
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
        public bool EnqueuePassive(Unit unit, string key, string label, Action run)
            => Enqueue(unit, ActionKind.Passive, key, label, run);

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
                if (!_actionValues.ContainsKey(unit)) _actionValues[unit] = FullActionValue(unit);
            }

            // 죽거나 벤치로 내려간 유닛은 제외한다.
            List<Unit> stale = _actionValues.Keys.Where(unit => unit == null || !current.Contains(unit)).ToList();
            foreach (Unit unit in stale) _actionValues.Remove(unit);

            // 쓰러진 유닛의 예약은 큐에서 걷어낸다.
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].Unit != null && _queue[i].Unit.isActive) continue;
                _queuedKeys.Remove(_queue[i].Key);
                _queue.RemoveAt(i);
            }

            if (_acting != null && !_acting.isActive)
            {
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

                if (next.Unit == null || !next.Unit.isActive) continue;
                if (next.Unit.isControlled) continue;   // 제어 중이면 예약을 버린다

                _actingElapsed = 0f;
                _currentActionId = ActionCount + 1;

                // 추가행동은 자기 턴을 쓰지 않아 OnTurnStart로는 잡히지 않는다.
                // '행동마다' 도는 효과(풍화)가 셀 수 있도록 시작을 알린다.
                if (next.Kind == ActionKind.Additional)
                {
                    next.Unit.Invoke(BaseEnums.UnitEventType.OnAdditionalActivates,
                        new EventContext(next.Unit));
                }

                next.Run();

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

            Unit next = null;
            float lowest = float.MaxValue;

            foreach (KeyValuePair<Unit, float> pair in _actionValues)
            {
                // 동점이면 속도가 빠른 쪽이 먼저 행동한다.
                if (pair.Value > lowest) continue;
                if (Mathf.Approximately(pair.Value, lowest) &&
                    next != null && SpeedOf(pair.Key) <= SpeedOf(next)) continue;

                lowest = pair.Value;
                next = pair.Key;
            }

            if (next == null) return;

            // 다음 행동자가 0에 닿을 만큼만 전원을 앞당긴다.
            // 이때 깎인 AV가 곧 흐른 전투 시간이다. 시계는 여기서만 움직인다.
            if (lowest > 0f)
            {
                foreach (Unit unit in _actionValues.Keys.ToList())
                {
                    _actionValues[unit] = Mathf.Max(0f, _actionValues[unit] - lowest);
                }
                // 궁극기 자원은 행동 횟수가 아니라 흐른 전투 시간으로 찬다.
                AccrueUltimateResources(lowest / ActionValuePerSecond);
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
                   && unit.ultimateCooldown <= 0f
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

            float elapsed = 0f;
            while (result.Count < count && simulated.Count > 0)
            {
                Unit next = null;
                float lowest = float.MaxValue;
                foreach (KeyValuePair<Unit, float> pair in simulated)
                {
                    if (pair.Value > lowest) continue;
                    if (Mathf.Approximately(pair.Value, lowest) &&
                        next != null && SpeedOf(pair.Key) <= SpeedOf(next)) continue;
                    lowest = pair.Value;
                    next = pair.Key;
                }

                if (next == null) break;

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
