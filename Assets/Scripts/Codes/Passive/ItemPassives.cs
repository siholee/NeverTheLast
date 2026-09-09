using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Codes.Passive
{
    internal static class ItemPassiveIds
    {
        public const int Khopesh = 6400;
        public const int YasakaniMagatama = 6401;
        public const int YataMirror = 6402;
        public const int MinotaurHorn = 6403;
        public const int GoldenApple = 6404;
        public const int GoldenBeastShield = 6405;
        public const int AriadneThread = 6406;
        public const int OuroborosBranch = 6407;
        public const int AegeusSword = 6408;
        public const int PeriphetesClub = 6409;
        public const int Jambiya = 6410;
        public const int AlamutFortress = 6411;
        public const int AugusteWatch = 6412;
    }

    /// <summary>장비를 벗으면 자신이 만든 상시 상태도 함께 걷어 내는 아이템 패시브 공통형.</summary>
    public abstract class ItemStatusPassive : PassiveCode
    {
        protected abstract string StatusKey { get; }

        protected ItemStatusPassive(PassiveCodeContext context, string codeName) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = codeName;
            IgnoresActivationChance = true;
            IgnoresCodeCapacity = true;
            Transferable = false;
        }

        protected void AddPermanentStatus(int id, BaseEffect effect, string description)
        {
            Caster?.AddStatus(BuffStatus.Create(
                id, StatusKey, CodeName, Caster, Caster, effect,
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: description));
        }

        public override void StopCode()
        {
            Caster?.RemoveStatusByKey(StatusKey);
        }
    }

    /// <summary>코페쉬 — 일반행동 적중마다 2턴 동안 DEX +8, 최대 4중첩.</summary>
    public sealed class KhopeshItemPassive : ItemStatusPassive
    {
        private const string BuffKey = "item_khopesh_dex";
        protected override string StatusKey => BuffKey;
        private Action<EventContext> _hitHandler;
        private Action<EventContext> _cleanupHandler;

        public KhopeshItemPassive(PassiveCodeContext context) : base(context, "코페쉬") { }

        public override void CastCode()
        {
            if (Caster == null || _hitHandler != null) return;
            _hitHandler = _ => AddStack();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        private void AddStack()
        {
            if (Caster == null) return;
            int stacks = 1;
            UnitStatus current = Caster.GetStatus(ItemPassiveIds.Khopesh);
            KhopeshDexEffect currentEffect = current?.Effects
                .Select(instance => instance.EffectObject)
                .OfType<KhopeshDexEffect>()
                .FirstOrDefault();
            if (currentEffect != null) stacks = Mathf.Min(4, currentEffect.Stacks + 1);

            Caster.AddStatus(BuffStatus.Create(
                ItemPassiveIds.Khopesh, BuffKey, CodeName,
                Caster, Caster, new KhopeshDexEffect(stacks),
                duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"DEX +{stacks * 8} ({stacks}/4중첩, 2턴)."));
        }

        public override void StopCode()
        {
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _hitHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _hitHandler = null;
            _cleanupHandler = null;
            base.StopCode();
        }
    }

    internal sealed class KhopeshDexEffect : BaseEffect
    {
        public int Stacks { get; }
        public KhopeshDexEffect(int stacks) : base(0, stacks) => Stacks = Mathf.Clamp(stacks, 1, 4);
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX ? Stacks * 8 : 0;
    }

    /// <summary>야사카니의 곡옥 — 궁극기 사용 후 4턴 동안 DEX +25.</summary>
    public sealed class YasakaniMagatamaItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_yasakani_dex";
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;

        public YasakaniMagatamaItemPassive(PassiveCodeContext context) : base(context, "야사카니의 곡옥") { }

        public override void CastCode()
        {
            if (Caster == null || _ultimateHandler != null) return;
            _ultimateHandler = _ => Caster.AddStatus(BuffStatus.Create(
                ItemPassiveIds.YasakaniMagatama, StatusKey, CodeName,
                Caster, Caster, new FlatDexEffect(25),
                duration: 4,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "DEX +25 (4턴)."));
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        public override void StopCode()
        {
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _ultimateHandler = null;
            _cleanupHandler = null;
            base.StopCode();
        }
    }

    internal sealed class FlatDexEffect : BaseEffect
    {
        private readonly int _amount;
        public FlatDexEffect(int amount) : base(0, amount) => _amount = amount;
        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == BaseEnums.PrimaryStat.DEX ? _amount : 0;
    }

    public sealed class YataMirrorItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_yata_mirror";
        public YataMirrorItemPassive(PassiveCodeContext context) : base(context, "야타의 거울") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.YataMirror, new YataReviveEffect(),
            "전투당 1회 치명 피해를 막고 2턴 경직 후 최대 체력으로 부활합니다.");
    }

    internal sealed class YataReviveEffect : BaseEffect
    {
        private const int StaggerTurns = 2;
        private bool _used;
        private int _remaining;

        public YataReviveEffect() : base(0) { }

        public override bool TryPreventDeath(Unit unit, Unit attacker)
        {
            if (_used || unit == null || unit != Target) return false;
            _used = true;
            _remaining = StaggerTurns;
            unit.AddUntargetableSource();
            unit.ControlStarts(new ControlContext(attacker, StaggerTurns));
            return true;
        }

        public override void OnOwnerTurn()
        {
            if (_remaining <= 0 || Target == null || !Target.isActive) return;
            _remaining--;
            if (_remaining > 0) return;
            Target.RemoveUntargetableSource();
            if (Target.isControlled) Target.ControlEnds();
            Target.ModifyHp(Target.HpMax, Target);
        }

        public override void OnRemove()
        {
            if (_remaining <= 0 || Target == null) return;
            _remaining = 0;
            Target.RemoveUntargetableSource();
            if (Target.isControlled) Target.ControlEnds();
        }
    }

    public sealed class MinotaurHornItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_minotaur_horn";
        public MinotaurHornItemPassive(PassiveCodeContext context) : base(context, "미노타우로스의 뿔") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.MinotaurHorn, new MinotaurHornEffect(),
            "물리 피해를 25% 덜 받고 짐승 속성을 얻습니다.");
    }

    internal sealed class MinotaurHornEffect : BaseEffect
    {
        public MinotaurHornEffect() : base(0) { }
        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target && context?.DamageTags?.Contains(DamageTag.Physical) == true ? 0.75f : 1f;
        public override bool GrantsUnitTag(Unit unit, string tag)
            => unit == Target && string.Equals(tag, "Beast", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class GoldenAppleItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_golden_apple";
        public GoldenAppleItemPassive(PassiveCodeContext context) : base(context, "황금 사과") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.GoldenApple, new OwnedSummonDamageEffect(1.4f),
            "자신이 소환한 소환수의 피해가 40% 증가합니다.");
    }

    internal sealed class OwnedSummonDamageEffect : BaseEffect
    {
        private readonly float _multiplier;
        public OwnedSummonDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float SummonDamageMultiplierModifier(Unit unit) => unit == Target ? _multiplier : 1f;
    }

    public sealed class GoldenBeastShieldItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_golden_beast_shield";
        public GoldenBeastShieldItemPassive(PassiveCodeContext context) : base(context, "금빛 짐승의 가죽 방패") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.GoldenBeastShield, new DurabilityMultiplierEffect(1.2f),
            "내구가 20% 증가합니다.");
    }

    internal sealed class DurabilityMultiplierEffect : BaseEffect
    {
        private readonly float _multiplier;
        public DurabilityMultiplierEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float DurabilityMultiplierModifier(Unit unit) => unit == Target ? _multiplier : 1f;
    }

    public sealed class AriadneThreadItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_ariadne_thread";
        public AriadneThreadItemPassive(PassiveCodeContext context) : base(context, "아리아드네의 실타래") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.AriadneThread, new AttackTriggeredHealingEffect(1.25f),
            "공격으로 자신을 회복할 때 회복량이 25% 증가합니다.");
    }

    public sealed class OuroborosBranchItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_ouroboros_branch";
        public OuroborosBranchItemPassive(PassiveCodeContext context) : base(context, "우로보로스의 가지") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.OuroborosBranch, new OutgoingHealingEffect(1.3f),
            "자신이 행하는 회복 효과가 30% 증가합니다.");
    }

    internal sealed class OutgoingHealingEffect : BaseEffect
    {
        private readonly float _multiplier;
        public OutgoingHealingEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;
        public override float OutgoingHealingMultiplierModifier(Unit source, Unit target)
            => source == Target ? _multiplier : 1f;
    }

    public sealed class AegeusSwordItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_aegeus_sword";
        public AegeusSwordItemPassive(PassiveCodeContext context) : base(context, "에게우스의 검") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.AegeusSword, new CritMultiplierBonusEffect(0.5f),
            "치명타 피해가 50% 증가합니다.");
    }

    /// <summary>오귀스트의 시계 — 보유자가 부여하는 모든 유한 턴 상태의 지속시간 +1.</summary>
    public sealed class AugusteWatchItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_auguste_watch";
        public AugusteWatchItemPassive(PassiveCodeContext context) : base(context, "오귀스트의 시계") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.AugusteWatch, new GrantedStatusDurationEffect(1),
            "부여하는 유한 턴 상태의 지속시간이 1턴 증가합니다.");
    }

    internal sealed class GrantedStatusDurationEffect : BaseEffect
    {
        private readonly int _turns;
        public GrantedStatusDurationEffect(int turns) : base(0, turns) => _turns = turns;

        public override int GrantedStatusDurationAdditiveModifier(Unit source, UnitStatus status)
            => source == Target && status != null && status.Duration > 0 ? _turns : 0;
    }

    public sealed class PeriphetesClubItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_periphetes_club";
        public PeriphetesClubItemPassive(PassiveCodeContext context) : base(context, "페리페테스의 몽둥이") { }
        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.PeriphetesClub, new TaggedDamageEffect(DamageTag.Slash, 1.25f),
            "베기 공격으로 가하는 피해가 25% 증가합니다.");
    }

    internal sealed class TaggedDamageEffect : BaseEffect
    {
        private readonly int _tag;
        private readonly float _multiplier;
        public TaggedDamageEffect(int tag, float multiplier) : base(0, multiplier)
        {
            _tag = tag;
            _multiplier = multiplier;
        }

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target && context?.DamageTags?.Contains(_tag) == true ? _multiplier : 1f;
    }

    /// <summary>잠비야 — 물리·접촉 공격 적중 시 대상 최대 체력의 2% 고정 피해.</summary>
    public sealed class JambiyaItemPassive : PassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;

        public JambiyaItemPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "잠비야";
            IgnoresActivationChance = true;
            IgnoresCodeCapacity = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _damageHandler != null) return;
            _damageHandler = OnDamageDealt;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
        }

        private void OnDamageDealt(DamageResolvedContext context)
        {
            Unit target = context?.Target;
            List<int> tags = context?.DamageContext?.DamageTags;
            if (target == null || !target.isActive || context.DamageDealt <= 0 ||
                tags == null || !tags.Contains(DamageTag.Physical) ||
                !tags.Contains(DamageTag.ContactAttack) || tags.Contains(DamageTag.TrueDamage)) return;

            int damage = Mathf.Max(1, Mathf.RoundToInt(target.HpMax * 0.02f));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.TrueDamage,
                }));
        }

        public override void StopCode()
        {
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDamageDealt, _damageHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _damageHandler = null;
            _cleanupHandler = null;
        }
    }

    /// <summary>알라무트 요새 — 처치 또는 강인도 파괴 시 다음 행동을 즉시 얻는다.</summary>
    public sealed class AlamutFortressItemPassive : PassiveCode
    {
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public AlamutFortressItemPassive(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "알라무트 요새";
            IgnoresActivationChance = true;
            IgnoresCodeCapacity = true;
            Transferable = false;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            Unit.AnyUnitDied += OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void OnAnyUnitDied(Unit fallen, Unit attacker)
        {
            if (Caster == null || !Caster.isActive || attacker != Caster || fallen == null) return;
            Managers.GameManager.Instance?.ActionScheduler.AdvanceAction(Caster, 1f);
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Unit.AnyUnitDied -= OnAnyUnitDied;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _cleanupHandler = null;
            _registered = false;
        }
    }
}
