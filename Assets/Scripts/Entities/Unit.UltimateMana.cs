using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive.Reward;
using UnityEngine;

namespace Entities
{
    public partial class Unit
    {
        private readonly HashSet<int> _initialManaSources = new();
        private int _storedUltimateMana, _originalUltimateCount, _pendingUltimateCopies;
        private bool _ultimateCommitted, _ultimateNotified;
        public int StoredUltimateMana => _storedUltimateMana;
        public int OriginalUltimateCount => _originalUltimateCount;
        public bool IsUltimateReplay { get; private set; }
        public bool HasPendingUltimateCopy => _pendingUltimateCopies > 0;
        private bool HasManaReward(int id) => ActivePassiveCodesResolved().OfType<UltimateManaRewardCode>().Any(x => x.Id == id);

        public void GrantInitialMana(int sourceId, int amount)
        {
            if (UltimateResourceType != BaseEnums.UltimateResourceType.Mana || !IsOnField || !_initialManaSources.Add(sourceId)) return;
            AddUltimateResource(amount);
        }

        private void ResetBattleUltimateState()
        {
            _initialManaSources.Clear();
            _storedUltimateMana = _originalUltimateCount = _pendingUltimateCopies = 0;
            IsUltimateReplay = _ultimateCommitted = _ultimateNotified = false;
        }

        public void ClearManaRewardState(int id)
        {
            if (id == UltimateManaRewardCode.Overflow) _storedUltimateMana = 0;
            if (id == UltimateManaRewardCode.Double) { _pendingUltimateCopies = 0; _originalUltimateCount = 0; }
            RefreshView();
        }

        public void CastUltimateReplay()
        {
            if (!HasPendingUltimateCopy || !isActive || !IsOnField || isControlled || isCasting || UltimateCode == null || !UltimateCode.HasValidTarget()) return;
            _pendingUltimateCopies--;
            BeginUltimateCast(true);
        }

        private void BeginUltimateCast(bool replay)
        {
            IsUltimateReplay = replay;
            _ultimateCommitted = _ultimateNotified = false;
            bool deferred = UltimateCode.DefersActivationUntilImpact;
            if (!deferred) ConsumeUltimateActivationResource();
            UltimateCode.CastCode();
            if (!deferred) NotifyUltimateActivation();
        }

        /// <summary>완료/취소 모두 자원 이동을 한 번만 처리한다. 무료 복제는 소비·저장분 이동·자기 카운트가 없다.</summary>
        internal void FinishUltimateAction()
        {
            if (!IsUltimateReplay && _ultimateCommitted && isActive && IsOnField)
            {
                int stored = _storedUltimateMana;
                _storedUltimateMana = 0;
                if (stored > 0) AddUltimateResource(stored);
            }
            if (!isActive || !IsOnField || isControlled || (UltimateCode is BaseUltimateCode body && !body.CompletedNormally))
                _pendingUltimateCopies = 0;
            IsUltimateReplay = _ultimateCommitted = false;
        }
    }
}
