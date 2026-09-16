using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Combat;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    public static class GenericGiantStatusIds
    {
        public const int Bulwark = 7850;
        public const int FrostCore = 7851;
        public const int Shatter = 7852;
        public const int StunningBlow = 7853;
        public const int CoupDeGrace = 7854;
        public const int Adaptability = 7855;
        public const int ShatteredArmor = 7856;
        public const int Permafrost = 7857;
        public const int FrozenSowing = 7858;
    }

    /// <summary>파종이 세우는 씨앗을 바꾸는 쪽이 다는 표식.</summary>
    internal interface IFrostSowingUpgrade
    {
        int SeedEnemyId { get; }
    }

    /// <summary>파멸·종말의 거인 — 전투 시작 시 최대 체력 비례 방어막.</summary>
    public sealed class GenericGiantBulwark : PersistentStatusPassive
    {
        private readonly float _maxHpRatio;

        public GenericGiantBulwark(PassiveCodeContext context, float maxHpRatio)
            : base(context, GenericGiantStatusIds.Bulwark, "generic_giant_bulwark", "거인의 방벽",
                $"전투 시작 시 최대 체력의 {maxHpRatio * 100f:F0}%만큼 방어막을 얻습니다.")
        {
            _maxHpRatio = maxHpRatio;
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new GiantBulwarkEffect(_maxHpRatio);
    }

    internal sealed class GiantBulwarkEffect : BaseEffect
    {
        private readonly float _ratio;
        public GiantBulwarkEffect(float ratio) : base(0, ratio) => _ratio = ratio;

        public override void OnApply()
        {
            if (Target == null) return;
            Target.AddShield(Mathf.Max(1, Mathf.RoundToInt(Target.HpMax * _ratio)), Caster ?? Target);
        }
    }

    /// <summary>
    /// 혹한의 거인 — 자기 턴마다 <b>눈을 직접 깔고</b> 얼음을 부착한다.
    /// 눈 위에서 얼음을 두르고 있으면 STR·CON 레벨 성장량이 1.5배가 된다.
    ///
    /// 예전에는 테마 태그 <c>Snow</c>를 봤다. 그 태그를 단 테마가 하나도 없어 <b>한 번도 켜진 적이 없었고</b>,
    /// 태그는 판이 아니라 스테이지에 붙는 것이라 플레이어가 손댈 수 없는 조건이기도 했다.
    /// 지금은 자기가 깐 판을 본다 — 수리야가 햇빛으로 덮으면 그대로 꺼지고,
    /// 물을 걸어 빙결로 얼음을 태우면 부착 조건이 깨진다. <b>두 갈래 다 플레이어의 답이다.</b>
    /// </summary>
    public sealed class FrostGiantCore : PersistentStatusPassive
    {
        /// <summary>자기 턴마다 새로 매기는 눈의 지속.</summary>
        public const int SnowTurns = 2;

        public FrostGiantCore(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.FrostCore, "frost_giant_core", "혹한의 노심",
                "턴 시작 시 눈 필드를 깔고 자신에게 얼음 원소를 부착합니다. " +
                "얼음이 부착된 채 눈 필드에 있으면 STR·CON의 레벨 성장량이 1.5배가 됩니다.")
        {
            IsUniquePassive = true;
            Transferable = false;
        }

        protected override BaseEffect CreateInitialEffect() => new FrostGiantCoreEffect();
    }

    internal sealed class FrostGiantCoreEffect : BaseEffect
    {
        public FrostGiantCoreEffect() : base(0) { }

        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            // 판을 자기 기준으로 새로 매긴다. 쓰러지면 Battlefield가 스스로 걷는다.
            Battlefield.Set(FieldKind.Snow, Target, FrostGiantCore.SnowTurns);
            Target.GrantCombatElement(BaseEnums.UnitElement.Cryo, Unit.CommonElementAuraDuration, Caster ?? Target);
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
        {
            if (unit != Target || (stat != BaseEnums.PrimaryStat.STR && stat != BaseEnums.PrimaryStat.CON)) return 0;
            if (!unit.HasAttachedElement(BaseEnums.UnitElement.Cryo)) return 0;
            if (!Battlefield.Is(FieldKind.Snow)) return 0;
            return Mathf.RoundToInt(unit.GetLevelGrowthStatValue(stat) * 0.5f);
        }
    }

    public sealed class GiantShatter : PersistentStatusPassive
    {
        public GiantShatter(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.Shatter, "giant_shatter", "파쇄",
                "단일 대상 궁극기 적중 후 대상의 방어력이 3턴 동안 20% 감소합니다.")
        {
            SupersededByCodeId = VoidPrismCodeIds.DestructionRay;
        }

        protected override BaseEffect CreateInitialEffect() => new GiantShatterEffect();
    }

    internal sealed class GiantShatterEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;
        public GiantShatterEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _handler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            DamageContext damage = context?.DamageContext;
            if (context?.Attacker != Target || context.Target == null || context.DamageDealt <= 0 ||
                damage?.CodeType != BaseEnums.CodeType.Ultimate ||
                damage.DamageTags?.Contains(DamageTag.SingleTarget) != true) return;

            context.Target.AddStatus(BuffStatus.Create(
                GenericGiantStatusIds.ShatteredArmor, "giant_shattered_armor", "파쇄 — 방어 붕괴",
                Target, context.Target, new ArmorShredEffect(0.8f),
                duration: 3,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "방어력이 20% 감소합니다."));
        }
    }

    public sealed class GiantStunningBlow : PersistentStatusPassive
    {
        public GiantStunningBlow(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.StunningBlow, "giant_stunning_blow", "강타",
                "단일 대상 궁극기 적중 후 대상을 1턴 기절시킵니다.") { }

        protected override BaseEffect CreateInitialEffect() => new GiantStunningBlowEffect();
    }

    internal sealed class GiantStunningBlowEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _handler;
        public GiantStunningBlowEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _handler = OnDamageDealt;
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _handler);
            _handler = null;
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            DamageContext damage = context?.DamageContext;
            if (context?.Attacker != Target || context.Target == null || context.DamageDealt <= 0 ||
                damage?.CodeType != BaseEnums.CodeType.Ultimate ||
                damage.DamageTags?.Contains(DamageTag.SingleTarget) != true) return;
            ControlStatuses.ApplyFixedStun(context.Target, Target, 1);
        }
    }

    /// <summary>행동불능 적을 치명타로 공격할 때 치명타 배율 자체에 +0.4.</summary>
    public sealed class GiantCoupDeGrace : PersistentStatusPassive
    {
        public GiantCoupDeGrace(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.CoupDeGrace, "giant_coup_de_grace", "병상첨병",
                "행동불능 상태의 적을 공격할 때 치명타 피해가 40% 증가합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new GiantCoupDeGraceEffect();
    }

    internal sealed class GiantCoupDeGraceEffect : BaseEffect
    {
        public GiantCoupDeGraceEffect() : base(0, 0.4f) { }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || !target.isControlled || context?.IsCrit != true) return 1f;
            float normalCrit = Mathf.Max(1f, attacker.CritMultiplierCurr);
            return (normalCrit + 0.4f) / normalCrit;
        }
    }

    public sealed class GiantAdaptability : PersistentStatusPassive
    {
        public GiantAdaptability(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.Adaptability, "giant_adaptability", "적응력",
                "물리·접촉 기술에 적중당할 때마다 이번 전투 동안 STR이 1% 증가합니다.") { }

        protected override BaseEffect CreateInitialEffect() => new GiantAdaptabilityEffect();
    }

    internal sealed class GiantAdaptabilityEffect : BaseEffect
    {
        private Action<EventContext> _handler;
        private int _stacks;
        public GiantAdaptabilityEffect() : base(0) { }

        public override void OnApply()
        {
            if (Target == null) return;
            _stacks = 0;
            _handler = OnAfterDamageTaken;
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override void OnRemove()
        {
            Target?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _handler = null;
            _stacks = 0;
        }

        private void OnAfterDamageTaken(EventContext context)
        {
            DamageContext damage = context?.DmgCtx;
            List<int> tags = damage?.DamageTags;
            if (context?.Grantee != Target || damage == null || damage.ResolvedDamage <= 0 || tags == null ||
                !tags.Contains(DamageTag.Physical) || !tags.Contains(DamageTag.ContactAttack)) return;
            _stacks++;
            Target.RefreshAttributes();
        }

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.STR ? 1f + _stacks * 0.01f : 1f;
    }

    /// <summary>
    /// 영구동토 — 체력 칸이 깨질 때마다 후열에 씨앗을 세운다.
    ///
    /// <b>3슬롯을 10슬롯으로 되돌리는 장치다.</b> 플레이어는 이미 배웠다 — 씨앗을 한꺼번에
    /// 쓸면 자폭이 겹쳐 후열이 죽는다. 그런데 보스를 빨리 깎을수록 칸이 빨리 깨지고 씨앗이
    /// 더 자주 선다. <b>급하게 미는 것이 곧 벌인</b> 구조이며, 새 규칙은 하나도 없다.
    ///
    /// 소환 체계를 쓰지 않고 <c>SpawnUnit</c>으로 진짜 적을 세운다. 그래야 자폭(1433)까지
    /// 데이터 그대로 따라온다 — 3슬롯에서 본 그 씨앗이 맞아야 학습이 이어진다.
    /// </summary>
    public sealed class GiantPermafrost : PersistentStatusPassive
    {
        /// <summary>칸 하나가 깨질 때 세우는 수.</summary>
        public const int SeedsPerBreak = 2;

        /// <summary>기본으로 세우는 씨앗. 얼어붙은 파종이 있으면 그쪽이 덮는다.</summary>
        public const int DefaultSeedId = 1061;

        public GiantPermafrost(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.Permafrost, "giant_permafrost", "영구동토",
                "체력 칸이 깨질 때마다 적 후열에 공허의 씨앗을 2기 세웁니다.")
        {
        }

        protected override BaseEffect CreateInitialEffect() => new GiantPermafrostEffect(SeedsPerBreak);
    }

    internal sealed class GiantPermafrostEffect : BaseEffect
    {
        /// <summary>체력 칸을 나누는 코드가 없을 때 쓰는 기본 칸 수.</summary>
        private const int FallbackSegments = 2;

        private readonly int _seedsPerBreak;
        private Action<EventContext> _handler;
        private int _brokenSegments;

        public GiantPermafrostEffect(int seedsPerBreak) : base(0, seedsPerBreak)
            => _seedsPerBreak = seedsPerBreak;

        public override void OnApply()
        {
            if (Target == null) return;
            _brokenSegments = 0;
            _handler = _ => CheckBreak();
            Target.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
        }

        public override void OnRemove()
        {
            if (Target == null || _handler == null) return;
            Target.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _handler);
            _handler = null;
        }

        private void CheckBreak()
        {
            if (Target == null || !Target.isActive || Target.HpMax <= 0) return;

            int segments = Mathf.Max(FallbackSegments, Target.HpSegmentCount);
            // 남은 칸 수를 세어, 지난번보다 더 깨졌을 때만 심는다.
            int broken = segments - Mathf.CeilToInt(Target.HpCurr / (Target.HpMax / (float)segments));
            broken = Mathf.Clamp(broken, 0, segments - 1);
            if (broken <= _brokenSegments) return;

            _brokenSegments = broken;
            Sow();
        }

        private void Sow()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            int seedId = ResolveSeedId();
            int rear = grid.GetRearColumn(true);
            int planted = 0;

            for (int y = 1; y <= 4 && planted < _seedsPerBreak; y++)
            {
                if (!grid.IsCellAvailable(rear, y)) continue;
                if (grid.SpawnUnit(rear, y, true, seedId) != null) planted++;
            }

            if (planted > 0)
            {
                Debug.Log($"[영구동토] {Target.UnitName}의 칸이 깨져 씨앗 {planted}기가 솟았다");
            }
        }

        /// <summary>얼어붙은 파종이 서 있으면 혹한의 씨앗으로 바뀐다.</summary>
        private int ResolveSeedId()
        {
            if (Target == null) return GiantPermafrost.DefaultSeedId;

            foreach (var status in Target.ActiveStatuses)
            {
                foreach (var effect in status.Effects)
                {
                    if (effect.EffectObject is IFrostSowingUpgrade upgrade) return upgrade.SeedEnemyId;
                }
            }

            return GiantPermafrost.DefaultSeedId;
        }
    }

    /// <summary>얼어붙은 파종 — 영구동토가 세우는 씨앗이 혹한의 씨앗이 된다.</summary>
    public sealed class GiantFrozenSowing : PersistentStatusPassive
    {
        /// <summary>혹한의 씨앗. 엘리트 등급이라 같은 두 기로도 판이 크게 달라진다.</summary>
        public const int FrostSeedId = 2033;

        public GiantFrozenSowing(PassiveCodeContext context)
            : base(context, GenericGiantStatusIds.FrozenSowing, "giant_frozen_sowing", "얼어붙은 파종",
                "영구동토가 세우는 씨앗이 혹한의 씨앗이 됩니다.")
        {
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        protected override BaseEffect CreateInitialEffect() => new GiantFrozenSowingEffect(FrostSeedId);
    }

    internal sealed class GiantFrozenSowingEffect : BaseEffect, IFrostSowingUpgrade
    {
        public GiantFrozenSowingEffect(int seedEnemyId) : base(0, seedEnemyId) => SeedEnemyId = seedEnemyId;

        public override bool IsBeneficial => true;

        public int SeedEnemyId { get; }
    }
}
