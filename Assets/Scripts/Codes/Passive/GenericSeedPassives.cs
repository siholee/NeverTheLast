using System;
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
    public static class GenericSeedCodeIds
    {
        public const int RuinFormation = 1430;
        public const int ApocalypseFormation = 1431;
        public const int FrostCore = 1432;
        public const int SelfDestruct = 1433;
        public const int GreaterExplosion = 1434;
        public const int Overload = 1435;
        public const int FrostRay = 1436;
    }

    public static class GenericSeedStatusIds
    {
        public const int Formation = 7860;
        public const int FrostCore = 7861;
        public const int SelfDestruct = 7862;
        public const int GreaterExplosion = 7863;
        public const int Overload = 7864;
        public const int Overdrive = 7865;
        public const int FrostRay = 7866;
        public const int Slow = 7867;
    }

    /// <summary>전열이면 CON, 후열이면 DEX를 레벨에 비례해 얻는다.</summary>
    public sealed class GenericSeedFormation : PersistentStatusPassive
    {
        private readonly int _perLevel;

        public GenericSeedFormation(PassiveCodeContext context, int perLevel)
            : base(context, GenericSeedStatusIds.Formation, "generic_seed_formation", "가변 장갑진",
                $"전열에서는 레벨×{perLevel} CON, 후열에서는 레벨×{perLevel} DEX를 얻습니다.")
        {
            _perLevel = Mathf.Max(1, perLevel);
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new GenericSeedFormationEffect(_perLevel);
    }

    internal sealed class GenericSeedFormationEffect : BaseEffect
    {
        private readonly int _perLevel;
        public GenericSeedFormationEffect(int perLevel) : base(0, perLevel) => _perLevel = perLevel;

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || unit.currentCell == null || GridManager.Instance == null) return 0;
            int amount = Mathf.Max(1, unit.Level) * _perLevel;
            if (unit.currentCell.xPos == GridManager.Instance.GetFrontColumn(unit.IsEnemy))
                return stat == BaseEnums.PrimaryStat.CON ? amount : 0;
            if (unit.currentCell.xPos == GridManager.Instance.GetRearColumn(unit.IsEnemy))
                return stat == BaseEnums.PrimaryStat.DEX ? amount : 0;
            return 0;
        }
    }

    /// <summary>
    /// 혹한의 동력로 — 눈을 직접 깔고 그 위에서 DEX +30%, CON +15%.
    ///
    /// 혹한의 노심과 같은 이유로 테마 태그 판정을 판 판정으로 옮겼다.
    /// 거인이 없는 슬롯에서도 혼자 눈을 만들 수 있어야 이 씨앗이 엘리트 값을 한다.
    /// </summary>
    public sealed class FrostSeedCore : PersistentStatusPassive
    {
        /// <summary>자기 턴마다 새로 매기는 눈의 지속.</summary>
        public const int SnowTurns = 2;

        public FrostSeedCore(PassiveCodeContext context)
            : base(context, GenericSeedStatusIds.FrostCore, "frost_seed_core", "혹한의 동력로",
                "턴 시작 시 눈 필드를 깝니다. 눈 필드에 있는 동안 DEX가 30%, CON이 15% 증가합니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new FrostSeedCoreEffect();
    }

    internal sealed class FrostSeedCoreEffect : BaseEffect
    {
        public FrostSeedCoreEffect() : base(0) { }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;
            Combat.Battlefield.Set(Combat.FieldKind.Snow, Target, FrostSeedCore.SnowTurns);
        }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || !Combat.Battlefield.Is(Combat.FieldKind.Snow)) return 1f;
            return stat switch
            {
                BaseEnums.PrimaryStat.DEX => 1.30f,
                BaseEnums.PrimaryStat.CON => 1.15f,
                _ => 1f,
            };
        }
    }

    /// <summary>치명 피해 시 적 진영 중앙에서 최대 체력 비례 고정 범위 피해를 가하고 파괴된다.</summary>
    public sealed class SeedSelfDestruct : PersistentStatusPassive
    {
        private readonly float _maxHpRatio;
        private readonly bool _healingReduction;

        public SeedSelfDestruct(PassiveCodeContext context, bool enhanced)
            : base(context,
                enhanced ? GenericSeedStatusIds.GreaterExplosion : GenericSeedStatusIds.SelfDestruct,
                enhanced ? "seed_greater_explosion" : "seed_self_destruct",
                enhanced ? "대폭발" : "자폭",
                enhanced
                    ? "치명 피해 시 적 전체에 최대 체력의 16% 고정 피해를 주고 치유량 감소를 3턴 부여한 뒤 파괴됩니다."
                    : "치명 피해 시 적 전체에 최대 체력의 8% 고정 피해를 주고 파괴됩니다.")
        {
            // 20%/40%는 후열을 그냥 지웠다 — 대폭발 하나가 비CON 아군 최대 체력의 2배를 넘었다.
            // 8%/16%는 <b>방어막으로 받아 내는 크기</b>다. 대폭발 한 방이 후열 체력의 85~90%라
            // 맨몸이면 빈사가 되고 방어막 한 겹이면 넘어간다. 일반 씨앗은 셋이 동시에 터져야
            // 위험해지므로 한 마리씩 끊는 쪽이 여전히 정답이다.
            _maxHpRatio = enhanced ? 0.16f : 0.08f;
            _healingReduction = enhanced;
            if (enhanced) Grade = BaseEnums.CodeGrade.Enhanced;
            else SupersededByCodeId = GenericSeedCodeIds.GreaterExplosion;
        }

        protected override BaseEffect CreateInitialEffect()
            => new SeedSelfDestructEffect(_maxHpRatio, _healingReduction, CodeName);
    }

    /// <summary>
    /// 자폭 본체.
    ///
    /// <b>죽음의 유언은 <c>OnDeath</c>로 받는다.</b> 예전에는 <c>TryPreventDeath</c>에서 터뜨리고
    /// false를 돌려주었는데, 그 훅은 <b>처음 true를 반환한 효과에서 순회가 멈춘다.</b>
    /// 자폭보다 뒤에 있는 효과가 사망을 막으면 터지고도 살아남는 조합이 생긴다.
    ///
    /// 폭발 범위는 기획의 "적 진영 중간으로 이동해 범위 피해"를 적 전체로 읽었다.
    /// 한 진영이 여덟 칸이라 중앙 기준 9칸 범위는 결국 전원을 덮기 때문이다.
    /// </summary>
    internal sealed class SeedSelfDestructEffect : BaseEffect
    {
        private const int HealingReductionTurns = 3;

        private readonly float _maxHpRatio;
        private readonly bool _healingReduction;
        private readonly string _sourceName;
        private Action<EventContext> _handler;
        private bool _triggered;

        public SeedSelfDestructEffect(float maxHpRatio, bool healingReduction, string sourceName)
            : base(0, maxHpRatio)
        {
            _maxHpRatio = maxHpRatio;
            _healingReduction = healingReduction;
            _sourceName = sourceName;
        }

        public override void OnApply()
        {
            if (Target == null) return;
            _triggered = false;
            _handler = OnDeath;
            Target.AddListener(BaseEnums.UnitEventType.OnDeath, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _handler);
            _handler = null;
        }

        private void OnDeath(EventContext context)
        {
            Unit self = Target;
            if (_triggered || self == null || context?.Grantee != self) return;
            _triggered = true;

            int damage = Mathf.Max(1, Mathf.RoundToInt(self.HpMax * _maxHpRatio));
            List<Unit> targets = Combat.CombatTargets.AliveEnemies(self);

            Debug.Log($"[{_sourceName}] {self.UnitName}이(가) 적 진영 중앙에서 폭발합니다({damage}).");
            foreach (Unit target in targets)
            {
                // 고정피해는 방어력 감쇠만 무시한다. 내구는 관통 빌드에 대한 최후의 완충재이므로
                // 기획에 없는 DurabilityPenetration을 붙이지 않는다.
                target.TakeDamage(new DamageContext(
                    self, damage, BaseEnums.CodeType.Passive,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.AdditionalAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                        DamageTag.TrueDamage,
                    }));

                if (_healingReduction && target.isActive)
                    HealingReductionStatus.Apply(target, self, HealingReductionTurns, _sourceName);
            }
        }
    }

    /// <summary>체력 30% 이하에서 과부하 궁극기의 DEX 증가량을 5%+20%로 만든다.</summary>
    public sealed class SeedOverload : PersistentStatusPassive
    {
        public SeedOverload(PassiveCodeContext context)
            : base(context, GenericSeedStatusIds.Overload, "seed_overload", "오버로드",
                "체력이 30% 이하일 때 과부하 궁극기의 DEX 증가량이 25%가 됩니다.") { }

        protected override BaseEffect CreateInitialEffect() => new MarkerBuffEffect();
    }

    /// <summary>얼음 원소를 부착하는 씨앗 궁극기에 2턴 둔화를 추가한다.</summary>
    public sealed class SeedFrostRay : PersistentStatusPassive
    {
        public SeedFrostRay(PassiveCodeContext context)
            : base(context, GenericSeedStatusIds.FrostRay, "seed_frost_ray", "서리광선",
                "얼음 원소를 부착할 때 대상에게 2턴 둔화를 부여합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new MarkerBuffEffect();
    }

    public sealed class SeedSlowEffect : BaseEffect
    {
        public SeedSlowEffect() : base(0, 2f / 3f) => Category = BaseEnums.EffectCategory.Negative;

        public override float ActionSpeedModifier(Unit unit, float calculatedSpeed)
            => unit == Target ? 1f + (calculatedSpeed - 1f) * (2f / 3f) : calculatedSpeed;
    }
}
