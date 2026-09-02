using System;
using BaseClasses;
using Codes.Base;
using UnityEngine;

namespace Codes.Passive
{
    public class Block : PassiveCode
    {
        private const float TickInterval = 4f;

        private bool _isRegistered;
        private float _elapsed;
        private Action<EventContext> _turnHandler;
        private Action<EventContext> _roundEndHandler;

        public Block(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "막기";
            MaxStage = 3;
            CodeTags = new System.Collections.Generic.List<int> { DamageTag.Physical };
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _turnHandler = OnOwnerTurnStart;
            _roundEndHandler = OnRoundEnd;
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            _isRegistered = true;
            _elapsed = 0f;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_turnHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            }

            if (_roundEndHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            }

            _turnHandler = null;
            _roundEndHandler = null;
            _isRegistered = false;
            _elapsed = 0f;
        }

        private void OnOwnerTurnStart(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            _elapsed += Caster.LastTurnSeconds;
            if (_elapsed < TickInterval) return;

            _elapsed -= TickInterval;
            if (UnityEngine.Random.value > GetStageChance()) return;

            int shieldAmount = Caster.SkillDamage(40, BaseEnums.PrimaryStat.CON) + Mathf.RoundToInt(GetStageShieldBonus());
            Caster.AddShield(shieldAmount, Caster);
            Debug.Log($"[막기] {Caster.UnitName}이 {shieldAmount} 방어막 획득");
        }

        private void OnRoundEnd(EventContext context)
        {
            if (context.Grantee == Caster)
            {
                _elapsed = 0f;
            }
        }

        private float GetStageChance()
        {
            return Mathf.Clamp(CurrentStage, 1, MaxStage) switch
            {
                1 => 0.05f,
                2 => 0.15f,
                _ => 0.25f,
            };
        }

        private int GetStageShieldBonus()
        {
            return Mathf.Clamp(CurrentStage, 1, MaxStage) switch
            {
                1 => 100,
                2 => 200,
                _ => 300,
            };
        }
    }
}
