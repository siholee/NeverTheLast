using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>공허의 기사 N — 단일 적에게 90 + STR×0.8 위력의 접촉 물리 피해.</summary>
    public sealed class VoidKnightNormal : BaseNormalCode
    {
        public VoidKnightNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "공허의 검";
            Power = 90;
            PowerStatCoefficient = 0.8f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.35f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
        };
    }

    /// <summary>
    /// 공허의 사수 N — 단일 적에게 70 + DEX×0.7 위력의 비접촉 물리 피해.
    /// <b>적 후열을 먼저 노린다</b> — 앞줄이 막고 있어도 물러선 캐릭터를 먼저 문다.
    /// </summary>
    public sealed class VoidMarksmanNormal : BaseNormalCode
    {
        public VoidMarksmanNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "공허의 사격";
            Power = 70;
            PowerStatCoefficient = 0.7f;
            PowerStat = BaseEnums.PrimaryStat.DEX;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
        };

        protected override List<Unit> SelectTarget()
        {
            List<Unit> enemies = GetAvailableEnemies();
            if (enemies.Count == 0) return new List<Unit>();

            // 뒷줄(|x| == 2)이 하나라도 있으면 그쪽에서만 고른다.
            List<Unit> rear = enemies
                .Where(unit => unit.currentCell != null && Mathf.Abs(unit.currentCell.xPos) == 2)
                .ToList();
            Unit picked = CombatTargets.PickByPriority(rear.Count > 0 ? rear : enemies);
            return picked != null ? new List<Unit> { picked } : new List<Unit>();
        }
    }

    /// <summary>공허의 선봉대 N — 단일 적에게 80 + CON×0.5 위력의 접촉 물리 피해.</summary>
    public sealed class VoidVanguardNormal : BaseNormalCode
    {
        public VoidVanguardNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "방벽 타격";
            Power = 80;
            PowerStatCoefficient = 0.5f;
            PowerStat = BaseEnums.PrimaryStat.CON;
            CastingDelay = 0.4f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.CON) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };
    }

    /// <summary>
    /// 공허의 분쇄자 N — 단일 적에게 100 + STR×0.6 위력의 접촉 물리 피해.
    /// 대상이 보호막을 두르고 있으면 위력이 두 배가 된다 — 방어를 쌓는 편성을 정면으로 겨눈다.
    /// </summary>
    public sealed class VoidCrusherNormal : BaseNormalCode
    {
        private const float ShieldedMultiplier = 2f;

        public VoidCrusherNormal(NormalCodeContext context) : base(context)
        {
            CodeName = "분쇄 타격";
            Power = 100;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.45f;
            CodeTags = new List<int> { DamageTag.Physical, DamageTag.ContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
        {
            // 대상은 SelectTarget이 이미 정해 둔 현재 타겟을 본다.
            float shielded = Caster.currentNormalTarget != null && Caster.currentNormalTarget.ShieldCurr > 0
                ? ShieldedMultiplier
                : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier * shielded));
        }

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.SingleTarget, DamageTag.NormalAttack,
            DamageTag.Physical, DamageTag.ContactAttack,
        };
    }

    /// <summary>
    /// 공허의 용 N 브레스 — 적 전열 전체(비어 있으면 후열 전체)에 80 + INT×0.6 위력의 특수 피해.
    /// 팔레트 변형만 적중 후 자기 원소를 부착한다.
    /// </summary>
    public sealed class VoidDragonNormal : BaseNormalCode
    {
        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidDragonNormal(NormalCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context)
        {
            _attachedElement = attachedElement;
            CodeName = "브레스";
            Power = 80;
            PowerStatCoefficient = 0.6f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CastingDelay = 0.5f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            DamageTag.AllTarget, DamageTag.NormalAttack,
            DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override List<Unit> SelectTarget()
        {
            List<Unit> enemies = GetAvailableEnemies();
            if (enemies.Count == 0) return new List<Unit>();

            List<Unit> front = enemies
                .Where(unit => unit.currentCell != null && Mathf.Abs(unit.currentCell.xPos) == 1)
                .ToList();
            return front.Count > 0 ? front : enemies;
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (_attachedElement == BaseEnums.UnitElement.None || target == null || !target.isActive) return;
            target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
        }
    }

    /// <summary>
    /// 공허의 사슴 N — 단일 적에게 200 + STR×1.2 위력의 비접촉 특수 피해.
    /// 자기 원소를 부착하고, 행동을 마친 뒤 30% 확률로 <b>강화 일반행동</b>이 즉시 이어진다.
    /// 강화 일반행동은 서로 다른 세 대상에게 100 + STR×0.8씩 나눠 때린다.
    /// </summary>
    public sealed class VoidDeerNormal : BaseNormalCode
    {
        private const int BasePower = 200;
        private const float BaseCoefficient = 1.2f;
        private const int EmpoweredPower = 100;
        private const float EmpoweredCoefficient = 0.8f;
        private const float FollowUpChance = 0.30f;
        private const int EmpoweredTargets = 3;

        private readonly BaseEnums.UnitElement _attachedElement;
        private bool _empowered;

        public VoidDeerNormal(NormalCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context)
        {
            _attachedElement = attachedElement;
            CodeName = "사슴의 뿔";
            Power = BasePower;
            PowerStatCoefficient = BaseCoefficient;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CastingDelay = 0.5f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override int CalculateDamage(float critMultiplier)
            => Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

        protected override List<int> GetDamageTags() => new()
        {
            _empowered ? DamageTag.MultiTarget : DamageTag.SingleTarget,
            DamageTag.NormalAttack, DamageTag.Special, DamageTag.NonContactAttack,
        };

        protected override List<Unit> SelectTarget()
        {
            List<Unit> enemies = GetAvailableEnemies();
            if (enemies.Count == 0) return new List<Unit>();

            if (!_empowered)
            {
                Unit picked = CombatTargets.PickByPriority(enemies);
                return picked != null ? new List<Unit> { picked } : new List<Unit>();
            }
            return CombatTargets.PickByPriority(enemies, EmpoweredTargets);
        }

        protected override void OnAttackResolved(Unit target, DamageContext context)
        {
            if (_attachedElement != BaseEnums.UnitElement.None && target != null && target.isActive)
            {
                target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
            }

            // 강화 일반행동은 30% 확률로 <b>같은 행동 안에서</b> 이어진다.
            // 코루틴을 다시 태우면 행동 순서가 한 번 더 소비되므로, 여기서 즉시 해결한다.
            if (_empowered || Random.value >= FollowUpChance) return;
            FireEmpowered();
        }

        private void FireEmpowered()
        {
            _empowered = true;
            try
            {
                int power = EmpoweredPower + Mathf.RoundToInt(
                    Caster.GetBasePrimaryStat(BaseEnums.PrimaryStat.STR) * EmpoweredCoefficient);
                bool isCrit = Random.value <= Caster.CritChanceCurr;
                float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) * crit));

                var tags = new List<int>
                {
                    DamageTag.MultiTarget, DamageTag.NormalAttack,
                    DamageTag.Special, DamageTag.NonContactAttack,
                };
                foreach (Unit extra in CombatTargets.PickByPriority(GetAvailableEnemies(), EmpoweredTargets))
                {
                    extra.TakeDamage(new DamageContext(
                        Caster, damage, BaseEnums.CodeType.Normal, new List<int>(tags), isCrit));
                    if (_attachedElement != BaseEnums.UnitElement.None && extra.isActive)
                    {
                        extra.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
                    }
                }

                // 공허의 선봉장(1484)처럼 '강화 일반행동을 마칠 때'를 세는 코드가 이 신호를 본다.
                NotifyActionResolved();
            }
            finally
            {
                _empowered = false;
            }
        }
    }
}
