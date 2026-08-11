using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 전투의 모든 행동을 중앙에서 예약하고 한 번에 하나씩 실행한다.
    ///
    /// 행동치(Action Value) 모델은 붕괴: 스타레일을 따른다.
    ///   속도  = 100 × ActionSpeedCurr   (ActionSpeedCurr = 1 + 최종 DEX×0.01)
    ///   AV    = 10000 / 속도            (작을수록 먼저 행동)
    ///   AV는 매초 <see cref="DrainRate"/>만큼 줄고, 0이 되면 행동을 예약한다.
    ///
    /// 핵심 규칙:
    ///   · 한 번에 한 유닛만 행동한다. 누군가 행동 중이면 다른 유닛의 AV는 멈춘다.
    ///   · 일반공격도 예외가 아니다(기존처럼 병렬로 때리지 않는다).
    ///   · 궁극기는 속도와 무관하게 조건 충족 즉시 예약되고, 현재 행동이 끝나면
    ///     대기 중인 일반행동을 제치고 먼저 실행된다.
    /// </summary>
    public class ActionScheduler
    {
        /// <summary>AV 산출의 분자. 스타레일과 동일하게 10000을 쓴다.</summary>
        public const float BaseActionValue = 10000f;

        /// <summary>속도 100 = DEX 0 기준. DEX 1당 속도 1이 오른다.</summary>
        public const float SpeedScale = 100f;

        /// <summary>
        /// AV가 1초에 줄어드는 양. 전투 전체의 템포를 정하는 유일한 상수다.
        /// 속도 100(DEX 0)이면 AV 100 → 2.5초마다 행동한다.
        /// </summary>
        public const float DrainRate = 40f;

        /// <summary>
        /// 행동이 이 시간을 넘겨도 끝나지 않으면 강제로 종료 처리한다.
        /// 코드 구현이 isCasting을 되돌리지 못하는 경우에도 전투가 멈추지 않게 하는 안전장치.
        /// </summary>
        private const float ActionWatchdogSeconds = 8f;

        /// <summary>행동 예약 상태. UI가 읽는다.</summary>
        public sealed class Reservation
        {
            public Unit Unit;
            public bool IsUltimate;
            /// <summary>예약까지(또는 실행까지) 남은 예상 시간(초). 표시 정렬용.</summary>
            public float EtaSeconds;
        }

        private readonly Dictionary<Unit, float> _actionValues = new();
        private readonly List<Unit> _ultimateQueue = new();

        private Unit _acting;
        private float _actingElapsed;

        /// <summary>현재 행동 중인 유닛. 없으면 null.</summary>
        public Unit ActingUnit => _acting;

        /// <summary>라운드 시작 시 호출. 참가 유닛의 AV를 초기화한다.</summary>
        public void BeginRound()
        {
            _actionValues.Clear();
            _ultimateQueue.Clear();
            _acting = null;
            _actingElapsed = 0f;

            foreach (Unit unit in Participants())
            {
                _actionValues[unit] = FullActionValue(unit);
            }
        }

        public void EndRound()
        {
            _actionValues.Clear();
            _ultimateQueue.Clear();
            _acting = null;
        }

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
            if (ReferenceEquals(unit, _acting)) return 1f;
            if (!_actionValues.TryGetValue(unit, out float remaining)) return 0f;

            float full = FullActionValue(unit);
            if (full <= 0f) return 0f;
            return Mathf.Clamp01(1f - remaining / full);
        }

        /// <summary>궁극기 예약 대기 중인지.</summary>
        public bool HasUltimateReserved(Unit unit) => unit != null && _ultimateQueue.Contains(unit);

        /// <summary>
        /// 매 프레임 호출. 라운드 진행 중에만 돌린다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            SyncParticipants();

            // 1) 행동 중이면 끝날 때까지 기다린다. 이 동안 AV는 멈춘다.
            if (_acting != null)
            {
                _actingElapsed += deltaTime;

                bool finished = !_acting.isCasting || !_acting.isActive;
                if (finished || _actingElapsed >= ActionWatchdogSeconds)
                {
                    if (!finished)
                    {
                        Debug.LogWarning($"[ActionScheduler] {_acting.UnitName}의 행동이 {ActionWatchdogSeconds}초를 넘겨 강제 종료합니다.");
                        _acting.isCasting = false;
                    }

                    _acting = null;
                    _actingElapsed = 0f;
                }

                return;
            }

            // 2) 궁극기가 예약돼 있으면 일반행동보다 먼저 실행한다.
            if (TryStartReservedUltimate()) return;

            // 3) 새로 조건을 만족한 궁극기를 예약한다.
            ReserveReadyUltimates();
            if (TryStartReservedUltimate()) return;

            // 4) 아무도 행동하지 않을 때만 AV가 흐른다.
            AdvanceActionValues(deltaTime);
            TryStartNormalAction();
        }

        /// <summary>전투에 참여 중인 유닛(필드 위 · 활성 · 벤치 제외).</summary>
        private static IEnumerable<Unit> Participants()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return Enumerable.Empty<Unit>();

            return grid.heroList.Concat(grid.enemyList)
                .Where(unit => unit != null
                               && unit.isActive
                               && unit.currentCell != null
                               && unit.currentCell.yPos > 0);
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
            foreach (Unit unit in stale)
            {
                _actionValues.Remove(unit);
                _ultimateQueue.Remove(unit);
            }

            // 행동 중이던 유닛이 쓰러졌다면 붙잡고 있지 않는다(Tick의 완료 판정과 이중 안전장치).
            if (_acting != null && !_acting.isActive) _acting = null;
        }

        private void AdvanceActionValues(float deltaTime)
        {
            float step = deltaTime * DrainRate;
            foreach (Unit unit in _actionValues.Keys.ToList())
            {
                _actionValues[unit] = Mathf.Max(0f, _actionValues[unit] - step);
            }
        }

        /// <summary>마나가 찬 유닛의 궁극기를 예약한다. 속도와 무관하게 즉시 줄을 선다.</summary>
        private void ReserveReadyUltimates()
        {
            foreach (Unit unit in _actionValues.Keys)
            {
                if (_ultimateQueue.Contains(unit)) continue;
                if (!CanUseUltimate(unit)) continue;
                _ultimateQueue.Add(unit);
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

        private bool TryStartReservedUltimate()
        {
            while (_ultimateQueue.Count > 0)
            {
                Unit unit = _ultimateQueue[0];
                _ultimateQueue.RemoveAt(0);

                // 예약 후 상황이 바뀌었을 수 있으므로 실행 직전에 다시 확인한다.
                if (!CanUseUltimate(unit)) continue;

                Execute(unit, ultimate: true);
                return true;
            }

            return false;
        }

        private void TryStartNormalAction()
        {
            Unit next = null;
            float best = float.MaxValue;

            foreach (KeyValuePair<Unit, float> pair in _actionValues)
            {
                if (pair.Value > 0f) continue;
                if (!CanAct(pair.Key)) continue;

                // 동시에 0이 된 경우 속도가 빠른 쪽이 먼저 행동한다.
                float tie = -SpeedOf(pair.Key);
                if (tie >= best) continue;
                best = tie;
                next = pair.Key;
            }

            if (next == null) return;

            // 행동을 소비했으므로 AV를 다시 채운다.
            _actionValues[next] = FullActionValue(next);
            Execute(next, ultimate: false);
        }

        private static bool CanAct(Unit unit)
        {
            return unit != null
                   && unit.isActive
                   && !unit.isControlled
                   && !unit.isCasting
                   && unit.ActiveNormalCode != null
                   && unit.ActiveNormalCode.HasValidTarget();
        }

        private void Execute(Unit unit, bool ultimate)
        {
            _actingElapsed = 0f;

            if (ultimate) unit.CastUltimateCode();
            else unit.CastNormalCode();

            // 코드에 따라서는 코루틴 없이 즉시 끝난다(예: 자원만 쌓는 궁극기).
            // 그런 경우 isCasting이 서지 않으므로 붙잡지 않고 바로 다음으로 넘어간다.
            _acting = unit.isCasting ? unit : null;
        }

        /// <summary>
        /// 앞으로의 행동 순서를 예측한다. 좌상단 행동서열 UI가 쓴다.
        /// 실제 진행을 건드리지 않고 사본으로만 시뮬레이션한다.
        /// </summary>
        public List<Reservation> ForecastOrder(int count)
        {
            var result = new List<Reservation>();
            if (count <= 0) return result;

            // 예약된 궁극기가 가장 먼저다.
            foreach (Unit unit in _ultimateQueue)
            {
                if (result.Count >= count) return result;
                result.Add(new Reservation { Unit = unit, IsUltimate = true, EtaSeconds = 0f });
            }

            // 현재 행동 중인 유닛도 맨 앞에 보여준다.
            if (_acting != null && result.Count < count)
            {
                result.Insert(0, new Reservation { Unit = _acting, IsUltimate = false, EtaSeconds = 0f });
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
                    if (pair.Value >= lowest) continue;
                    lowest = pair.Value;
                    next = pair.Key;
                }

                if (next == null) break;

                elapsed += lowest / DrainRate;
                foreach (Unit unit in simulated.Keys.ToList())
                {
                    simulated[unit] -= lowest;
                }

                result.Add(new Reservation { Unit = next, IsUltimate = false, EtaSeconds = elapsed });
                simulated[next] = FullActionValue(next);
            }

            return result;
        }
    }
}
