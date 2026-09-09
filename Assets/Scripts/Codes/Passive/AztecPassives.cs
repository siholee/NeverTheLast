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
    internal static class AztecCodeNames
    {
        public static string Passive(int codeId) => codeId switch
        {
            320 => "행동불능 추적",
            321 => "맹독 추적",
            322 => "죽음의 제의",
            323 => "연기 나는 거울의 신",
            324 => "인신공양",
            325 => "비몽사몽",
            326 => "망치 나가신다!",
            327 => "원소 친화 - 풀",
            328 => "고귀한 몸",
            _ => "메히코 전투술",
        };
    }

    /// <summary>새 아즈텍 테마 전용 패시브. 320~328은 이전 아즈텍 코드와 겹치지 않는 새 ID다.</summary>
    public sealed class AztecPassive : PassiveCode
    {
        private readonly int _codeId;

        public AztecPassive(PassiveCodeContext context, int codeId, string codeName) : base(context)
        {
            _codeId = codeId;
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = codeName;
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            switch (_codeId)
            {
                case 1300: AddSelf(new ControlledTargetDamageEffect(1.25f)); break;
                case 1301: AddSelf(new PoisonedTargetDamageEffect(1.25f)); break;
                case 1302: AddSelf(new SerpentDeathRiteEffect()); break;
                case 1303:
                    AddSelf(new TezcatlipocaDivinityEffect());
                    AztecCombat.ApplyTezcatlipocaFormation(Caster);
                    break;
                case 1304: AddSelf(new HumanSacrificeEffect()); break;
                case 1305: AddSelf(new DreamlikeResonanceEffect(1.2f)); break;
                case 1306: AddSelf(new MarkerEffect()); break;
                case 1307: AddSelf(new DendroAffinityEffect()); break;
                case 1308: AddSelf(new NobleBodyEffect(-1)); break;
            }
        }

        private void AddSelf(BaseEffect effect)
        {
            Caster.AddStatus(BuffStatus.Create(
                7320 + _codeId, $"aztec_passive_{_codeId}", CodeName,
                Caster, Caster, effect,
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: AztecCombat.Description(_codeId)));
        }
    }

    internal static class AztecCombat
    {
        public const int JaguarWarriorId = 1040;
        public const int EliteJaguarWarriorId = 2010;
        public const int PoisonStatusId = 1;

        public static List<Unit> Enemies(Unit caster) => global::Target.GetAllEnemies(caster)
            .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable).ToList();

        public static List<Unit> AlliesIncludingSelf(Unit caster)
        {
            var allies = global::Target.GetAllAllies(caster)
                .Where(unit => unit != null && unit.isActive).ToList();
            if (caster != null && caster.isActive && !allies.Contains(caster)) allies.Add(caster);
            return allies;
        }

        public static bool IsFrontLine(Unit unit) =>
            unit?.currentCell != null && GridManager.Instance != null &&
            unit.currentCell.xPos == GridManager.Instance.GetFrontColumn(unit.IsEnemy);

        public static bool IsJaguar(Unit unit) => unit != null &&
            (unit.ID == JaguarWarriorId || unit.ID == EliteJaguarWarriorId || unit.HasUnitTag("Jaguar"));

        public static void ApplyPoison(Unit caster, Unit target, int turns = 3, float coefficient = 8f)
        {
            if (caster == null || target == null || !target.isActive) return;
            var poison = new UnitStatus(PoisonStatusId, caster, target) { Duration = Mathf.Max(1, turns) };
            poison.AddEffect(1001, coefficient);
            target.AddStatus(poison);
        }

        public static void ApplyTezcatlipocaFormation(Unit tezcatlipoca)
        {
            if (tezcatlipoca == null) return;
            foreach (Unit ally in AlliesIncludingSelf(tezcatlipoca))
            {
                ally.AddStatus(BuffStatus.Create(
                    7640, $"tezcatlipoca_formation_{tezcatlipoca.GetEntityId()}", "검은 태양의 전열",
                    tezcatlipoca, ally, new AztecFrontLineDamageReductionEffect(tezcatlipoca, 0.2f),
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "전열 아군 하나당 받는 피해가 20% 감소합니다."));
            }
        }

        public static string Description(int codeId) => codeId switch
        {
            320 => "행동불능 상태의 적에게 주는 피해 +25%.",
            321 => "맹독 상태의 적에게 주는 피해 +25%.",
            322 => "아군이 쓰러질 때 적 전체에게 INT 기반 추가행동을 가합니다.",
            323 => "INT에 비례해 최대 체력이 증가하고 전열 수에 비례한 피해 감소를 아군에게 제공합니다.",
            324 => "적이 쓰러질 때마다 STR +5%.",
            325 => "진동 반응의 CON을 1.2배로 계산합니다.",
            326 => "둔기를 장착하고 숙련되어 있으면 내구 5를 무시합니다.",
            327 => "풀 원소 보유 중 CON +2, 자기 턴마다 최대 체력의 1% 회복.",
            328 => "대상 지정 우선도 -1.",
            _ => "",
        };
    }

    internal sealed class MarkerEffect : BaseEffect
    {
        public MarkerEffect() : base(0) { }
        public override int DurabilityPenetrationModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && attacker.HasEquippedProficiency(EquipmentProficiency.Mace) ? 5 : 0;
    }

    internal sealed class ControlledTargetDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public ControlledTargetDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.isControlled ? _multiplier : 1f;
    }

    internal sealed class PoisonedTargetDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public PoisonedTargetDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.HasStatus(AztecCombat.PoisonStatusId) ? _multiplier : 1f;
    }

    internal sealed class SerpentDeathRiteEffect : BaseEffect
    {
        public SerpentDeathRiteEffect() : base(0) { }
        public override void OnApply() => Unit.AnyUnitDied += OnAnyUnitDied;
        public override void OnRemove() => Unit.AnyUnitDied -= OnAnyUnitDied;

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Target == null || !Target.isActive || dead == null || dead == Target || dead.IsEnemy != Target.IsEnemy) return;
            int power = 100 + Mathf.RoundToInt(Target.GetBaseInt() * 1.4f);
            foreach (Unit enemy in AztecCombat.Enemies(Target))
            {
                int damage = Mathf.Max(1, Target.SkillDamage(power, BaseEnums.PrimaryStat.INT));
                enemy.TakeDamage(new DamageContext(Target, damage, BaseEnums.CodeType.Passive,
                    new List<int> { DamageTag.MultiTarget, DamageTag.Special, DamageTag.NonContactAttack, DamageTag.AdditionalAttack }));
            }
        }
    }

    internal sealed class HumanSacrificeEffect : BaseEffect
    {
        private int _stacks;
        public HumanSacrificeEffect() : base(0) { }
        public override void OnApply() => Unit.AnyUnitDied += OnAnyUnitDied;
        public override void OnRemove() => Unit.AnyUnitDied -= OnAnyUnitDied;

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Target == null || !Target.isActive || dead == null || dead.IsEnemy == Target.IsEnemy) return;
            _stacks++;
            Target.RefreshAttributes();
        }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR ? 1f + _stacks * 0.05f : 1f;
    }

    internal sealed class DreamlikeResonanceEffect : BaseEffect, IResonanceConMultiplier
    {
        public float ResonanceConMultiplier { get; }
        public DreamlikeResonanceEffect(float multiplier) : base(0, multiplier) => ResonanceConMultiplier = multiplier;
    }

    internal sealed class DendroAffinityEffect : BaseEffect
    {
        public DendroAffinityEffect() : base(0) { }
        private bool Active => Target != null && Target.HasCombatElement(BaseEnums.UnitElement.Dendro);
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.CON && Active ? 2 : 0;
        public override void OnOwnerTurn()
        {
            if (!Active || !Target.isActive) return;
            Target.ModifyHp(Target.HpCurr + Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * 0.01f)), Target);
        }
    }

    internal sealed class NobleBodyEffect : BaseEffect
    {
        private readonly int _priority;
        public NobleBodyEffect(int priority) : base(0, priority) => _priority = priority;
        public override int TargetPriorityAdditiveModifier(Unit unit) => unit == Target ? _priority : 0;
    }

    internal sealed class TezcatlipocaDivinityEffect : BaseEffect
    {
        public TezcatlipocaDivinityEffect() : base(0) { }
        public override void OnApply() => Unit.AnyUnitDied += OnAnyUnitDied;
        public override void OnRemove() => Unit.AnyUnitDied -= OnAnyUnitDied;
        public override float MaxHpMultiplierModifier(Unit unit)
            => unit == Target ? 1f + Mathf.Max(0, Target.GetBaseInt()) * 0.01f : 1f;

        private void OnAnyUnitDied(Unit dead, Unit attacker)
        {
            if (Target == null || !Target.isActive || dead == null || dead.IsEnemy != Target.IsEnemy || !AztecCombat.IsJaguar(dead)) return;
            Target.AddUltimateResource(2);
        }
    }

    internal sealed class AztecFrontLineDamageReductionEffect : BaseEffect
    {
        private readonly Unit _tezcatlipoca;
        private readonly float _perFront;
        public AztecFrontLineDamageReductionEffect(Unit tezcatlipoca, float perFront) : base(0, perFront)
        { _tezcatlipoca = tezcatlipoca; _perFront = perFront; }

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || _tezcatlipoca == null || !_tezcatlipoca.isActive) return 1f;
            int frontCount = AztecCombat.AlliesIncludingSelf(_tezcatlipoca).Count(AztecCombat.IsFrontLine);
            return Mathf.Max(0.1f, 1f - frontCount * _perFront);
        }
    }
}
