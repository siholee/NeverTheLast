using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>쿠베라·바루나·오르페우스·스사노오가 쓰는 상태 ID 대역.</summary>
    public static class NewSupportStatusIds
    {
        public const int EarthLaw = 5340;
        public const int KuberaGeoAffinity = 5341;
        public const int Rooting = 5342;
        public const int Merchant = 5343;
        public const int OceanVerdict = 5344;
        public const int WaterHeaven = 5345;
        public const int Bard = 5346;
        public const int Raijin = 5347;
        public const int WaveCut = 5348;
    }

    // ══════════════════════════════════════════════════════════════
    // 쿠베라
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 쿠베라 고유 P — 대지의 계율.
    /// 에어본 상태인 적이 받는 피해 +10%.
    ///
    /// '받는 피해'를 적 쪽에 얹으려면 라운드 도중 합류하는 적까지 계속 추적해야 한다.
    /// 실제로 적을 때리는 쪽은 아군뿐이므로, <b>아군이 에어본 대상에게 주는 피해</b>로 뒤집어
    /// 같은 결과를 훨씬 싸게 얻는다.
    /// </summary>
    public sealed class KuberaEarthLaw : UniquePassiveCode
    {
        public const string SharedKey = "airborne_target_damage_aura";
        private const float Multiplier = 1.1f;

        public KuberaEarthLaw(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "대지의 계율";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AuraTargets.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NewSupportStatusIds.EarthLaw, SharedKey, CodeName, Caster, ally,
                    new AirborneTargetDamageEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "에어본 상태의 적에게 주는 피해 +10%"));
            }
        }
    }

    /// <summary>Lv.5 원소 친화 - 바위 — 바위 원소 보유 중 접촉 공격에게 받는 피해 −25%.</summary>
    public sealed class KuberaGeoAffinity : PassiveCode
    {
        public KuberaGeoAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 바위";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NewSupportStatusIds.KuberaGeoAffinity, "kubera_geo_affinity", CodeName,
            Caster, Caster, new GeoContactGuardEffect(0.75f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "바위 원소를 보유한 동안 접촉 공격에게 받는 피해가 25% 감소합니다."));
    }

    /// <summary>Lv.27 뿌리박기 — 자신의 원소가 풀 또는 바위면 상시 에어본 면역.</summary>
    public sealed class KuberaRooting : PassiveCode
    {
        public KuberaRooting(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "뿌리박기";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NewSupportStatusIds.Rooting, "kubera_rooting", CodeName,
            Caster, Caster, new RootingEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "풀 또는 바위 원소를 보유한 동안 에어본에 면역입니다."));
    }

    /// <summary>Lv.32 상인 — 전투에서 얻는 골드 +25%. 실제 적용은 <see cref="RewardModifiers"/>가 한다.</summary>
    public sealed class KuberaMerchant : PassiveCode
    {
        public const float GoldBonus = 0.25f;

        public KuberaMerchant(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "상인";
            IgnoresActivationChance = true;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 바루나
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 바루나 고유 P — 대해의 판결.
    /// 아군 전체가 원소 반응으로 만든 피해 +40%. 스카디 `원소술사`의 상위 코드이며
    /// 둘 다 하나만, 그리고 높은 쪽만 적용된다(집계는 <c>ElementalReaction.FieldReactionMultiplier</c>).
    /// </summary>
    public sealed class VarunaOceanVerdict : UniquePassiveCode
    {
        public VarunaOceanVerdict(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "대해의 판결";
            IgnoresActivationChance = true;
            // 원소술사(76)의 강화 등급. 필드 판정은 IReactionAmplifier가 높은 쪽만 고른다.
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NewSupportStatusIds.OceanVerdict, "varuna_ocean_verdict", CodeName,
            Caster, Caster, new ReactionAmplifierEffect(0.40f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "아군 전체가 원소 반응으로 만든 피해 +40%. 원소술사와 중첩되지 않습니다."));
    }

    /// <summary>Lv.50 수천(水天) — 물·번개·바람 원소 아군의 치명타 피해 +25%.</summary>
    public sealed class VarunaWaterHeaven : PassiveCode
    {
        public const string SharedKey = "water_heaven_crit_aura";

        private static readonly BaseEnums.UnitElement[] Elements =
        {
            BaseEnums.UnitElement.Hydro,
            BaseEnums.UnitElement.Electro,
            BaseEnums.UnitElement.Anemo,
        };

        public VarunaWaterHeaven(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "수천";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AuraTargets.Allies(Caster)
                         .Where(unit => Elements.Any(unit.HasCombatElement)))
            {
                ally.AddStatus(BuffStatus.Create(
                    NewSupportStatusIds.WaterHeaven, SharedKey, CodeName, Caster, ally,
                    new SpecialCritDamageEffect(0.25f),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "치명타 피해 +25%"));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 오르페우스
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 오르페우스 고유 P — 죽음마저 속인 자.
    /// 전투 시작 시 궁극기 `비탄의 연주`를 즉시 발동한다.
    /// 궁극기 자원과 무관하게 한 번 터뜨리므로, 강인도가 첫 교전부터 깔린다.
    /// </summary>
    public sealed class OrpheusCheatedDeath : UniquePassiveCode
    {
        public OrpheusCheatedDeath(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "죽음마저 속인 자";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || !Caster.isActive) return;
            Caster.ActiveUltimateCode?.CastCode();
        }
    }

    /// <summary>Lv.30 음유시인 — 바람 원소 보유자면 아군 전체 획득 EXP +5%.</summary>
    public sealed class OrpheusBard : PassiveCode
    {
        public const float ExpBonus = 0.05f;

        public OrpheusBard(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "음유시인";
            IgnoresActivationChance = true;
        }

        /// <summary>바람 원소 조건은 지급 시점에 다시 본다.</summary>
        public bool IsActiveFor(Unit unit)
            => unit != null && unit.HasCombatElement(BaseEnums.UnitElement.Anemo);
    }

    // ══════════════════════════════════════════════════════════════
    // 스사노오
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 스사노오 고유 P — 뇌신.
    ///
    /// 세 가지를 한꺼번에 한다.
    ///   1. 바람·번개 원소 <b>판정</b>을 얻는다(부착이 아니라 판정이므로 원소 반응 재료가 되지 않는다).
    ///   2. 물·바람·번개가 부착된 적을 때릴 때 방어력 20%를 무시한다.
    ///   3. 상시형 궁극기 `천총운검`을 굴린다 — 에어본 상태의 적이 있으면 즉시 벤다.
    ///
    /// 3번을 여기서 처리하는 것은 수르트의 황혼이 라그나로크를 관리하는 것과 같은 구조다.
    /// </summary>
    public sealed class SusanooRaijin : UniquePassiveCode
    {
        public SusanooRaijin(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "뇌신";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            Caster.AddElementJudgement(BaseEnums.UnitElement.Anemo);
            Caster.AddElementJudgement(BaseEnums.UnitElement.Electro);

            Caster.AddStatus(BuffStatus.Create(
                NewSupportStatusIds.Raijin, "susanoo_raijin", CodeName,
                Caster, Caster, new RaijinEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "바람·번개 원소 판정을 얻고, 물·바람·번개가 붙은 적의 방어력 20%를 무시합니다. "
                             + "에어본 상태의 적이 있으면 천총운검이 즉시 발동합니다."));
        }
    }

    /// <summary>Lv.22 파도베기 — 양손검 또는 한손검 숙련이면 물 속성 유닛에게 받는 피해 −30%.</summary>
    public sealed class SusanooWaveCut : PassiveCode
    {
        public SusanooWaveCut(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "파도베기";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            bool qualified = Caster.HasProficiency(EquipmentProficiency.Greatsword) ||
                             Caster.HasProficiency(EquipmentProficiency.Longsword);
            if (!qualified) return;

            Caster.AddStatus(BuffStatus.Create(
                NewSupportStatusIds.WaveCut, "susanoo_wave_cut", CodeName,
                Caster, Caster, new ElementalAttackerGuardEffect(BaseEnums.UnitElement.Hydro, 0.7f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "물 속성 유닛에게 받는 피해가 30% 감소합니다."));
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 효과 구현
    // ══════════════════════════════════════════════════════════════

    /// <summary>에어본 상태의 대상에게 주는 피해 배율.</summary>
    internal sealed class AirborneTargetDamageEffect : BaseEffect
    {
        private readonly float _multiplier;

        public AirborneTargetDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.IsAirborne ? _multiplier : 1f;
    }

    /// <summary>바위 원소를 보유한 동안 접촉 공격에게 받는 피해를 줄인다.</summary>
    internal sealed class GeoContactGuardEffect : BaseEffect
    {
        private readonly float _multiplier;

        public GeoContactGuardEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.DamageTags == null) return 1f;
            if (!unit.HasCombatElement(BaseEnums.UnitElement.Geo)) return 1f;
            return context.DamageTags.Contains(DamageTag.ContactAttack) ? _multiplier : 1f;
        }
    }

    /// <summary>지정한 원소를 가진 공격자에게 받는 피해를 줄인다.</summary>
    internal sealed class ElementalAttackerGuardEffect : BaseEffect
    {
        private readonly BaseEnums.UnitElement _element;
        private readonly float _multiplier;

        public ElementalAttackerGuardEffect(BaseEnums.UnitElement element, float multiplier)
            : base(0, multiplier)
        {
            _element = element;
            _multiplier = multiplier;
        }

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.Attacker == null) return 1f;
            return context.Attacker.HasCombatElement(_element) ? _multiplier : 1f;
        }
    }

    /// <summary>뿌리박기 — 풀 또는 바위 원소를 보유한 동안 에어본 면역.</summary>
    internal sealed class RootingEffect : BaseEffect
    {
        public RootingEffect() : base(0) { }

        public override bool GrantsAirborneImmunity(Unit unit)
            => unit == Target &&
               (unit.HasCombatElement(BaseEnums.UnitElement.Dendro) ||
                unit.HasCombatElement(BaseEnums.UnitElement.Geo));
    }

    /// <summary>
    /// 뇌신 + 천총운검.
    /// 방어 무시는 질의 훅으로, 상시형 궁극기는 틱으로 처리한다.
    /// </summary>
    internal sealed class RaijinEffect : BaseEffect
    {
        private const float DefenseIgnore = 0.8f;
        private const int StrikeCooldownTurns = 1;   // 2초 → 1턴
        private const int StrikePower = 80;

        private static readonly BaseEnums.UnitElement[] Marked =
        {
            BaseEnums.UnitElement.Hydro,
            BaseEnums.UnitElement.Anemo,
            BaseEnums.UnitElement.Electro,
        };

        private int _cooldownTurns;

        public RaijinEffect() : base(0) { }

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            return Marked.Any(target.HasCombatElement) ? DefenseIgnore : 1f;
        }

        /// <summary>
        /// 스사노오의 턴마다 에어본 상태의 적을 찾아 추가공격으로 예약한다.
        /// 한 번에 하나만 행동해야 하므로 그 자리에서 터뜨리지 않는다.
        /// </summary>
        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            if (_cooldownTurns > 0)
            {
                _cooldownTurns--;
                return;
            }

            if (!global::Target.GetAllEnemies(Target)
                    .Any(unit => unit != null && unit.isActive && !unit.IsUntargetable && unit.IsAirborne))
            {
                return;
            }

            _cooldownTurns = StrikeCooldownTurns;
            Unit owner = Target;
            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                owner, "susanoo_ameno", "천총운검", () => Strike(owner));
        }

        private static void Strike(Unit owner)
        {
            Unit airborne = global::Target.GetAllEnemies(owner)
                .FirstOrDefault(unit => unit != null && unit.isActive && !unit.IsUntargetable && unit.IsAirborne);
            if (airborne == null) return;

            bool isCrit = UnityEngine.Random.value <= owner.CritChanceCurr;
            float critMultiplier = isCrit ? owner.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                owner.SkillDamage(StrikePower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

            airborne.TakeDamage(new DamageContext(owner, damage, BaseEnums.CodeType.Ultimate, new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.AdditionalAttack,
                DamageTag.ContactAttack, DamageTag.Physical, DamageTag.Slash,
            }, isCrit));

            Debug.Log($"[천총운검] {owner.UnitName} → {airborne.UnitName} ({damage})");
        }
    }

    /// <summary>
    /// 전투 밖 보상 배율. 골드·EXP는 상태가 정리된 뒤에도 지급될 수 있으므로
    /// 상태가 아니라 <b>보유 코드</b>를 직접 훑는다.
    /// </summary>
    public static class RewardModifiers
    {
        /// <summary>필드의 아군이 만들어 내는 골드 배율. 같은 코드는 중첩되지 않는다.</summary>
        public static float GoldMultiplier()
        {
            float bonus = 0f;
            foreach (Unit hero in ActiveHeroes())
            {
                if (hero.ActivePassiveCodes.Any(code => code is KuberaMerchant))
                {
                    bonus = Mathf.Max(bonus, KuberaMerchant.GoldBonus);
                }
            }
            return 1f + bonus;
        }

        /// <summary>
        /// 필드의 아군이 만들어 내는 EXP 배율.
        /// 선두주자·음유시인·카리스마는 서로 더해지고, 같은 코드끼리는 가장 큰 하나만 센다.
        /// </summary>
        public static float ExpMultiplier()
        {
            float frontrunner = 0f;
            float bard = 0f;
            float charisma = 0f;
            foreach (Unit hero in ActiveHeroes())
            {
                if (hero.ActivePassiveCodes.Any(code => code is IndraFrontrunner))
                {
                    frontrunner = Mathf.Max(frontrunner, IndraFrontrunner.ExpBonus);
                }

                if (hero.ActivePassiveCodes.Any(code => code is Charisma))
                {
                    charisma = Mathf.Max(charisma, Charisma.ExpBonus);
                }

                OrpheusBard bardCode = hero.ActivePassiveCodes.OfType<OrpheusBard>().FirstOrDefault();
                if (bardCode != null && bardCode.IsActiveFor(hero))
                {
                    bard = Mathf.Max(bard, OrpheusBard.ExpBonus);
                }
            }
            return 1f + frontrunner + bard + charisma;
        }

        private static IEnumerable<Unit> ActiveHeroes()
        {
            var grid = Managers.GridManager.Instance;
            if (grid == null || grid.heroList == null) return Array.Empty<Unit>();
            return grid.heroList.Where(hero => hero != null && !hero.IsEnemy && hero.isActive);
        }
    }
}
