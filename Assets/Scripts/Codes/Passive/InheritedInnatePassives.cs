using System;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    internal static class InheritedInnateStatusIds
    {
        public const int AtalanteWeakness = 5950;
        public const int OrionGeoAffinity = 5951;
        public const int AmaterasuSunRhythm = 5952;
        public const int AsclepiusNashorsTooth = 5953;
    }

    /// <summary>약점 추적 열화본: 디버프당 방어력 3% 무시, 최대 12%.</summary>
    public sealed class InheritedAtalanteWeaknessTracker : PassiveCode
    {
        public InheritedAtalanteWeaknessTracker(PassiveCodeContext context) : base(context)
        {
            CodeName = "약점 추적 (전수)";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            InheritedInnateStatusIds.AtalanteWeakness, "inherited_atalante_weakness", CodeName,
            Caster, Caster, new InheritedDebuffPenetrationEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "대상의 디버프 하나당 방어력을 3% 무시합니다(최대 12%)."));
    }

    /// <summary>원소 친화 - 바위 열화본: 바위 원소 보유 중 2초마다 CON +1.</summary>
    public sealed class InheritedOrionGeoAffinity : PassiveCode
    {
        public InheritedOrionGeoAffinity(PassiveCodeContext context) : base(context)
        {
            CodeName = "원소 친화 - 바위 (전수)";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            InheritedInnateStatusIds.OrionGeoAffinity, "inherited_orion_geo_affinity", CodeName,
            Caster, Caster, new InheritedGeoGrowthEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Replace,
            isBeneficial: true,
            description: "바위 원소를 보유한 동안 2초마다 CON이 1 증가합니다."));
    }

    /// <summary>내셔의 이빨 열화본: 궁극기 사용 후 5초간 DEX +20.</summary>
    public sealed class InheritedAsclepiusNashorsTooth : PassiveCode
    {
        private Action<EventContext> _castHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public InheritedAsclepiusNashorsTooth(PassiveCodeContext context) : base(context)
        {
            CodeName = "내셔의 이빨 (전수)";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _castHandler = _ => ApplyDexBuff();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _castHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void ApplyDexBuff()
        {
            Caster?.AddStatus(BuffStatus.Create(
                InheritedInnateStatusIds.AsclepiusNashorsTooth,
                "inherited_asclepius_nashors_tooth", CodeName,
                Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, 20),
                duration: 5f,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "궁극기 사용 후 5초간 DEX가 20 증가합니다."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _castHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey("inherited_asclepius_nashors_tooth");
            _registered = false;
        }
    }

    /// <summary>태양의 박자 열화본: 기본공격마다 6초간 DEX +2, 최대 4중첩.</summary>
    public sealed class InheritedAmaterasuSunRhythm : PassiveCode
    {
        private Action<EventContext> _hitHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public InheritedAmaterasuSunRhythm(PassiveCodeContext context) : base(context)
        {
            CodeName = "태양의 박자 (전수)";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _hitHandler = _ => AddStack();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void AddStack()
        {
            if (Caster.GetAllStatuses(InheritedInnateStatusIds.AmaterasuSunRhythm).Count >= 4) return;
            Caster.AddStatus(BuffStatus.Create(
                InheritedInnateStatusIds.AmaterasuSunRhythm, "inherited_amaterasu_sun_rhythm", CodeName,
                Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, 2),
                duration: 6f,
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "DEX +2 (최대 4중첩)."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey("inherited_amaterasu_sun_rhythm");
            _registered = false;
        }
    }

    internal sealed class InheritedDebuffPenetrationEffect : BaseEffect
    {
        public InheritedDebuffPenetrationEffect() : base(0) { }

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            int debuffCount = target.GetAllStatuses()
                .FindAll(status => status.Category == BaseEnums.StatusCategory.Negative).Count;
            return 1f - Mathf.Min(4, debuffCount) * 0.03f;
        }
    }

    internal sealed class InheritedGeoGrowthEffect : BaseEffect
    {
        private float _elapsed;
        private int _conStacks;
        public InheritedGeoGrowthEffect() : base(0) { }

        public override void OnUpdate(float deltaTime)
        {
            if (Target == null || !Target.isActive || !Target.HasCombatElement(BaseEnums.UnitElement.Geo)) return;
            _elapsed += deltaTime;
            int gained = Mathf.FloorToInt(_elapsed / 2f);
            if (gained <= 0) return;
            _elapsed -= gained * 2f;
            _conStacks += gained;
            Target.RefreshAttributes();
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.CON ? _conStacks : 0;
    }
}
