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

        /// <summary>아군에게 나눠 주는 찬드라 CON의 비율.</summary>
        private const float ShareRatio = 0.10f;

        /// <summary>
        /// 전투가 열릴 때 자신의 CON 10%를 아군 전체에게 나눠 준다.
        ///
        /// 예전에는 자기 INT를 CON으로 환산해 혼자 두꺼워졌다. 지금은 그 두께를 편성 전체로
        /// 흘려 보낸다 — 찬드라를 키울수록 파티 전원의 체력이 함께 오르는 구조다.
        /// 값은 <b>시전 시점의 CON</b>으로 굳힌다. 매 프레임 다시 재면 자기 자신도 대상이라
        /// CON이 CON을 부풀리는 되먹임이 생긴다.
        /// </summary>
        public override void CastCode()
        {
            if (Caster == null) return;

            int share = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ShareRatio));
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    BuffStatusIds.Nishakara, "ChandraNishakara", "니샤카라",
                    Caster, ally, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.CON, share),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"찬드라가 나눈 CON +{share}"));
            }
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in Combat.CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.RemoveStatusByKey("ChandraNishakara");
            }
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


    /// <summary>
    /// Lv.10 카피바라 — 우정도 획득 +25%.
    ///
    /// 훈련 강화량이 아니라 <b>쌓이는 속도</b>를 건드린다. 우정 훈련(75)에 더 빨리 닿게 하는
    /// 코드라, 런이 길수록 값이 커진다. <c>TrainingManager</c>가 필드에서 읽는다.
    /// </summary>
    public sealed class ChandraCapybara : PassiveCode
    {
        public const float BondBonus = 0.25f;

        public ChandraCapybara(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "카피바라";
            IgnoresActivationChance = true;
            SupersededByCodeId = 139;
        }
    }

    /// <summary>
    /// Lv.27 봉우 — 우정 훈련이 발동하면 그 보너스가 +50%.
    ///
    /// 카피바라가 우정 훈련에 <b>닿는 속도</b>를 올린다면, 봉우는 닿은 뒤의 <b>한 번의 크기</b>를
    /// 올린다. 둘을 함께 들면 앞뒤가 맞물린다.
    /// </summary>
    public sealed class ChandraSwornFriend : PassiveCode
    {
        public const float FriendshipBonus = 0.50f;

        public ChandraSwornFriend(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "봉우";
            IgnoresActivationChance = true;
            // 미트라(113)가 금색 상위다. 둘을 함께 들면 이쪽은 발동하지 않는다.
            SupersededByCodeId = SuryaCodeIds.Mitra;
        }
    }
}
