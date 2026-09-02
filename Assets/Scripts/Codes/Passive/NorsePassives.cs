using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>프레이아·로키·스카디가 쓰는 상태 ID 대역.</summary>
    public static class NorseStatusIds
    {
        public const int Phytoncide = 5320;
        public const int PhytoncideRegen = 5321;
        public const int Medicine = 5322;
        public const int Leadership = 5323;
        public const int WarChief = 5324;
        public const int Fenrir = 5325;
        public const int Summoner = 5326;
        public const int FrostWarrior = 5327;
        public const int CryoMastery = 5328;
        public const int CryoAffinity = 5329;
        public const int Elementalist = 5330;
    }

    // ══════════════════════════════════════════════════════════════
    // 프레이아
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 프레이아 고유 P — 피톤치드.
    /// 프레이아가 치유한 아군에게 2초에 걸쳐 최대 체력의 5%를 더 회복시킨다.
    /// 대상별 내부 쿨다운 2초라 광역 힐 한 번이 여러 번 겹쳐 터지지 않는다.
    /// </summary>
    public sealed class FreyaPhytoncide : UniquePassiveCode
    {
        public FreyaPhytoncide(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "피톤치드";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Phytoncide, "freya_phytoncide", CodeName,
            Caster, Caster, new PhytoncideEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "치유한 아군이 1턴에 걸쳐 최대 체력의 5%를 추가로 회복합니다."));
    }

    /// <summary>Lv.30 의술 — 부여하는 치유·보호막 +25%. `의신의 가호`(98)의 하위 코드.</summary>
    public sealed class FreyaMedicine : PassiveCode
    {
        public FreyaMedicine(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "의술";
            IgnoresActivationChance = true;
            // 일반 등급. 의신의 가호(98)를 배웠으면 발동 자체가 막힌다.
            SupersededByCodeId = AsclepiusDivineMedicine.CodeId;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Medicine, "freya_medicine", CodeName,
            Caster, Caster, new MedicineEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "부여하는 치유와 보호막이 25% 증가합니다. 의신의 가호와 중첩되지 않습니다."));
    }

    /// <summary>Lv.40 리더쉽 — 필드에 있는 동안 아군 전체가 가하는 피해 +10%.</summary>
    public sealed class FreyaLeadership : PassiveCode
    {
        public const string SharedKey = "leadership_damage_aura";
        private const float Multiplier = 1.1f;

        public FreyaLeadership(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "리더쉽";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AuraTargets.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NorseStatusIds.Leadership, SharedKey, CodeName, Caster, ally,
                    new FlatOutgoingDamageEffect(Multiplier),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "가하는 피해 +10%"));
            }
        }
    }

    /// <summary>Lv.61 전사장 — 전열 아군에게 STR +8. 전열 여부는 매 질의마다 다시 본다.</summary>
    public sealed class FreyaWarChief : PassiveCode
    {
        public const string SharedKey = "warchief_front_str";
        private const int Bonus = 8;

        public FreyaWarChief(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "전사장";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            foreach (Unit ally in AuraTargets.Allies(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    NorseStatusIds.WarChief, SharedKey, CodeName, Caster, ally,
                    new FrontRowStatEffect(BaseEnums.PrimaryStat.STR, Bonus),
                    stackPolicy: BaseEnums.StatusStackPolicy.ReplaceIfStronger,
                    isBeneficial: true, description: "전열에 있는 동안 STR +8"));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 로키
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 로키 고유 P — 펜리르.
    /// 소환수 펜리르가 4초마다 현재 체력이 가장 낮은 적을 문다.
    /// 칸을 차지하지 않아야 하므로 별도 유닛을 만들지 않고 로키에 붙는 상태로 굴린다.
    /// </summary>
    public sealed class LokiFenrir : UniquePassiveCode
    {
        public LokiFenrir(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "펜리르";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Fenrir, "loki_fenrir", CodeName,
            Caster, Caster, new FenrirEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "펜리르가 2턴마다 체력이 가장 낮은 적에게 STR 위력 40의 피해를 입힙니다."));
    }

    /// <summary>Lv.25 소환사 — 필드의 모든 소환수가 가하는 피해 +25%.</summary>
    public sealed class LokiSummoner : PassiveCode
    {
        public LokiSummoner(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "소환사";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Summoner, "loki_summoner", CodeName,
            Caster, Caster, new SummonMasterEffect(1.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "필드의 모든 소환수가 가하는 피해 +25%. 중첩되지 않습니다."));
    }

    // ══════════════════════════════════════════════════════════════
    // 스카디
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 스카디 고유 P — 서리의 전사.
    /// 8초마다 부착 원소가 하나도 없으면 자신에게 얼음을 두른다. 상시 빙결 면역.
    ///
    /// 스카디의 고유 원소가 얼음이라 평소에는 발동하지 않는다.
    /// 원소 반응은 고유 원소까지 걷어 가므로, <b>반응으로 얼음을 잃은 직후</b> 되돌리는 코드다.
    /// </summary>
    public sealed class SkadiFrostWarrior : UniquePassiveCode
    {
        public SkadiFrostWarrior(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "서리의 전사";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.FrostWarrior, "skadi_frost_warrior", CodeName,
            Caster, Caster, new FrostWarriorEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "4턴마다 원소가 없으면 얼음을 부착합니다. 빙결에 면역입니다."));
    }

    /// <summary>Lv.10 원소 숙련 - 얼음 — 얼음 원소를 보유한 적에게 주는 피해 +10%.</summary>
    public sealed class CryoMastery : ElementMasteryPassive
    {
        public CryoMastery(PassiveCodeContext context) : base(
            context, "원소 숙련 - 얼음", BaseEnums.UnitElement.Cryo,
            NorseStatusIds.CryoMastery, "mastery_cryo") { }
    }

    /// <summary>Lv.12 원소 친화 - 얼음 — 자신이 얼음을 보유한 동안 빙결된 적을 때리면 확정 치명타.</summary>
    public sealed class SkadiCryoAffinity : PassiveCode
    {
        public SkadiCryoAffinity(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소 친화 - 얼음";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.CryoAffinity, "skadi_cryo_affinity", CodeName,
            Caster, Caster, new FrozenTargetCritEffect(),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "얼음 원소를 보유한 동안 빙결된 적을 공격하면 반드시 치명타가 됩니다."));
    }

    /// <summary>Lv.60 원소술사 — 아군 전체가 원소 반응으로 만든 피해 +25%.</summary>
    public sealed class SkadiElementalist : PassiveCode
    {
        public SkadiElementalist(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "원소술사";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            NorseStatusIds.Elementalist, "skadi_elementalist", CodeName,
            Caster, Caster, new ReactionAmplifierEffect(0.25f),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: "아군 전체가 원소 반응으로 만든 피해 +25%. 상위 코드와 중첩되지 않습니다."));
    }

    // ══════════════════════════════════════════════════════════════
    // 효과 구현
    // ══════════════════════════════════════════════════════════════

    /// <summary>필드 전역 버프의 대상 목록을 뽑는 공용 헬퍼.</summary>
    internal static class AuraTargets
    {
        public static List<Unit> Allies(Unit caster)
        {
            List<Unit> allies = global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive)
                .ToList();
            if (caster != null && caster.isActive && !allies.Contains(caster)) allies.Add(caster);
            return allies;
        }
    }

    /// <summary>피톤치드 — 프레이아가 치유를 넣을 때마다 대상에게 회복 상태를 얹는다.</summary>
    internal sealed class PhytoncideEffect : BaseEffect
    {
        private const int RegenDurationTurns = 1;   // 2초 → 1턴
        private const float MaxHpRatio = 0.05f;

        public PhytoncideEffect() : base(0) { }

        public override void OnHealingOrShieldGranted(Unit source, Unit target)
        {
            if (source != Target || target == null || !target.isActive) return;

            // 피톤치드가 만든 회복이 다시 피톤치드를 부르지 않도록,
            // 이미 회복 상태가 붙어 있는 대상은 건너뛴다.
            if (target.HasStatus(NorseStatusIds.PhytoncideRegen)) return;

            int total = Mathf.Max(1, Mathf.RoundToInt(target.HpMax * MaxHpRatio));
            target.AddStatus(BuffStatus.Create(
                NorseStatusIds.PhytoncideRegen, "freya_phytoncide_regen", "피톤치드",
                source, target, new TimedRegenEffect(total, RegenDurationTurns),
                duration: RegenDurationTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"{RegenDurationTurns}턴에 걸쳐 {total}을 회복합니다."));
        }
    }

    /// <summary>지정한 총량을 지속 턴에 걸쳐 균등하게 회복시킨다.</summary>
    internal sealed class TimedRegenEffect : BaseEffect
    {
        private readonly int _perTurn;

        public TimedRegenEffect(int total, int durationTurns) : base(0, total)
        {
            _perTurn = Mathf.Max(1, Mathf.RoundToInt(total / (float)Mathf.Max(1, durationTurns)));
        }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;
            Target.ModifyHp(Target.HpCurr + _perTurn, Caster ?? Target);
        }
    }

    /// <summary>의술 — 부여 치유·보호막 +25%.</summary>
    internal sealed class MedicineEffect : BaseEffect
    {
        private const float Multiplier = 1.25f;

        public MedicineEffect() : base(0, Multiplier) { }

        /// <summary>
        /// 등급 대체는 <see cref="PassiveCode.SupersededByCodeId"/>가 발동 단계에서 막는다.
        /// 여기 남은 판정은 <b>배운 기록 없이</b> 장비가 의신의 가호를 부여한 경우를 위한 것이다.
        /// </summary>
        private bool Suppressed =>
            Target != null && Target.HasStatus(AsclepiusStatusIds.DivineMedicine);

        public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            => source == Target && !Suppressed ? Multiplier : 1f;

        public override float OutgoingShieldMultiplierModifier(Unit source, Unit target)
            => source == Target && !Suppressed ? Multiplier : 1f;
    }

    /// <summary>전열에 있는 동안에만 붙는 스탯 보너스.</summary>
    internal sealed class FrontRowStatEffect : BaseEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amount;

        public FrontRowStatEffect(BaseEnums.PrimaryStat stat, int amount) : base(0, amount)
        {
            _stat = stat;
            _amount = amount;
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || stat != _stat) return 0;
            if (unit.currentCell == null || GridManager.Instance == null) return 0;
            return unit.currentCell.xPos == GridManager.Instance.GetFrontColumn(unit.IsEnemy) ? _amount : 0;
        }
    }

    /// <summary>펜리르 — 로키의 2턴마다 현재 체력이 가장 낮은 적을 문다.</summary>
    internal sealed class FenrirEffect : BaseEffect
    {
        private const int IntervalTurns = 2;   // 4초 → 2턴
        private const int Power = 40;

        private int _turnsSinceBite;

        public FenrirEffect() : base(0, Power) { }

        /// <summary>주기가 차면 스케줄러에 추가공격으로 예약한다. 같은 키라 겹쳐 쌓이지 않는다.</summary>
        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            _turnsSinceBite++;
            if (_turnsSinceBite < IntervalTurns) return;

            _turnsSinceBite = 0;
            Unit owner = Target;
            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                owner, "loki_fenrir", "펜리르", () => Bite(owner));
        }

        private static void Bite(Unit owner)
        {
            Unit prey = global::Target.GetAllEnemies(owner)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .OrderBy(unit => unit.HpCurr)
                .FirstOrDefault();
            if (prey == null) return;

            Combat.Summons.Deal(owner, prey, Power, BaseEnums.PrimaryStat.STR,
                DamageTag.ContactAttack, DamageTag.Physical);
        }
    }

    /// <summary>소환사 — 아군 전체 소환수 피해 배율. <see cref="Combat.Summons"/>가 최댓값 하나만 읽는다.</summary>
    internal sealed class SummonMasterEffect : BaseEffect
    {
        private readonly float _multiplier;

        public SummonMasterEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override float SummonDamageMultiplierModifier(Unit unit) => _multiplier;
        public override float AlliedSummonDamageMultiplierModifier(Unit unit, Unit summonOwner) => _multiplier;
    }

    /// <summary>서리의 전사 — 빙결 면역 + 4턴마다 무원소 상태면 얼음 재부착.</summary>
    internal sealed class FrostWarriorEffect : BaseEffect
    {
        private const int IntervalTurns = 4;   // 8초 → 4턴

        private int _turnsSinceCheck;

        public FrostWarriorEffect() : base(0) { }

        public override bool GrantsFreezeImmunity(Unit unit) => unit == Target;

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            _turnsSinceCheck++;
            if (_turnsSinceCheck < IntervalTurns) return;
            _turnsSinceCheck = 0;

            // 원소 부착은 행동이 아니다. 큐를 거치지 않고 그 자리에서 처리한다.
            if (Target.HasAnyCombatElement) return;
            Target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Target);
        }
    }

    /// <summary>
    /// 원소 친화 - 얼음 — 얼음을 두른 채 빙결된 적을 때리면 확정 치명타.
    ///
    /// 치명타 확률은 <c>AttributesUpdate</c> 시점에 한 번 굳으므로 대상별 조건을 담을 수 없다.
    /// 대신 바유 `기습`과 같은 방식으로, 피해가 계산되는 시점에 대상을 보고
    /// 치명타가 아니었다면 배율을 보정해 결과적으로 확정 치명타로 만든다.
    /// </summary>
    internal sealed class FrozenTargetCritEffect : BaseEffect
    {
        public FrozenTargetCritEffect() : base(0) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || context == null) return 1f;
            if (context.CodeType == BaseEnums.CodeType.Effect) return 1f;
            if (!attacker.HasCombatElement(BaseEnums.UnitElement.Cryo) || !target.IsFrozen) return 1f;

            float critMultiplier = Mathf.Max(1f, attacker.CritMultiplierCurr);
            float applied = context.IsCrit ? critMultiplier : 1f;
            context.IsCrit = true;
            return critMultiplier / applied;
        }
    }

    /// <summary>원소 반응 피해 증폭. <see cref="ElementalReaction.FieldReactionMultiplier"/>가 최댓값 하나만 읽는다.</summary>
    internal sealed class ReactionAmplifierEffect : BaseEffect, IReactionAmplifier
    {
        public float ReactionDamageBonus { get; }

        public ReactionAmplifierEffect(float bonus) : base(0, bonus) => ReactionDamageBonus = bonus;
    }
}
