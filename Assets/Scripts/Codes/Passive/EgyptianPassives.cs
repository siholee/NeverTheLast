using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    internal static class EgyptianStatusIds
    {
        public const int HorusSkyFalcon = 7800;
        public const int AnubisSouls = 7801;
        public const int BastetGrace = 7802;
        public const int PiercingShot = 7803;
        public const int SpearGuard = 7804;
        public const int Reaper = 7805;
        public const int LightArmament = 7806;
        public const int Selfish = 7807;
    }

    /// <summary>호루스 P — 행동 속도를 1.0으로 고정하고 초과 속도 1%마다 STR +1.</summary>
    public sealed class HorusSkyFalcon : UniquePassiveCode
    {
        public HorusSkyFalcon(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "창공의 매";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.HorusSkyFalcon, "horus_sky_falcon", CodeName,
            Caster, Caster, new HorusSkyFalconEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "행동 속도가 1.0으로 고정되고 초과 속도 1%마다 STR이 1 증가합니다."));
    }

    internal sealed class HorusSkyFalconEffect : BaseEffect
    {
        public HorusSkyFalconEffect() : base(0) { }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != BaseEnums.PrimaryStat.STR) return 0;
            float excess = Mathf.Max(0f, unit.GetDerivedActionSpeed() - 1f);
            return Mathf.FloorToInt(excess * 100f + 0.0001f);
        }

        public override float ActionSpeedModifier(Unit unit, float calculatedSpeed)
            => unit == Target ? 1f : calculatedSpeed;
    }

    /// <summary>아누비스 P — 필드의 모든 죽음을 영혼으로 저장한다. 코드 인스턴스가 런 동안 스택을 보존한다.</summary>
    public sealed class AnubisSoulHarvest : UniquePassiveCode
    {
        private int _soulStacks;
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        public AnubisSoulHarvest(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "영혼 수확";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                EgyptianStatusIds.AnubisSouls, "anubis_souls", CodeName,
                Caster, Caster, new AnubisSoulStrengthEffect(() => _soulStacks),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "필드에서 유닛이 쓰러질 때마다 영혼을 얻습니다. 영혼 4개마다 STR +1."));

            if (_registered) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Caster == null || !Caster.isActive || dead == null) return;
            _soulStacks++;
            Caster.RefreshAttributes();
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _cleanupHandler = null;
            _registered = false;
        }
    }

    internal sealed class AnubisSoulStrengthEffect : BaseEffect
    {
        private readonly Func<int> _stacks;
        public AnubisSoulStrengthEffect(Func<int> stacks) : base(0) => _stacks = stacks;

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR
                ? Mathf.Max(0, _stacks?.Invoke() ?? 0) / 4
                : 0;
    }

    /// <summary>바스테트 P와 패시브형 궁극기 — 접촉 회피 및 에어본 정점 추가공격.</summary>
    public sealed class BastetAirborneHunter : UniquePassiveCode
    {
        private bool _registered;
        private Action<EventContext> _cleanupHandler;

        public BastetAirborneHunter(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "고양이의 몸놀림";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.AddStatus(BuffStatus.Create(
                EgyptianStatusIds.BastetGrace, "bastet_grace", CodeName,
                Caster, Caster, new BastetContactEvasionEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "LUK×0.5% 확률로 접촉 공격을 회피합니다."));

            if (_registered) return;
            ControlStatuses.AirborneApexReached += OnAirborneApex;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnAirborneApex(Unit source, Unit target)
        {
            if (Caster == null || !Caster.isActive || target == null || !target.isActive ||
                target.IsEnemy == Caster.IsEnemy || !target.HasStatusKey(ControlStatuses.AirborneKey)) return;
            Caster.StartCoroutine(StrikeAtApex(target));
        }

        private IEnumerator StrikeAtApex(Unit target)
        {
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(120, BaseEnums.PrimaryStat.DEX) * crit));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
                }, isCrit);

            GameManager.Instance?.sfxManager?.TryPlayMeleeAttack(Caster, target, context);
            yield return new WaitForSeconds(SfxManager.MeleeImpactDelay);
            if (target != null && target.isActive) target.TakeDamage(context);
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            ControlStatuses.AirborneApexReached -= OnAirborneApex;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _cleanupHandler = null;
            _registered = false;
        }
    }

    internal sealed class BastetContactEvasionEffect : BaseEffect
    {
        public BastetContactEvasionEffect() : base(0) { }

        public override float EvasionChanceAdditiveModifier(Unit unit, DamageContext context)
            => unit == Target && context?.DamageTags?.Contains(DamageTag.ContactAttack) == true
                ? Mathf.Clamp01(unit.GetBaseLuk() * 0.005f)
                : 0f;
    }

    public sealed class EgyptianPiercingShot : PassiveCode
    {
        public EgyptianPiercingShot(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "관통사격"; IgnoresActivationChance = true; }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.PiercingShot, "egypt_piercing_shot", CodeName,
            Caster, Caster, new EgyptianPiercingShotEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "숙련 활을 장착한 화살 공격이 적 내구 4를 무시합니다."));
    }

    internal sealed class EgyptianPiercingShotEffect : BaseEffect
    {
        public EgyptianPiercingShotEffect() : base(0) { }
        public override int DurabilityPenetrationModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && Target.HasEquippedBow() &&
               context?.DamageTags?.Contains(DamageTag.Arrow) == true ? 4 : 0;
    }

    public sealed class AnubisSpearGuard : PassiveCode
    {
        public AnubisSpearGuard(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "창지기"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.SpearGuard, "anubis_spear_guard", CodeName,
            Caster, Caster, new AnubisSpearGuardEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "숙련된 창을 장착하면 STR +3%."));
    }

    internal sealed class AnubisSpearGuardEffect : BaseEffect
    {
        public AnubisSpearGuardEffect() : base(0) { }
        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR &&
               unit.HasEquippedProficiency(EquipmentProficiency.Spear) ? 1.03f : 1f;
    }

    public sealed class AnubisReaper : PassiveCode
    {
        public AnubisReaper(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "저승사자"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.Reaper, "anubis_reaper", CodeName,
            Caster, Caster, new AnubisReaperEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "사령 속성 대상에게 주는 피해가 30% 증가합니다."));
    }

    internal sealed class AnubisReaperEffect : BaseEffect
    {
        public AnubisReaperEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null &&
               (target.HasUnitTag("Undead") || target.HasUnitTag("Necrotic") || target.HasUnitTag("사령"))
                ? 1.30f : 1f;
    }

    /// <summary>바스테트 Lv.2 — TrainingManager가 타입을 확인해 DEX 훈련량에 10%를 곱한다.</summary>
    public sealed class BastetMasterThief : PassiveCode
    {
        public BastetMasterThief(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "대도"; IgnoresActivationChance = true; }
    }

    public sealed class BastetLightArmament : PassiveCode
    {
        public BastetLightArmament(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "가벼운 무장"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.LightArmament, "bastet_light_armament", CodeName,
            Caster, Caster, new BastetLightArmamentEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "의복류 방어구를 착용하면 DEX +5%."));
    }

    internal sealed class BastetLightArmamentEffect : BaseEffect
    {
        public BastetLightArmamentEffect() : base(0) { }
        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX && unit.HasEquippedClothing() ? 1.05f : 1f;
    }

    public sealed class BastetSelfish : PassiveCode
    {
        public BastetSelfish(PassiveCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Passive; CodeName = "이기적"; IgnoresActivationChance = true; }
        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            EgyptianStatusIds.Selfish, "bastet_selfish", CodeName,
            Caster, Caster, new BastetSelfishEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore, isBeneficial: true,
            description: "추가공격으로 가하는 피해가 20% 증가합니다."));
    }

    internal sealed class BastetSelfishEffect : BaseEffect
    {
        public BastetSelfishEffect() : base(0) { }
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(DamageTag.AdditionalAttack) == true
                ? 1.20f : 1f;
    }
}
