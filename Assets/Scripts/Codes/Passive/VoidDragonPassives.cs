using System;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class VoidDragonCodeIds
    {
        public const int VoidOrigin = 1530;
        public const int Aether = 1531;
        public const int PerfectResurrection = 1532;
        public const int ElementalMastery = 1533;
    }

    public static class VoidDragonStatusIds
    {
        public const int VoidOrigin = 7970;
        public const int Aether = 7971;
        public const int PerfectResurrection = 7972;
        public const int ElementalMastery = 7973;
        public const int ResurrectionWard = 7974;
    }

    /// <summary>
    /// 공허의 용 P 공허의 근원 — 필드의 공허 괴수 아군 하나당 자신의 올스탯 +3%.
    /// 자신이 쓰러지면 남은 공허 괴수 전원이 최대 체력의 50%를 회복한다.
    ///
    /// 보스를 먼저 잡을수록 강해지는 호위가 남고, 호위를 먼저 잡으면 보스가 약해지는
    /// <b>순서의 문제</b>를 만든다. 10스테이지는 용 단독이지만, 폭풍·완전한 부활과 함께
    /// 전투가 길어지면 8스테이지 호위가 남아 있는 상황도 상정한다.
    /// </summary>
    public sealed class VoidDragonOrigin : PersistentStatusPassive
    {
        private const float PerAlly = 0.03f;
        private const float DeathHealRatio = 0.5f;

        private Action<EventContext> _turnHandler;
        private Action<EventContext> _deathHandler;

        public VoidDragonOrigin(PassiveCodeContext context)
            : base(context, VoidDragonStatusIds.VoidOrigin, "void_dragon_origin", "공허의 근원",
                $"필드의 공허 괴수 아군 하나당 올스탯 +{PerAlly * 100f:F0}%. "
                + $"자신이 쓰러지면 남은 공허 괴수 아군이 최대 체력의 {DeathHealRatio * 100f:F0}%를 회복합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => null;

        protected override void OnRegistered()
        {
            _turnHandler = _ => Refresh();
            _deathHandler = _ => HealSurvivors();
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            Refresh();
        }

        protected override void OnUnregistered()
        {
            Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            _turnHandler = null;
            _deathHandler = null;
        }

        private int VoidAllyCount()
            => CombatTargets.AliveAlliesIncludingSelf(Caster)
                .Count(unit => unit != Caster && unit.HasUnitTag("VoidMonster"));

        private void Refresh()
        {
            if (Caster == null || !Registered || !Caster.isActive) return;

            float bonus = 1f + PerAlly * VoidAllyCount();
            Caster.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster,
                new AllStatMultiplierEffect(bonus),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"올스탯 +{(bonus - 1f) * 100f:F0}%"));
        }

        private void HealSurvivors()
        {
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)
                         .Where(unit => unit != Caster && unit.HasUnitTag("VoidMonster")))
            {
                ally.ModifyHp(ally.HpCurr + Mathf.RoundToInt(ally.HpMax * DeathHealRatio), Caster);
            }
        }
    }

    /// <summary>
    /// 에테르(1531) — 원소술사(76)의 금색 상위. 아군 전체가 원소 반응으로 만든 피해 +40%.
    /// 같은 상태 키를 써서 원소술사·대해의 판결과 겹치지 않고 높은 쪽만 남는다.
    /// </summary>
    public sealed class VoidAether : PassiveCode
    {
        public const int CodeId = 1531;
        private const float Bonus = 0.40f;

        public VoidAether(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "에테르";
            IgnoresActivationChance = true;
            Transferable = false;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            VoidDragonStatusIds.Aether, "skadi_elementalist", CodeName,
            Caster, Caster, new ReactionAmplifierEffect(Bonus),
            stackPolicy: BaseEnums.StatusStackPolicy.Replace,
            isBeneficial: true,
            description: $"아군 전체가 원소 반응으로 만든 피해 +{Bonus * 100f:F0}%. 하위 코드와 중첩되지 않습니다."));
    }

    /// <summary>
    /// 완전한 부활(1532) — 명계의 재림(1403)의 금색 상위.
    ///
    /// 원본은 <b>다음 자기 턴</b>에 되살아나 그 사이 한 턴을 잃는다. 이쪽은 그 자리에서
    /// 일어나고 2턴 동안 받는 피해까지 줄어, 부활 직후 다시 눕는 일이 없다.
    /// </summary>
    public sealed class VoidPerfectResurrection : PassiveCode
    {
        public const int CodeId = 1532;

        public VoidPerfectResurrection(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "완전한 부활";
            IgnoresActivationChance = true;
            Transferable = false;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            VoidDragonStatusIds.PerfectResurrection, "void_perfect_resurrection", CodeName,
            Caster, Caster, new PerfectResurrectionEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            category: BaseEnums.StatusCategory.Neutral,
            isBeneficial: true,
            description: "치명적인 피해를 입으면 전투당 1회 즉시 부활합니다. "
                         + "해로운 상태를 모두 잃고 체력이 가득 차며 2턴간 받는 피해가 30% 감소합니다."));
    }

    internal sealed class PerfectResurrectionEffect : BaseEffect
    {
        private const float WardReduction = 0.30f;
        private const int WardTurns = 2;

        private bool _used;

        public PerfectResurrectionEffect() : base(0) { }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit == null || unit != Target) return false;
            _used = true;

            foreach (var status in Target.ActiveStatuses
                         .Where(status => status != null && status.Category == BaseEnums.StatusCategory.Negative)
                         .ToList())
            {
                Target.RemoveStatusByKey(status.Key);
            }
            Target.ResetCombatElements();
            Target.ModifyHp(Target.HpMax, Target);
            Target.AddStatus(BuffStatus.Create(
                VoidDragonStatusIds.ResurrectionWard, "void_resurrection_ward", "완전한 부활",
                Target, Target, new FlatMitigationEffect(WardReduction),
                duration: WardTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"{WardTurns}턴 동안 받는 피해가 {WardReduction * 100f:F0}% 감소합니다."));

            Debug.Log($"[완전한 부활] {Target.UnitName}이(가) 그 자리에서 되살아났다.");
            return true;
        }
    }

    /// <summary>
    /// 원소 정통(1533) — 원소 숙련(58·186·187·57·188·74·189)의 금색 상위.
    ///
    /// 숙련은 원소마다 코드가 따로라 상위도 일곱 개가 되어야 한다. 대신 이 코드는
    /// <b>보유자 자신의 고유 원소</b>를 읽어 하나로 여덟 변형을 모두 덮는다.
    /// </summary>
    public sealed class VoidElementalMastery : PassiveCode
    {
        public const int CodeId = 1533;
        private const float Bonus = 0.25f;

        public VoidElementalMastery(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 정통";
            IgnoresActivationChance = true;
            Transferable = false;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            VoidDragonStatusIds.ElementalMastery, "void_elemental_mastery", CodeName,
            Caster, Caster, new SelfElementMasteryEffect(Bonus),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: $"자신의 원소를 보유한 적에게 주는 피해 +{Bonus * 100f:F0}%."));
    }

    internal sealed class SelfElementMasteryEffect : BaseEffect
    {
        private readonly float _bonus;
        public SelfElementMasteryEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            if (!Enum.TryParse(Target.Element, true, out BaseEnums.UnitElement own)) return 1f;
            if (own == BaseEnums.UnitElement.None) return 1f;

            return target.HasCombatElement(own) ? 1f + _bonus : 1f;
        }
    }
}
