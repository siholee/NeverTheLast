using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Base;
using Effects.Vfx;
using Entities.Status;
using UnityEngine;

namespace Entities
{
    /// <summary>
    /// 유닛의 상태(버프/디버프) 컨테이너.
    /// 중첩 정책 처리, 만료 틱, 효과 객체 열거를 담당한다.
    /// Unit이 소유하며, 상태 변화 시 콜백(onChanged)으로 스탯 재계산을 요청한다.
    /// </summary>
    [Serializable]
    public class UnitStatusController
    {
        private Unit _owner;
        private Action _onChanged;              // 상태 추가/제거 시 (AttributesUpdate + InfoTab 갱신)
        private Action<Unit> _notifyBeneficial; // 이로운 상태 부여 시 (시전자 전달)
        private Action<Unit, UnitStatus> _notifyNegative; // 해로운 상태 적용/갱신 시

        private readonly List<UnitStatus> _statuses = new();

        // 상태가 붙고 떨어진 사실은 바깥 호출이 끝난 뒤에 알린다. 알림을 받은 코드(야마의 선고)가 같은 목록을 고쳐도
        // TickTurn · RemoveByKey의 역순 순회 인덱스가 어긋나지 않게 하려는 것이다.
        private int _batchDepth;
        private readonly List<(UnitStatus Status, bool Added)> _pendingChanges = new();

        public void Initialize(
            Unit owner,
            Action onChanged,
            Action<Unit> notifyBeneficial,
            Action<Unit, UnitStatus> notifyNegative)
        {
            _owner = owner;
            _onChanged = onChanged;
            _notifyBeneficial = notifyBeneficial;
            _notifyNegative = notifyNegative;
            _statuses.Clear();
        }

        /// <summary>현재 보유 중인 모든 상태의 효과 객체 열거 (스탯 질의 훅 집계용)</summary>
        public IEnumerable<BaseEffect> ActiveEffectObjects()
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                var effects = _statuses[i].Effects;
                for (int j = 0; j < effects.Count; j++)
                {
                    if (effects[j].EffectObject != null)
                    {
                        yield return effects[j].EffectObject;
                    }
                }
            }
        }

        private void BeginBatch() => _batchDepth++;

        private void EndBatch()
        {
            if (--_batchDepth > 0) return;
            while (_pendingChanges.Count > 0)
            {
                var changes = _pendingChanges.ToArray();
                _pendingChanges.Clear();
                foreach (var change in changes) Unit.NotifyStatusChanged(_owner, change.Status, change.Added);
            }
        }

        /// <summary>상태 추가 - 중첩 정책에 따라 처리 (동일 여부 판정은 Key 기준)</summary>
        public void Add(UnitStatus status)
        {
            BeginBatch();
            try { AddCore(status); }
            finally { EndBatch(); }
        }

        private void AddCore(UnitStatus status)
        {
            if (status == null) return;
            status.ApplicationSucceeded = false;
            status.Owner = _owner;
            PrepareEffects(status);
            bool hostile = Combat.DamageMitigation.IsHostileEffect(status.Caster, _owner);
            // 중립 표식과 ID 기반 지속피해도 효과 명중을 받는다. 같은 진영의 부여는 두 저항 모두 건너뛴다.
            bool harmful = status.CountsAsDebuff || (!status.IsBeneficial && status.Category != BaseEnums.StatusCategory.Positive);
            // 적이 거는 해로운 상태는 특수공격력 대 특수방어력으로 명중을 겨룬다(효과 회피).
            // 이미 자기 확률로 굴린 쪽(화상 등)은 SkipEffectHitRoll을 켜서 두 번 굴리지 않는다.
            if (hostile && harmful && !status.SkipEffectHitRoll &&
                !Combat.DamageMitigation.RollEffectHit(status.Caster, _owner))
            {
                Debug.Log($"[효과 저항] {_owner.UnitName}이(가) '{status.StatusName}'을(를) 저항했습니다.");
                Effects.CombatFeedback.PlayEffectResisted(_owner);
                return;
            }
            if (hostile && harmful && !status.SkipStatusResistanceRoll &&
                _owner.ResistsNegativeStatus(status))
            {
                Debug.Log($"[상태 저항] {_owner.UnitName}이(가) '{status.StatusName}' 상태를 저항했습니다.");
                Effects.CombatFeedback.PlayEffectResisted(_owner);
                return;
            }

            // 시전자 효과가 자기가 거는 상태의 지속을 늘린다(죽음의 계약 — 지속피해 +1턴). 무한(0 이하)은 건드리지 않는다.
            if (status.Duration > 0 && status.Caster != null)
            {
                int bonus = status.Caster.CombatEffects().Sum(effect => effect.OutgoingStatusDurationModifier(status.Caster, status));
                if (bonus != 0) status.Duration = Mathf.Max(1, status.Duration + bonus);
            }

            switch (status.StackPolicy)
            {
                case BaseEnums.StatusStackPolicy.Stack:
                    // 중첩 허용 - 그대로 추가
                    AddInternal(status);
                    Debug.Log($"[Status-Stack] {_owner.UnitName}에게 {status.StatusName} 추가 (중첩, 총 {_statuses.Count}개)");
                    break;

                case BaseEnums.StatusStackPolicy.ExtendDuration:
                    // 지속시간 연장 - 기존 것 찾아서 시간 추가
                    var existing = _statuses.FirstOrDefault(s => s.Key == status.Key);
                    if (existing != null)
                    {
                        int oldRemaining = existing.RemainingTurns;
                        existing.Duration = existing.ElapsedTurns + oldRemaining + status.Duration;
                        status.ApplicationSucceeded = true;
                        // 들어온 쪽은 아직 효과 객체가 없을 수 있다. 지속피해 판정은 이미 붙어 있는 쪽으로 한다.
                        if (status.CountsAsDebuff || existing.CountsAsDebuff)
                        {
                            _notifyNegative?.Invoke(status.Caster, status);
                        }
                        Debug.Log($"[Status-Extend] {_owner.UnitName}의 {status.StatusName} 지속 연장: {oldRemaining}턴 → {existing.RemainingTurns}턴");
                    }
                    else
                    {
                        AddInternal(status);
                        Debug.Log($"[Status-Extend] {_owner.UnitName}에게 {status.StatusName} 최초 적용");
                    }
                    break;

                case BaseEnums.StatusStackPolicy.ReplaceIfStronger:
                    // 더 강한 것으로 교체 - Coefficient 비교
                    var existingStrong = _statuses.FirstOrDefault(s => s.Key == status.Key);
                    if (existingStrong != null)
                    {
                        float newAvgCoeff = status.Effects.Count > 0
                            ? status.Effects.Average(e => e.Coefficient)
                            : 0f;
                        float existingAvgCoeff = existingStrong.Effects.Count > 0
                            ? existingStrong.Effects.Average(e => e.Coefficient)
                            : 0f;

                        if (newAvgCoeff > existingAvgCoeff)
                        {
                            RemoveAt(_statuses.IndexOf(existingStrong));
                            AddInternal(status);
                            Debug.Log($"[Status-Replace] {_owner.UnitName}의 {status.StatusName} 교체 (계수: {existingAvgCoeff:F1} → {newAvgCoeff:F1})");
                        }
                        else
                        {
                            Debug.Log($"[Status-Replace] {_owner.UnitName}의 기존 {status.StatusName}이 더 강함 - 무시");
                        }
                    }
                    else
                    {
                        AddInternal(status);
                        Debug.Log($"[Status-Replace] {_owner.UnitName}에게 {status.StatusName} 최초 적용");
                    }
                    break;

                case BaseEnums.StatusStackPolicy.Replace:
                    // 무조건 교체 - 지속시간/수치 갱신
                    int replaceIndex = _statuses.FindIndex(s => s.Key == status.Key);
                    if (replaceIndex >= 0)
                    {
                        RemoveAt(replaceIndex);
                    }
                    AddInternal(status);
                    break;

                case BaseEnums.StatusStackPolicy.Ignore:
                    // 중복 무시 - 기존 것 있으면 추가 안 함
                    if (_statuses.Any(s => s.Key == status.Key))
                    {
                        Debug.Log($"[Status-Ignore] {_owner.UnitName}에게 이미 {status.StatusName} 존재 - 무시");
                    }
                    else
                    {
                        AddInternal(status);
                        Debug.Log($"[Status-Ignore] {_owner.UnitName}에게 {status.StatusName} 최초 적용");
                    }
                    break;
            }
        }

        private void PrepareEffects(UnitStatus status)
        {
            // Effect 객체들 생성 및 할당 (직접 생성된 EffectObject는 Caster/Target만 보정)
            foreach (var effectInstance in status.Effects)
            {
                if (effectInstance.EffectObject == null)
                {
                    effectInstance.EffectObject = EffectFactory.CreateEffect(
                        effectInstance.EffectId,
                        effectInstance.Coefficient,
                        status.Caster,
                        _owner
                    );
                }
                else
                {
                    effectInstance.EffectObject.Caster ??= status.Caster;
                    effectInstance.EffectObject.Target ??= _owner;
                }
            }
        }

        private void AddInternal(UnitStatus status)
        {
            status.ApplicationSucceeded = true;
            _statuses.Add(status);
            _pendingChanges.Add((status, true));
            status.OnApply();
            // 스탯 질의 훅을 가진 효과가 즉시 반영되도록 스탯 캐시 갱신
            _onChanged?.Invoke();

            if (status.IsBeneficial)
            {
                _notifyBeneficial?.Invoke(status.Caster);
            }
            if (status.CountsAsDebuff)
            {
                _notifyNegative?.Invoke(status.Caster, status);
            }
            StatusAffectedVfx.Play(_owner, status);
        }

        private void RemoveAt(int index)
        {
            if (index >= 0 && index < _statuses.Count)
            {
                var status = _statuses[index];
                status.OnRemove();
                _statuses.RemoveAt(index);
                _pendingChanges.Add((status, false));
                // 스탯 질의 훅 해제 반영
                _onChanged?.Invoke();

                Debug.Log($"[Status] {_owner.UnitName}의 {status.StatusName} 제거 (남은 상태: {_statuses.Count}개)");
            }
        }

        /// <summary>상태 ID로 상태 제거 (가장 오래된 것 하나만 제거)</summary>
        public void Remove(int statusId)
        {
            BeginBatch();
            try
            {
                for (int i = 0; i < _statuses.Count; i++)
                {
                    if (_statuses[i].StatusId == statusId)
                    {
                        RemoveAt(i);
                        return;
                    }
                }
            }
            finally { EndBatch(); }
        }

        /// <summary>같은 숫자 ID에 다른 키가 공존해도 선택한 상태만 정화한다.</summary>
        internal void RemoveInstance(UnitStatus status)
        {
            BeginBatch();
            try { RemoveAt(_statuses.IndexOf(status)); }
            finally { EndBatch(); }
        }

        /// <summary>키로 상태 제거 (일치하는 모든 상태 제거)</summary>
        public void RemoveByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            BeginBatch();
            try
            {
                for (int i = _statuses.Count - 1; i >= 0; i--)
                {
                    if (_statuses[i].Key == key)
                    {
                        RemoveAt(i);
                    }
                }
            }
            finally { EndBatch(); }
        }

        public bool Has(int statusId) => _statuses.Any(s => s.StatusId == statusId);

        public bool HasKey(string key) => !string.IsNullOrEmpty(key) && _statuses.Any(s => s.Key == key);

        public UnitStatus Get(int statusId) => _statuses.FirstOrDefault(s => s.StatusId == statusId);

        public List<UnitStatus> GetAll(int statusId) => _statuses.Where(s => s.StatusId == statusId).ToList();

        public List<UnitStatus> GetAll() => new(_statuses);

        /// <summary>내부 리스트 직접 반환 (UI 표시용)</summary>
        public List<UnitStatus> GetLive() => _statuses;

        /// <summary>
        /// 보유자의 턴 시작 틱: 효과 진행 + 만료 상태 제거.
        /// 전투가 턴제이므로 프레임이 아니라 턴 경계에서만 돈다.
        /// </summary>
        public void TickTurn()
        {
            BeginBatch();
            try
            {
                // 틱 도중 상태가 추가·제거될 수 있으므로 사본으로 순회한다.
                foreach (UnitStatus status in _statuses.ToList())
                {
                    status.OnOwnerTurn();
                }

                for (int i = _statuses.Count - 1; i >= 0; i--)
                {
                    if (_statuses[i].IsExpired()) RemoveAt(i);
                }
            }
            finally { EndBatch(); }
        }

        /// <summary>모든 상태 정리 (라운드 종료 시 — OnRemove 호출로 이벤트 리스너 등 해제)</summary>
        public void ClearAll()
        {
            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                _statuses[i].OnRemove();
            }
            _statuses.Clear();
            _pendingChanges.Clear();
        }
    }
}
