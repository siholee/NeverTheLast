using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Normal
{
    public enum AztecNormalStyle
    {
        Jaguar,
        EliteJaguar,
        Eagle,
        EliteEagle,
        SerpentPriest,
        EliteSerpentPriest,
        Tezcatlipoca,
    }

    /// <summary>새 메히코 테마의 일반행동. 수치는 고정 위력 + 지정 스탯×계수 형식이다.</summary>
    public sealed class AztecNormal : BaseNormalCode
    {
        private readonly AztecNormalStyle _style;

        public AztecNormal(NormalCodeContext context, AztecNormalStyle style) : base(context)
        {
            _style = style;
            CodeName = style == AztecNormalStyle.Tezcatlipoca ? "전열 칙령" : "일반행동";
            CastingDelay = style == AztecNormalStyle.Tezcatlipoca ? 0f : 0.4f;
            MaxStage = 1;

            (int flat, float coefficient, BaseEnums.PrimaryStat stat) = PowerOf(style);
            Power = flat;
            PowerStatCoefficient = coefficient;
            PowerStat = stat;
        }

        private static (int, float, BaseEnums.PrimaryStat) PowerOf(AztecNormalStyle style) => style switch
        {
            AztecNormalStyle.Jaguar => (80, 0.4f, BaseEnums.PrimaryStat.STR),
            AztecNormalStyle.EliteJaguar => (100, 0.6f, BaseEnums.PrimaryStat.STR),
            AztecNormalStyle.Eagle => (80, 0.6f, BaseEnums.PrimaryStat.DEX),
            AztecNormalStyle.EliteEagle => (100, 0.6f, BaseEnums.PrimaryStat.DEX),
            AztecNormalStyle.SerpentPriest => (0, 0.6f, BaseEnums.PrimaryStat.INT),
            AztecNormalStyle.EliteSerpentPriest => (0, 0.6f, BaseEnums.PrimaryStat.INT),
            _ => (0, 0f, BaseEnums.PrimaryStat.INT),
        };

        private bool IsJaguar => _style is AztecNormalStyle.Jaguar or AztecNormalStyle.EliteJaguar;
        private bool IsEagle => _style is AztecNormalStyle.Eagle or AztecNormalStyle.EliteEagle;
        private bool IsSerpent => _style is AztecNormalStyle.SerpentPriest or AztecNormalStyle.EliteSerpentPriest;

        public override void CastCode()
        {
            if (_style == AztecNormalStyle.Tezcatlipoca)
            {
                CastTezcatlipocaCommand();
                return;
            }
            base.CastCode();
        }

        public override bool HasValidTarget()
        {
            if (_style == AztecNormalStyle.Tezcatlipoca) return FrontLineAlly() != null;
            if (IsSerpent) return SacrificeTarget() != null;
            return base.HasValidTarget();
        }

        protected override List<Unit> SelectTarget()
        {
            if (!IsSerpent) return base.SelectTarget();
            Unit target = SacrificeTarget();
            return target == null ? new List<Unit>() : new List<Unit> { target };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            BaseEnums.PrimaryStat stat = IsEagle ? BaseEnums.PrimaryStat.DEX :
                IsSerpent ? BaseEnums.PrimaryStat.INT : BaseEnums.PrimaryStat.STR;
            return Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(CurrentPower, stat) * critMultiplier));
        }

        protected override List<int> GetDamageTags()
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack };
            tags.Add(IsSerpent ? DamageTag.Special : DamageTag.Physical);
            tags.Add(IsJaguar ? DamageTag.ContactAttack : DamageTag.NonContactAttack);
            return tags;
        }

        private Unit SacrificeTarget()
        {
            return AztecCombat.AlliesIncludingSelf(Caster)
                .Where(unit => unit != Caster && unit.currentCell != null && unit.currentCell.yPos > 0)
                .OrderBy(unit => unit.HpCurr)
                .FirstOrDefault();
        }

        private Unit FrontLineAlly()
        {
            return AztecCombat.AlliesIncludingSelf(Caster)
                .Where(unit => unit != Caster && AztecCombat.IsFrontLine(unit) && !unit.isControlled && !unit.isCasting)
                .OrderByDescending(unit => AztecCombat.IsJaguar(unit))
                .ThenByDescending(unit => unit.Priority)
                .FirstOrDefault();
        }

        private void CastTezcatlipocaCommand()
        {
            Unit target = FrontLineAlly();
            if (target == null) return;

            target.AddStatus(BuffStatus.Create(
                7650, $"tezcatlipoca_command_{Caster.GetEntityId()}", "전열 칙령",
                Caster, target, new OutgoingDamageMultiplierEffect(1.5f),
                duration: 1,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "1턴간 주는 피해가 50% 증가합니다."));

            if (AztecCombat.IsJaguar(target)) Caster.AddUltimateResource(1);

            Managers.GameManager.Instance?.ActionScheduler.EnqueueAdditional(
                target,
                $"tezcatlipoca_command_{Caster.GetEntityId()}_{target.GetEntityId()}",
                "테스카틀리포카의 칙령",
                target.CastNormalCode);

            Caster.isCasting = false;
            NotifyActionResolved();
        }
    }
}
