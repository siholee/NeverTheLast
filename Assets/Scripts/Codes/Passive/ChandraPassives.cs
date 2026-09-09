using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public class ChandraNishakara : UniquePassiveCode
    {
        public ChandraNishakara(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "니샤카라";
            MaxStage = 3;
            Transferable = false;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatus(BuffStatus.Create(
                BuffStatusIds.Nishakara, "ChandraNishakara", "니샤카라",
                Caster, Caster, new NishakaraBuffEffect(GetRatio())));
        }

        public override void StopCode()
        {
            Caster.RemoveStatusByKey("ChandraNishakara");
        }

        private float GetRatio()
        {
            return Mathf.Clamp(CurrentStage, 1, MaxStage) switch
            {
                1 => 0.25f,
                2 => 0.5f,
                _ => 1f,
            };
        }
    }

    public class ChandraBastion : PassiveCode
    {
        public ChandraBastion(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "성채";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.AddStatus(BuffStatus.Create(
                BuffStatusIds.Bastion, "ChandraBastion", "성채",
                Caster, Caster, new ShieldBonusBuffEffect(0.25f)));
        }
    }

    /// <summary>
    /// 기사회생(2)과 그 금색 상위 코드 완벽한 재기(103).
    ///
    /// 전투가 턴제로 바뀌면서 <b>초 단위 코루틴을 상태로 옮겼다.</b> 예전에는 5초에 걸쳐
    /// 30%를 회복했는데, 벽시계 시간은 행동 순서와 무관해 빠른 유닛일수록 회복이 늦게
    /// 끝나는 것처럼 보였다. 이제 자기 턴마다 정해진 비율씩 회복한다.
    ///
    /// 발동은 <b>전투당 1회</b>다. 재발동을 허용하면 회복 총량이 최대 체력을 넘겨 버린다.
    /// </summary>
    public class ChandraSecondWind : PassiveCode
    {
        /// <summary>회복이 끝나기까지의 턴 수. 두 등급이 같은 길이를 쓴다.</summary>
        public const int RecoveryTurns = 3;

        private const int StatusId = 5970;
        private const string StatusKey = "second_wind_recovery";

        private readonly float _threshold;
        private readonly float _totalRatio;

        private bool _isRegistered;
        private bool _triggered;
        private Action<EventContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public ChandraSecondWind(PassiveCodeContext context) : this(
            context, "기사회생", 0.25f, 0.60f, PerfectRecoveryCodeId, BaseEnums.CodeGrade.Normal) { }

        /// <summary>완벽한 재기(103)의 코드 ID. 기사회생은 이 코드가 있으면 잠든다.</summary>
        public const int PerfectRecoveryCodeId = 103;

        protected ChandraSecondWind(PassiveCodeContext context, string name,
            float threshold, float totalRatio, int supersededByCodeId, BaseEnums.CodeGrade grade)
            : base(context)
        {
            _threshold = threshold;
            _totalRatio = totalRatio;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
            Grade = grade;
            SupersededByCodeId = supersededByCodeId;
        }

        public override void CastCode()
        {
            _triggered = false;
            if (_isRegistered) return;

            _damageHandler = OnAfterDamageTaken;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            Caster.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _damageHandler = null;
            _cleanupHandler = null;
            _isRegistered = false;
            _triggered = false;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            if (_triggered || context.Grantee != Caster || !Caster.isActive || Caster.HpMax <= 0) return;
            if ((float)Caster.HpCurr / Caster.HpMax >= _threshold) return;

            _triggered = true;
            float perTurn = _totalRatio * 100f / RecoveryTurns;
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster,
                new PercentHealOverTimeEffect(perTurn),
                duration: RecoveryTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"{RecoveryTurns}턴 동안 자기 턴마다 최대 체력의 {perTurn:0.#}%를 회복합니다."));
        }
    }

    /// <summary>완벽한 재기(103) — 기사회생의 금색 상위 코드. 30% 미만에서 3턴에 걸쳐 전부 회복한다.</summary>
    public sealed class PerfectRecovery : ChandraSecondWind
    {
        public PerfectRecovery(PassiveCodeContext context)
            : base(context, "완벽한 재기", 0.30f, 1.00f, 0, BaseEnums.CodeGrade.Enhanced) { }
    }

    public class ChandraBulwark : PassiveCode
    {
        private bool _isRegistered;
        private Action<EventContext> _beneficialHandler;

        public ChandraBulwark(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "보루";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _beneficialHandler = OnBeneficialEffectReceived;
            Caster.AddListener(BaseEnums.UnitEventType.OnBeneficialEffectReceived, _beneficialHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_beneficialHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnBeneficialEffectReceived, _beneficialHandler);
            }

            _beneficialHandler = null;
            _isRegistered = false;
        }

        private void OnBeneficialEffectReceived(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            // Replace 정책: 재발동 시 지속 턴을 처음부터 다시 센다
            Caster.AddStatus(BuffStatus.Create(
                BuffStatusIds.BulwarkStr, "ChandraBulwarkStr", "보루",
                Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.STR, 4),
                duration: 2));
        }
    }

    public class ChandraLastStandFormation : PassiveCode
    {
        public ChandraLastStandFormation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "배수의 진";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster.RemoveStatusByKey("ChandraLastStandFormation");
            if (!HasOtherFrontAlly())
            {
                Caster.AddStatus(BuffStatus.Create(
                    BuffStatusIds.LastStandFormation, "ChandraLastStandFormation", "배수의 진",
                    Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.CON, 12)));
            }
        }

        private bool HasOtherFrontAlly()
        {
            if (GridManager.Instance == null || Caster.currentCell == null) return false;

            int frontColumn = GridManager.Instance.GetFrontColumn(Caster.IsEnemy);
            return Target.GetAllAllies(Caster).Any(ally =>
                ally != null &&
                ally != Caster &&
                ally.isActive &&
                ally.currentCell != null &&
                ally.currentCell.xPos == frontColumn);
        }
    }

    public class ChandraPurificationBath : PassiveCode
    {
        private bool _isRegistered;
        private Action<EventContext> _roundEndHandler;

        public ChandraPurificationBath(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "목욕제계";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _roundEndHandler = OnRoundEnd;
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_roundEndHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _roundEndHandler);
            }

            _roundEndHandler = null;
            _isRegistered = false;
        }

        private void OnRoundEnd(EventContext context)
        {
            if (context.Grantee != Caster || !Caster.isActive) return;

            float healRatio = Mathf.Max(0f, Caster.GetBaseCon()) * 0.01f;
            foreach (Unit ally in Target.GetAllAllies(Caster))
            {
                if (ally == null || !ally.isActive) continue;

                int healAmount = Mathf.RoundToInt(ally.HpMax * healRatio);
                ally.ModifyHp(ally.HpCurr + healAmount, Caster);
            }
        }
    }

    public class ChandraMoonlight : PassiveCode
    {
        private const int ImbueDuration = 2;

        private bool _isRegistered;
        private Action<EventContext> _beneficialGrantedHandler;

        public ChandraMoonlight(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "월광";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (_isRegistered) return;

            _beneficialGrantedHandler = OnBeneficialEffectGranted;
            Caster.AddListener(BaseEnums.UnitEventType.OnBeneficialEffectGranted, _beneficialGrantedHandler);
            _isRegistered = true;
        }

        public override void StopCode()
        {
            if (!_isRegistered) return;

            if (_beneficialGrantedHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnBeneficialEffectGranted, _beneficialGrantedHandler);
            }

            _beneficialGrantedHandler = null;
            _isRegistered = false;
        }

        private void OnBeneficialEffectGranted(EventContext context)
        {
            Unit target = context.Grantor;
            if (context.Grantee != Caster || target == null || !target.isActive) return;
            if (!Target.GetAllAllies(Caster).Contains(target)) return;

            // 바람 부여 마커 상태 (효과 객체 없음). Replace 정책으로 재부여 시 지속시간 갱신.
            string statusKey = $"AnemoImbue_{Caster.GetEntityId()}";
            target.AddStatus(BuffStatus.Create(
                BuffStatusIds.AnemoImbue, statusKey, "월광",
                Caster, target, null,
                duration: ImbueDuration));
        }
    }
}
