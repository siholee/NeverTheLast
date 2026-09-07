using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;

namespace Codes.Passive
{
    /// <summary>여러 유닛이 돌려 쓰는 능력치 해금 패시브의 상태 ID 대역.</summary>
    public static class SharedStatStatusIds
    {
        public const int Giant = 5960;
        public const int IronWall = 5961;
        public const int Curse = 5962;
    }

    /// <summary>
    /// 공용 해금 패시브 자리에 붙는, 전투 내내 유지되는 상태 하나짜리 패시브의 뼈대.
    /// 사망·라운드 종료에 스스로 상태를 걷는다.
    /// </summary>
    public abstract class PersistentStatusPassive : PassiveCode
    {
        private readonly int _statusId;
        private readonly string _statusKey;
        private readonly string _description;

        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        protected PersistentStatusPassive(
            PassiveCodeContext context, int statusId, string statusKey, string name, string description)
            : base(context)
        {
            _statusId = statusId;
            _statusKey = statusKey;
            _description = description;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = name;
            IgnoresActivationChance = true;
        }

        protected int StatusId => _statusId;
        protected string StatusKey => _statusKey;
        protected string Description => _description;
        protected bool Registered => _registered;

        /// <summary>전투 시작 시 곧바로 붙일 효과. 행동에 반응해 붙는 패시브는 null을 돌려준다.</summary>
        protected abstract BaseEffect CreateInitialEffect();

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            Caster.RemoveStatusByKey(_statusKey);
            BaseEffect effect = CreateInitialEffect();
            if (effect != null)
            {
                Caster.AddStatus(BuffStatus.Create(
                    _statusId, _statusKey, CodeName, Caster, Caster, effect,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: _description));
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            OnRegistered();
            _registered = true;
        }

        protected virtual void OnRegistered() { }
        protected virtual void OnUnregistered() { }

        public override void StopCode()
        {
            if (Caster == null) return;
            if (_registered)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
                OnUnregistered();
            }

            Caster.RemoveStatusByKey(_statusKey);
            _registered = false;
        }
    }

    /// <summary>공용 해금 패시브 39 거인 — STR +5%.</summary>
    public sealed class GiantPassive : PersistentStatusPassive
    {
        public const float Multiplier = 1.05f;

        public GiantPassive(PassiveCodeContext context)
            : base(context, SharedStatStatusIds.Giant, "giant", "거인", "STR +5%") { }

        protected override BaseEffect CreateInitialEffect() => new GiantEffect();
    }

    internal sealed class GiantEffect : BaseEffect
    {
        public GiantEffect() : base(0, GiantPassive.Multiplier) { }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR ? GiantPassive.Multiplier : 1f;
    }

    /// <summary>
    /// 공용 해금 패시브 6 철벽 — 일반행동을 해결할 때마다 해당 전투 동안 CON +2가 쌓인다.
    ///
    /// 중첩은 상태를 여러 개 붙이는 대신 <b>가산치를 키운 상태 하나</b>로 표현한다.
    /// 긴 전투에서 상태 목록이 수십 개로 불어나면 스탯 질의가 매번 그만큼 길어지기 때문이다.
    ///
    /// <b>중첩에 상한은 없다.</b> CON은 최대 체력만 늘릴 뿐 행동 속도를 바꾸지 않으므로
    /// "빨라져서 더 빨리 쌓는" 되먹임이 없다. 오래 버틴 만큼 단단해지는 것이 이 코드의 값이다.
    /// </summary>
    public sealed class IronWallPassive : PersistentStatusPassive
    {
        public const int ConPerAction = 2;

        private Action<EventContext> _actionHandler;
        private int _stacks;

        public IronWallPassive(PassiveCodeContext context)
            : base(context, SharedStatStatusIds.IronWall, "iron_wall", "철벽",
                $"일반행동을 할 때마다 이번 전투 동안 CON +{ConPerAction}. 중첩된다") { }

        // 행동에 반응해 처음 붙는다. 전투 시작 시점에는 아직 아무 효과도 없다.
        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _stacks = 0;
            _actionHandler = OnNormalActionResolved;
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            _stacks = 0;
        }

        private void OnNormalActionResolved(EventContext context)
        {
            if (Caster == null || !Registered || context?.Grantee != Caster || !Caster.isActive) return;

            _stacks++;
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster,
                new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.CON, ConPerAction * _stacks),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"{Description} (현재 +{ConPerAction * _stacks})"));
        }
    }

    /// <summary>
    /// 공용 해금 패시브 1016 저주 — <b>자신이</b> 부여하는 지속피해량 +20%.
    ///
    /// 원래는 츠쿠요미의 코드로 만들어졌지만 <b>정작 츠쿠요미는 이 코드를 갖지 않는다</b>
    /// (해금은 34·35·36·37·38뿐). 실제로는 독수리 전사·명계의 사령·공허의 괴조 같은
    /// 적 11종만 쓰고 있어, 특정 캐릭터에 묶지 않는 범용 코드로 옮겼다.
    ///
    /// 야마의 <c>죽음의 계약</c>(261)이 같은 계열의 <b>금 등급</b>이며 그쪽은 아군 전체에 건다.
    /// 둘을 같이 들면 <see cref="PassiveCode.SupersededByCodeId"/>가 이 코드를 재운다.
    /// 서로 다른 유닛이 하나씩 들었을 때는 같은 상태 키를 써서 높은 배율만 남는다.
    /// </summary>
    public sealed class CursePassive : PersistentStatusPassive
    {
        public const int CodeId = 1016;
        private const float Multiplier = 1.20f;

        public CursePassive(PassiveCodeContext context)
            : base(context, SharedStatStatusIds.Curse, YamaDeathContract.SharedKey, "저주",
                "부여하는 지속피해량이 20% 증가합니다.")
        {
            SupersededByCodeId = YamaDeathContract.CodeId;
        }

        protected override BaseEffect CreateInitialEffect() => new DotAmplifyEffect(Multiplier);
    }

}
