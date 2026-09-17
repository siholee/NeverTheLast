using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Passive
{
    // ══════════════════════════════════════════════════════════════
    // 일본 동부 전선 장비 패시브(433~442).
    // 433~437은 츠쿠요미·스사노오·세이메이 전용 장비이고 주인이 덱에 있을 때만 보상에 뜬다.
    // 438~442는 천공·해안 전선 적이 떨구는 T3다. 규격은 Detail_14 §6.F.
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 쿠사나기노츠루기 — 풀베기(433). 에어본 상태의 적에게 피해를 주면 그 주변 3×3의 다른 적에게
    /// 같은 피해의 40%를 한 번 더 준다. 불길을 풀째 베어 낸 검이라 떠 있는 적 곁을 함께 벤다.
    /// 번진 피해는 다시 번지지 않는다.
    /// </summary>
    public sealed class KusanagiItemPassive : ItemStatusPassive
    {
        private const float SplashRatio = 0.4f;
        private static bool _resolving;
        private Action<DamageResolvedContext> _dealtHandler;

        protected override string StatusKey => "item_kusanagi";

        public KusanagiItemPassive(PassiveCodeContext context) : base(context, "풀베기") { }

        public override void CastCode()
        {
            if (Caster == null || _dealtHandler != null) return;
            _dealtHandler = OnDamageDealt;
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            AddPermanentStatus(ItemPassiveIds.Kusanagi, new MarkerBuffEffect(),
                "에어본 상태의 적에게 피해를 주면 주변 3×3의 다른 적에게 그 피해의 40%를 줍니다.");
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            if (_resolving || context == null || context.Attacker != Caster || context.DamageDealt <= 0) return;
            if (context.DamageContext?.CodeType == BaseEnums.CodeType.Effect) return;
            Unit target = context.Target;
            if (target == null || !target.IsAirborne) return;

            int splash = Mathf.Max(1, Mathf.RoundToInt(context.DamageDealt * SplashRatio));
            _resolving = true;
            try
            {
                foreach (Unit other in SkyFrontline.SplashAround(Caster, target))
                {
                    other.TakeDamage(new DamageContext(
                        Caster, splash, BaseEnums.CodeType.Effect,
                        new List<int> { DamageTag.MultiTarget, DamageTag.Physical, DamageTag.NonContactAttack }));
                }
            }
            finally
            {
                _resolving = false;
            }
        }

        public override void StopCode()
        {
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
            _dealtHandler = null;
            base.StopCode();
        }
    }

    /// <summary>도츠카노츠루기 — 취한 뱀 베기(434). 행동불능(기절·빙결·에어본) 상태의 적에게 주는 피해 +25%.</summary>
    public sealed class TotsukaItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_totsuka";
        public TotsukaItemPassive(PassiveCodeContext context) : base(context, "취한 뱀 베기") { }

        public override void CastCode() => AddPermanentStatus(ItemPassiveIds.Totsuka, new TargetConditionDamageEffect(
                target => target.isControlled, 1.25f),
            "행동불능 상태의 적에게 주는 피해가 25% 증가합니다.");
    }

    /// <summary>
    /// 아메노누보코 — 창세의 휘저음(435). 적에게 거는 해로운 상태의 지속시간 +1턴.
    /// <b>행동불능(빙결·기절·에어본)은 늘리지 않는다</b> — 제어 길이는 분쇄 규칙과 함께 무한 제어를 막는 축이다.
    /// </summary>
    public sealed class AmenonuhokoItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_amenonuhoko";
        public AmenonuhokoItemPassive(PassiveCodeContext context) : base(context, "창세의 휘저음") { }

        public override void CastCode() => AddPermanentStatus(ItemPassiveIds.Amenonuhoko, new HarmfulDurationEffect(),
            "적에게 거는 해로운 상태의 지속시간이 1턴 증가합니다. 행동불능은 늘어나지 않습니다.");
    }

    internal sealed class HarmfulDurationEffect : BaseEffect
    {
        public HarmfulDurationEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;

        public override int GrantedStatusDurationAdditiveModifier(Unit source, UnitStatus status)
        {
            if (source != Target || status == null || status.Duration <= 0) return 0;
            if (status.Category != BaseEnums.StatusCategory.Negative) return 0;
            if (status.StatusId is ControlStatuses.FrozenStatusId or ControlStatuses.StunStatusId
                or ControlStatuses.AirborneStatusId) return 0;
            return 1;
        }
    }

    /// <summary>마스미노카가미 — 맑은 달거울(436). 지속피해 상태의 적에게 주는 피해 +20%.</summary>
    public sealed class MasumiMirrorItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_masumi_mirror";
        public MasumiMirrorItemPassive(PassiveCodeContext context) : base(context, "맑은 달거울") { }

        public override void CastCode() => AddPermanentStatus(ItemPassiveIds.Masumi, new TargetConditionDamageEffect(
                target => target.HasDamageOverTimeStatus(), 1.2f),
            "지속피해 상태의 적에게 주는 피해가 20% 증가합니다.");
    }

    /// <summary>
    /// 육임식반 — 점술반(437). 착용자가 새 행동불능을 걸 때마다 마나 +15. 한 프레임(한 행동)에 한 번만.
    /// 분쇄·면역으로 무효가 된 시도에는 주지 않는다 — 분쇄 규칙이 곧 이 회복의 상한이다.
    /// </summary>
    public sealed class ShikibanItemPassive : ItemStatusPassive
    {
        private const int ManaPerControl = 15;
        private Action<Unit, Unit> _controlHandler;
        private Action<EventContext> _cleanupHandler;
        private int _lastFrame = -1;

        protected override string StatusKey => "item_shikiban";
        public ShikibanItemPassive(PassiveCodeContext context) : base(context, "점술반") { }

        public override void CastCode()
        {
            if (Caster == null || _controlHandler != null) return;
            _controlHandler = OnControlApplied;
            ControlStatuses.ControlApplied += _controlHandler;
            AddPermanentStatus(ItemPassiveIds.Shikiban, new MarkerBuffEffect(),
                "새 행동불능을 걸 때마다 마나를 15 회복합니다(행동당 1회).");
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        private void OnControlApplied(Unit source, Unit target)
        {
            if (source != Caster || Caster == null || !Caster.isActive) return;
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;
            Caster.RecoverMana(ManaPerControl);
        }

        private void RemoveCleanup()
        {
            if (Caster != null && _cleanupHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _cleanupHandler = null;
        }

        public override void StopCode()
        {
            if (_controlHandler != null) ControlStatuses.ControlApplied -= _controlHandler;
            _controlHandler = null;
            RemoveCleanup();
            base.StopCode();
        }
    }

    /// <summary>고치 실 반지 — 고치 실(438). 같은 대상을 연달아 공격하면 두 번째부터 피해 +15%. 대상을 바꾸면 끊긴다.</summary>
    public sealed class CocoonThreadItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_cocoon_thread";
        public CocoonThreadItemPassive(PassiveCodeContext context) : base(context, "고치 실") { }

        public override void CastCode() => AddPermanentStatus(ItemPassiveIds.CocoonThread, new CocoonThreadEffect(),
            "같은 대상을 연달아 공격하면 두 번째 타격부터 피해가 15% 증가합니다.");
    }

    internal sealed class CocoonThreadEffect : BaseEffect
    {
        private Action<DamageResolvedContext> _dealtHandler;
        private Unit _lastTarget;

        public CocoonThreadEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;
        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            if (Target == null) return;
            _dealtHandler = context =>
            {
                if (context?.Attacker == Target && context.DamageContext?.CodeType != BaseEnums.CodeType.Effect)
                    _lastTarget = context.Target;
            };
            Target.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);
        }

        public override void OnRemove() => Target?.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _dealtHandler);

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target == _lastTarget &&
               context?.CodeType != BaseEnums.CodeType.Effect ? 1.15f : 1f;
    }

    /// <summary>불씨 인분 단검 — 불씨 인분(439). 일반행동 적중 시 25% 확률로 화상 1턴(CON 대결).</summary>
    public sealed class EmberScaleItemPassive : ItemStatusPassive
    {
        private const float Chance = 0.25f;
        private Action<EventContext> _hitHandler;

        protected override string StatusKey => "item_ember_scale";
        public EmberScaleItemPassive(PassiveCodeContext context) : base(context, "불씨 인분") { }

        public override void CastCode()
        {
            if (Caster == null || _hitHandler != null) return;
            _hitHandler = context =>
            {
                Unit target = context?.Grantor;
                if (target == null || !target.isActive || UnityEngine.Random.value > Chance) return;
                ElementalReaction.TryApplyBurn(Caster, target, 1);
            };
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            AddPermanentStatus(ItemPassiveIds.EmberScale, new MarkerBuffEffect(),
                "일반행동 적중 시 25% 확률로 화상 1턴을 시도합니다.");
        }

        public override void StopCode()
        {
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            _hitHandler = null;
            base.StopCode();
        }
    }

    /// <summary>
    /// 포식자의 깃털 망토 — 포식자의 깃털(440). 착용자의 타격이 횟수제 무적에 막힐 때마다 DEX +3%,
    /// 5중첩까지 전투 끝까지 남는다. 무적을 벗기는 동안 손이 빨라진다.
    /// </summary>
    public sealed class PredatorCloakItemPassive : ItemStatusPassive
    {
        private Action<Unit, Unit> _nullifyHandler;
        private Action<EventContext> _cleanupHandler;
        protected override string StatusKey => "item_predator_cloak";
        public PredatorCloakItemPassive(PassiveCodeContext context) : base(context, "포식자의 깃털") { }

        public override void CastCode()
        {
            if (Caster == null || _nullifyHandler != null) return;
            PredatorCloakEffect effect = new PredatorCloakEffect();
            AddPermanentStatus(ItemPassiveIds.PredatorCloak, effect,
                "타격이 무적에 막힐 때마다 DEX가 3% 증가합니다(최대 5중첩, 전투 끝까지).");
            _nullifyHandler = (attacker, _) =>
            {
                if (attacker != Caster || effect.Stacks >= PredatorCloakEffect.MaxStacks) return;
                effect.Stacks++;
                Caster.RefreshAttributes();
            };
            Unit.AnyHitNullified += _nullifyHandler;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        private void RemoveCleanup()
        {
            if (Caster != null && _cleanupHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _cleanupHandler = null;
        }

        public override void StopCode()
        {
            if (_nullifyHandler != null) Unit.AnyHitNullified -= _nullifyHandler;
            _nullifyHandler = null;
            RemoveCleanup();
            base.StopCode();
        }
    }

    internal sealed class PredatorCloakEffect : BaseEffect
    {
        public const int MaxStacks = 5;
        public int Stacks;

        public PredatorCloakEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;
        public override bool IsBeneficial => true;

        public override float PrimaryStatMultiplierModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX ? 1f + 0.03f * Stacks : 1f;
    }

    /// <summary>상어 이빨 단검 — 상어 이빨(441). 보호막을 가진 적에게 주는 피해 +20%.</summary>
    public sealed class SharkToothItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_shark_tooth";
        public SharkToothItemPassive(PassiveCodeContext context) : base(context, "상어 이빨") { }

        public override void CastCode() => AddPermanentStatus(ItemPassiveIds.SharkTooth, new TargetConditionDamageEffect(
                target => target.ShieldCurr > 0, 1.2f),
            "보호막을 가진 적에게 주는 피해가 20% 증가합니다.");
    }

    /// <summary>
    /// 따개비 박힌 등갑 — 따개비 등갑(442). 전투당 1회, 체력이 50% 아래로 떨어지면 최대 체력 15% 기준 보호막.
    /// 해일 같은 전체 공격 뒤에 한 번 버틸 몫을 준다.
    /// </summary>
    public sealed class BarnacleShellItemPassive : ItemStatusPassive
    {
        private Action<EventContext> _damageHandler;
        private bool _used;

        protected override string StatusKey => "item_barnacle_shell";
        public BarnacleShellItemPassive(PassiveCodeContext context) : base(context, "따개비 등갑") { }

        public override void CastCode()
        {
            if (Caster == null || _damageHandler != null) return;
            _used = false;
            _damageHandler = _ =>
            {
                if (_used || Caster == null || !Caster.isActive || Caster.HpCurr <= 0) return;
                if (Caster.HpCurr * 2 > Caster.HpMax) return;
                _used = true;
                Caster.AddShield(Mathf.Max(1, Mathf.RoundToInt(Caster.HpMax * 0.15f)), Caster);
            };
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            AddPermanentStatus(ItemPassiveIds.BarnacleShell, new MarkerBuffEffect(),
                "전투당 1회, 체력이 50% 아래로 떨어지면 최대 체력 15%의 보호막을 얻습니다.");
        }

        public override void StopCode()
        {
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            _damageHandler = null;
            base.StopCode();
        }
    }

    /// <summary>대상 조건을 만족하는 적에게 주는 피해 배율. 한 줄짜리 장비 효과들이 같은 모양을 공유한다.</summary>
    internal sealed class TargetConditionDamageEffect : BaseEffect
    {
        private readonly Func<Unit, bool> _condition;
        private readonly float _multiplier;

        public TargetConditionDamageEffect(Func<Unit, bool> condition, float multiplier) : base(0, multiplier)
        {
            _condition = condition;
            _multiplier = multiplier;
        }

        public override bool CountsAsReagentBuff => false;
        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && target != null && target.IsEnemy != attacker.IsEnemy && _condition(target)
                ? _multiplier
                : 1f;
    }
}
