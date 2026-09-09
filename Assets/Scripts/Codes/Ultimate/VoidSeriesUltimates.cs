using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 공허의 기사 U 파쇄격 — 단일 적에게 130 + STR×1.2 위력의 접촉 물리 피해를 주고
    /// 실제로 입힌 피해의 30%만큼 자신에게 보호막을 두른다.
    /// 보호막은 곧 기사의 화력이므로(공허의 갑주) 궁극기가 곧 다음 화력이 된다.
    /// </summary>
    public sealed class VoidKnightUltimate : SimpleUltimate
    {
        private const float ShieldRatio = 0.3f;

        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidKnightUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "파쇄격", 4, 0.45f)
        {
            _attachedElement = attachedElement;
            Power = 130;
            PowerStatCoefficient = 1.2f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
            };
        }

        protected override void Resolve()
        {
            Unit target = CombatTargets.PickByPriority(Enemies());
            if (target == null) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * crit));

            var context = new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>(CodeTags), isCrit);
            target.TakeDamage(context);

            if (context.ResolvedDamage > 0)
            {
                Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(context.ResolvedDamage * ShieldRatio)), Caster);
            }
            if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
            {
                target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 공허의 사수 U 공허의 일격 — 단일 적에게 100 + DEX×1.0 위력의 비접촉 물리 피해.
    /// <b>내구를 무시한다</b> — 내구로 버티는 전열 뒤에 숨어도 소용이 없다.
    /// </summary>
    public sealed class VoidMarksmanUltimate : SimpleUltimate
    {
        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidMarksmanUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "공허의 일격", 4, 0.5f)
        {
            _attachedElement = attachedElement;
            Power = 100;
            PowerStatCoefficient = 1.0f;
            PowerStat = BaseEnums.PrimaryStat.DEX;
            CodeTags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.NonContactAttack,
                DamageTag.Arrow, DamageTag.DurabilityPenetration,
            };
        }

        protected override void Resolve()
        {
            List<Unit> enemies = Enemies();
            List<Unit> rear = enemies
                .Where(unit => unit.currentCell != null && Mathf.Abs(unit.currentCell.xPos) == 2)
                .ToList();
            Unit target = CombatTargets.PickByPriority(rear.Count > 0 ? rear : enemies);
            if (target == null) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.DEX) * crit));

            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>(CodeTags), isCrit));

            if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
            {
                target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 공허의 선봉대 U 방벽 전개 — 필드의 공허 괴수 아군 전체에게 CON×0.8 보호막.
    /// 피해를 주지 않으므로 <b>팔레트 변형이 따로 없다</b> — 부착할 대상이 없기 때문이다.
    /// </summary>
    public sealed class VoidVanguardUltimate : SimpleUltimate
    {
        private const float ShieldConCoefficient = 0.8f;

        public VoidVanguardUltimate(UltimateCodeContext context)
            : base(context, "방벽 전개", 4, 0.4f)
        {
            CodeTags = new List<int>();
        }

        protected override void Resolve()
        {
            int shield = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ShieldConCoefficient));
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)
                         .Where(unit => unit.HasUnitTag("VoidMonster")))
            {
                ally.AddShield(shield, Caster);
            }
        }
    }

    /// <summary>
    /// 공허의 분쇄자 U 분쇄 — 적 전체에게 150 + STR×1.2 위력의 접촉 물리 피해를 주고
    /// <b>보호막을 모두 걷어낸다.</b> 방어를 쌓아 버티는 편성을 정면으로 겨눈다.
    /// </summary>
    public sealed class VoidCrusherUltimate : SimpleUltimate
    {
        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidCrusherUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "분쇄", 4, 0.55f)
        {
            _attachedElement = attachedElement;
            Power = 150;
            PowerStatCoefficient = 1.2f;
            PowerStat = BaseEnums.PrimaryStat.STR;
            CodeTags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Physical, DamageTag.ContactAttack,
            };
        }

        protected override void Resolve()
        {
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * crit));

            foreach (Unit target in Enemies())
            {
                // 보호막을 먼저 걷어야 이번 타격이 체력에 그대로 들어간다.
                if (target.ShieldCurr > 0) target.RemoveShield(target.ShieldCurr);
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>(CodeTags), isCrit));

                if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
                {
                    target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 공허의 용 U 원소 붕괴 — 적 전체에게 120 + INT×1.4 위력의 비접촉 특수 피해.
    /// 대상이 <b>용과 같은 원소를 두르고 있으면 1.5배</b>가 된다.
    ///
    /// 일반행동 브레스가 자기 원소를 깔아 두므로, 용은 스스로 조건을 만들고 스스로 터뜨린다.
    /// 플레이어는 원소를 지우거나 덮어씌워 이 연결을 끊는다.
    /// </summary>
    public sealed class VoidDragonUltimate : SimpleUltimate
    {
        private const float MatchingElementMultiplier = 1.5f;

        private readonly BaseEnums.UnitElement _attachedElement;

        public VoidDragonUltimate(UltimateCodeContext context, BaseEnums.UnitElement attachedElement)
            : base(context, "원소 붕괴", 4, 0.6f)
        {
            _attachedElement = attachedElement;
            Power = 120;
            PowerStatCoefficient = 1.4f;
            PowerStat = BaseEnums.PrimaryStat.INT;
            CodeTags = new List<int>
            {
                DamageTag.AllTarget, DamageTag.UltAttack,
                DamageTag.Special, DamageTag.NonContactAttack,
            };
        }

        protected override void Resolve()
        {
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int baseDamage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * crit));

            foreach (Unit target in Enemies())
            {
                bool matched = _attachedElement != BaseEnums.UnitElement.None &&
                               target.HasCombatElement(_attachedElement);
                int damage = matched
                    ? Mathf.RoundToInt(baseDamage * MatchingElementMultiplier)
                    : baseDamage;

                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate, new List<int>(CodeTags), isCrit));

                if (_attachedElement != BaseEnums.UnitElement.None && target.isActive)
                {
                    target.GrantCombatElement(_attachedElement, Unit.CommonElementAuraDuration, Caster);
                }
            }
        }

        public override bool HasValidTarget() => base.HasValidTarget() && Enemies().Count > 0;
    }

    /// <summary>
    /// 공허의 사슴 U 무리 부르기 — 같은 원소의 늑대·멧돼지·괴조 중 하나를 <b>칸에 세운다.</b>
    ///
    /// 기억 정령(칸을 차지하지 않는 소환수)이 아니라 <b>진짜 유닛</b>이다. 그래서 빈 칸이
    /// 없으면 소환은 실패하고, 대기열에도 남지 않는다. 불러낸 개체는 사슴과 같은 레벨이며
    /// 사슴이 쓰러지면 함께 쓰러진다.
    /// </summary>
    public sealed class VoidDeerUltimate : SimpleUltimate
    {
        /// <summary>같은 테마 안에서 부를 수 있는 병종의 기본 ID. 여기에 원소 오프셋을 더한다.</summary>
        private static readonly int[] SummonBaseIds = { 1086, 1078, 1070 };   // 늑대 · 멧돼지 · 괴조

        private readonly int _elementOffset;

        public VoidDeerUltimate(UltimateCodeContext context, int elementOffset)
            : base(context, "무리 부르기", 5, 0.6f)
        {
            _elementOffset = Mathf.Clamp(elementOffset, 0, 7);
            CodeTags = new List<int>();
        }

        protected override void Resolve()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return;

            bool isEnemy = Caster.IsEnemy;
            int unitId = SummonBaseIds[Random.Range(0, SummonBaseIds.Length)] + _elementOffset;

            foreach (int xPos in new[] { grid.GetFrontColumn(isEnemy), grid.GetRearColumn(isEnemy) })
            {
                for (int yPos = grid.yMin; yPos <= grid.yMax; yPos++)
                {
                    if (!grid.IsCellAvailable(xPos, yPos)) continue;

                    Unit spawned = grid.SpawnUnit(xPos, yPos, isEnemy, unitId);
                    if (spawned == null) continue;

                    BindLifetime(spawned);
                    spawned.Invoke(BaseEnums.UnitEventType.OnRoundStart, new EventContext(spawned));
                    Debug.Log($"[무리 부르기] {Caster.UnitName}이(가) {spawned.UnitName}을(를) ({xPos}, {yPos})에 세웠다.");
                    return;
                }
            }

            Debug.Log($"[무리 부르기] 빈 칸이 없어 {Caster.UnitName}의 소환이 실패했다.");
        }

        /// <summary>
        /// 사슴이 쓰러지면 함께 쓰러진다. 칸을 쓰는 진짜 유닛이라 기억 정령의
        /// <c>RegisterSummon</c> 경로를 쓸 수 없어(그쪽은 칸 없는 유닛을 전제한다) 사망만 연결한다.
        /// </summary>
        private void BindLifetime(Unit spawned)
        {
            Unit owner = Caster;
            System.Action<EventContext> handler = null;
            handler = _ =>
            {
                owner.RemoveListener(BaseEnums.UnitEventType.OnDeath, handler);
                if (spawned != null && spawned.isActive) spawned.Die(null);
            };
            owner.AddListener(BaseEnums.UnitEventType.OnDeath, handler);
        }
    }
}
