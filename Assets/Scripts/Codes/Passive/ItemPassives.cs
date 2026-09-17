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
        public const int SkilledChair = 6413;
        public const int FrostplateArmor = 6414;
        public const int Mjolnir = 6415;
        public const int SwordDance = 6416;
        public const int SnowTreader = 6417;
        public const int FleshRipper = 6418;
        public const int FrostscaleMail = 6419;
        public const int Lodbrok = 6420;
        public const int DulledCrystal = 6421;
        public const int Kusanagi = 6422;
        public const int Totsuka = 6423;
        public const int Amenonuhoko = 6424;
        public const int Masumi = 6425;
        public const int Shikiban = 6426;
        public const int CocoonThread = 6427;
        public const int EmberScale = 6428;
        public const int PredatorCloak = 6429;
        public const int SharkTooth = 6430;
        public const int BarnacleShell = 6431;
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

    /// <summary>
    /// 세이드스타프르 — 숙련된 의자.
    ///
    /// <b>자신이 아닌 아군</b>을 치유할 때마다 CON이 1씩 영구히 쌓인다.
    /// 자기 자신을 빼 둔 것은 자기 치유로 혼자 불어나는 길을 막기 위해서다.
    /// CON은 최대 체력이자 치유량의 축이라, 남을 살릴수록 더 잘 살리게 된다.
    /// </summary>
    public sealed class SkilledChairItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_skilled_chair";
        public SkilledChairItemPassive(PassiveCodeContext context) : base(context, "숙련된 의자") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.SkilledChair, new HealOtherStatGrowthEffect(BaseEnums.PrimaryStat.CON, 1),
            "자신이 아닌 아군을 치유할 때마다 CON이 1씩 증가합니다.");
    }

    /// <summary>다른 아군을 치유할 때마다 지정한 스탯이 쌓인다.</summary>
    internal sealed class HealOtherStatGrowthEffect : BaseEffect
    {
        private readonly BaseEnums.PrimaryStat _stat;
        private readonly int _amountPerHeal;
        private int _accumulated;

        public HealOtherStatGrowthEffect(BaseEnums.PrimaryStat stat, int amountPerHeal) : base(0, amountPerHeal)
        {
            _stat = stat;
            _amountPerHeal = Mathf.Max(1, amountPerHeal);
        }

        public override bool IsBeneficial => true;

        public override void OnHealingOrShieldGranted(Unit source, Unit target)
        {
            if (source != Target || target == null || target == source) return;

            _accumulated += _amountPerHeal;
            source.RefreshDerivedAttributes();
        }

        public override int PrimaryStatAdditiveModifier(Unit unit, BaseEnums.PrimaryStat stat)
            => unit == Target && stat == _stat ? _accumulated : 0;
    }

    /// <summary>
    /// 혹한의 갑주 — 전장 상태가 주는 불리한 배율을 무시한다.
    ///
    /// 눈밭에서 불·풀이 20%를 잃는 것을 없던 일로 만든다. 유리한 배율은 그대로 받으므로
    /// 얼음 유닛이 입어도 손해가 없다. 노르드 3부작 내내 눈이 깔리는 판에서
    /// <b>불 딜러를 데려갈 수 있게 하는 유일한 길</b>이다.
    /// </summary>
    public sealed class FrostplateArmorItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_frostplate_armor";
        public FrostplateArmorItemPassive(PassiveCodeContext context) : base(context, "혹한의 갑주") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.FrostplateArmor, new FieldPenaltyImmunityEffect(),
            "전장 상태가 주는 불리한 효과를 받지 않습니다.");
    }

    internal sealed class FieldPenaltyImmunityEffect : BaseEffect
    {
        public FieldPenaltyImmunityEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override bool IgnoresFieldPenalty(Unit unit) => unit == Target;
    }

    /// <summary>
    /// 묠니르 — 번개를 부착하는 공격은 확정 치명타가 되고 방어력을 2턴간 20% 깎는다.
    ///
    /// 치명타 확률은 <c>AttributesUpdate</c> 시점에 한 번 굳으므로 대상별 조건을 담을 수 없다.
    /// 잔의 `원소 친화 - 얼음`과 같은 방식으로, 피해를 계산하는 자리에서 대상을 보고
    /// 치명타가 아니었다면 배율을 보정해 결과적으로 확정 치명타로 만든다.
    ///
    /// 판정은 <b>부착 직후</b>가 아니라 <b>부착 직전</b>이다. 번개를 거는 공격이 곧 조건이므로
    /// 이미 번개를 두른 대상을 다시 치는 것으로는 켜지지 않는다.
    /// </summary>
    public sealed class MjolnirItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_mjolnir";
        public MjolnirItemPassive(PassiveCodeContext context) : base(context, "묠니르") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.Mjolnir, new MjolnirEffect(),
            "번개 원소를 부여하는 공격이 확정 치명타가 되고 대상의 방어력을 2턴간 20% 감소시킵니다.");
    }

    internal sealed class MjolnirEffect : BaseEffect
    {
        private const float ArmorMultiplier = 0.80f;
        private const int ArmorBreakTurns = 2;
        private const int StatusId = 8025;

        private Action<Unit, Unit, BaseEnums.UnitElement> _grantHandler;

        public MjolnirEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override void OnApply()
        {
            _grantHandler = OnElementGranted;
            Unit.AnyCombatElementGranted += _grantHandler;
        }

        public override void OnRemove()
        {
            if (_grantHandler == null) return;
            Unit.AnyCombatElementGranted -= _grantHandler;
            _grantHandler = null;
        }

        /// <summary>
        /// 확정 치명타. 치명타 판정은 이미 끝났으므로 배율 쪽에서 보정한다.
        /// 치명타가 아니었던 공격에만 모자란 만큼을 채워 결과를 같게 만든다.
        /// </summary>
        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || context == null || context.IsCrit) return 1f;
            if (!GrantsElectro(context)) return 1f;
            return Mathf.Max(1f, attacker.CritMultiplierCurr);
        }

        /// <summary>
        /// 번개를 거는 공격인지는 <b>대상에게 아직 번개가 붙어 있지 않은가</b>로 가른다.
        /// 부착은 피해 뒤에 오므로 피해 계산 시점에는 아직 붙기 전이다.
        /// </summary>
        private static bool GrantsElectro(DamageContext context)
            => context.DamageTags != null && context.DamageTags.Contains(DamageTag.Special);

        private void OnElementGranted(Unit source, Unit target, BaseEnums.UnitElement element)
        {
            if (source != Target || target == null || !target.isActive) return;
            if (element != BaseEnums.UnitElement.Electro) return;

            target.AddStatus(BuffStatus.Create(
                StatusId, "item_mjolnir_armor", "묠니르",
                source, target, new ArmorShredEffect(ArmorMultiplier),
                duration: ArmorBreakTurns,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                description: "방어력이 20% 감소합니다."));
        }
    }

    /// <summary>
    /// 칼춤 — 치명타 확률이 오른다.
    ///
    /// 발키리가 `예리함`(17)으로 100%를 넘긴 치명타 확률을 치명타 피해로 바꾸므로,
    /// 확률이 이미 가득 찬 뒤에도 이 장비가 죽지 않는다. 초과분을 쓰는 코드와 짝이다.
    /// </summary>
    public sealed class SwordDanceItemPassive : ItemStatusPassive
    {
        private const float CritChanceBonus = 0.15f;

        protected override string StatusKey => "item_sword_dance";
        public SwordDanceItemPassive(PassiveCodeContext context) : base(context, "칼춤") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.SwordDance, new CritChanceBonusEffect(CritChanceBonus),
            "치명타 확률이 15%p 증가합니다.");
    }

    internal sealed class CritChanceBonusEffect : BaseEffect
    {
        private readonly float _bonus;

        public CritChanceBonusEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override bool IsBeneficial => true;

        public override float CritChanceAdditiveModifier(Unit unit) => unit == Target ? _bonus : 0f;
    }

    /// <summary>
    /// 서리꾼의 장화 — 둔화에 걸리지 않는다.
    ///
    /// 둔화는 얼음이 두 번 붙어 일어나는 반응이라 노르드 판에서 저절로 쌓인다.
    /// DEX를 깎으므로 행동이 느려지는데, 자동 전투라 걸린 뒤에는 손쓸 수가 없다.
    /// 빙결이 아니라 둔화만 막는 것은 T3가 감당할 무게를 넘지 않기 위해서다.
    /// </summary>
    public sealed class SnowTreaderItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_snow_treader";
        public SnowTreaderItemPassive(PassiveCodeContext context) : base(context, "서리꾼의 장화") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.SnowTreader, new StatusKeyImmunityEffect("reaction_Slow"),
            "둔화에 걸리지 않습니다.");
    }

    /// <summary>특정 키의 해로운 상태만 막는다. 저항이 아니라 확정이다.</summary>
    internal sealed class StatusKeyImmunityEffect : BaseEffect
    {
        private readonly string _key;

        public StatusKeyImmunityEffect(string key) : base(0) => _key = key;

        public override bool IsBeneficial => true;

        public override float NegativeStatusResistanceChanceModifier(
            Unit unit, Entities.Status.UnitStatus status)
            => unit == Target && status != null && status.Key == _key ? 1f : 0f;
    }

    /// <summary>
    /// 살점 뜯개 — 처형선을 4%p 넓힌다.
    ///
    /// 덮어쓰지 않고 <b>더한다.</b> 천살성(8%)이면 12%, 당연한 운명(12%)이면 16%가 된다.
    /// 처형을 이미 들고 있어야 값을 하므로 처형 축을 고른 파티에만 붙는 보상이다.
    /// </summary>
    public sealed class FleshRipperItemPassive : ItemStatusPassive
    {
        private const float ThresholdBonus = 0.04f;

        protected override string StatusKey => "item_flesh_ripper";
        public FleshRipperItemPassive(PassiveCodeContext context) : base(context, "살점 뜯개") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.FleshRipper, new ExecuteThresholdBonusEffect(ThresholdBonus),
            "처형선이 4%p 증가합니다.");
    }

    internal sealed class ExecuteThresholdBonusEffect : BaseEffect
    {
        private readonly float _bonus;

        public ExecuteThresholdBonusEffect(float bonus) : base(0, bonus) => _bonus = bonus;

        public override bool IsBeneficial => true;

        public override float ExecuteThresholdAdditiveModifier(Unit unit)
            => unit == Target ? _bonus : 0f;
    }

    /// <summary>
    /// 서리 비늘 갑주 — 얼음이 부착된 동안 받는 물리 피해가 15% 줄어든다.
    ///
    /// 적이 얼음을 걸어 주는 테마라 조건이 저절로 채워진다. <b>디버프가 방어구의 조건이 되는 역전</b>이고,
    /// 초전도(전기+얼음)로 물리에 약해지는 판에서 정확히 반대 방향으로 선다.
    /// </summary>
    public sealed class FrostscaleMailItemPassive : ItemStatusPassive
    {
        private const float Multiplier = 0.85f;

        protected override string StatusKey => "item_frostscale_mail";
        public FrostscaleMailItemPassive(PassiveCodeContext context) : base(context, "서리 비늘 갑주") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.FrostscaleMail,
            new ElementGatedTaggedMitigationEffect(BaseEnums.UnitElement.Cryo, DamageTag.Physical, Multiplier),
            "얼음 원소가 부착된 동안 받는 물리 피해가 15% 감소합니다.");
    }

    /// <summary>지정한 원소를 두른 동안, 지정한 태그의 피해만 덜 받는다.</summary>
    internal sealed class ElementGatedTaggedMitigationEffect : BaseEffect
    {
        private readonly BaseEnums.UnitElement _element;
        private readonly int _tag;
        private readonly float _multiplier;

        public ElementGatedTaggedMitigationEffect(
            BaseEnums.UnitElement element, int tag, float multiplier) : base(0, multiplier)
        {
            _element = element;
            _tag = tag;
            _multiplier = multiplier;
        }

        public override bool IsBeneficial => true;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            if (unit != Target || context?.DamageTags == null) return 1f;
            if (!context.DamageTags.Contains(_tag)) return 1f;

            // 조건은 <b>부착</b>이다. 속성만으로는 켜지지 않는다 — 얼음 유닛이 공짜로 얻으면
            // 장비가 아니라 속성이 방어를 주는 꼴이 된다.
            return Target.HasAttachedElement(_element) ? _multiplier : 1f;
        }
    }

    /// <summary>
    /// 타르에 절인 털가죽 바지 — 독이 덜 밴다.
    ///
    /// 라그나르 로드브로크의 별명이 곧 이 물건이다. 뱀을 잡으러 갈 때 타르에 절여 입었다는
    /// 털가죽 바지가 독을 막아 주었다는 전승을 그대로 옮겼다.
    ///
    /// <b>지속피해 10% 경감, 치유량 감소 절반.</b> 둘은 같은 것의 앞뒤다 — 천천히 갉는 수단과
    /// 회복을 끊는 수단이라, 흡혈로 버티는 유닛을 무너뜨리는 정석 둘이 함께 무뎌진다.
    /// 막는 것이 아니라 무디게 하는 것이라, 지속피해 축이 통째로 죽지는 않는다.
    /// </summary>
    public sealed class LodbrokItemPassive : ItemStatusPassive
    {
        private const float DotMultiplier = 0.9f;
        private const float HealingReductionResistance = 0.5f;

        protected override string StatusKey => "item_lodbrok";
        public LodbrokItemPassive(PassiveCodeContext context) : base(context, "타르에 절인 털가죽 바지") { }

        public override void CastCode() => AddPermanentStatus(
            ItemPassiveIds.Lodbrok, new VenomWardEffect(DotMultiplier, HealingReductionResistance),
            "지속피해로 받는 피해가 10% 감소하고 치유량 감소량이 절반이 됩니다.");
    }

    /// <summary>지속피해를 덜 받고 치유량 감소를 덜어 낸다. 타르 바지가 쓰는 한 쌍.</summary>
    internal sealed class VenomWardEffect : BaseEffect
    {
        private readonly float _dotMultiplier;
        private readonly float _healingReductionResistance;

        public VenomWardEffect(float dotMultiplier, float healingReductionResistance) : base(0, dotMultiplier)
        {
            _dotMultiplier = dotMultiplier;
            _healingReductionResistance = healingReductionResistance;
        }

        public override bool IsBeneficial => true;

        /// <summary>지속피해는 태그 목록이 비어 있는 <see cref="BaseEnums.CodeType.Effect"/>다.</summary>
        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target && context != null && context.CodeType == BaseEnums.CodeType.Effect
                ? _dotMultiplier
                : 1f;

        public override float HealingReductionResistanceModifier(Unit unit)
            => unit == Target ? _healingReductionResistance : 0f;
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

    /// <summary>
    /// 무뎌진 결정(432) — 초반 적 전용 T1 무기가 준다. 착용자가 일으키는 해로운 원소 반응의 위력이 절반이 되고,
    /// 빙결·진동 기절·화상은 1턴으로 끝난다.
    ///
    /// 극초반(Lv40까지)의 파티는 반응 대책이 없는 경우가 많다. 반응을 없애지 않고 약하게만 남겨
    /// "적이 나를 얼린다"는 경험과 행동제어의 힌트는 그대로 준다. 적은 Lv41부터 온전한 무기로 갈아 든다.
    /// </summary>
    public sealed class DulledCrystalItemPassive : ItemStatusPassive
    {
        protected override string StatusKey => "item_dulled_crystal";

        public DulledCrystalItemPassive(PassiveCodeContext context) : base(context, "무뎌진 결정") { }

        public override void CastCode()
        {
            if (Caster == null || Caster.HasStatus(ItemPassiveIds.DulledCrystal)) return;
            AddPermanentStatus(ItemPassiveIds.DulledCrystal, new DulledCrystalEffect(),
                "일으키는 해로운 원소 반응의 위력이 50% 감소하고, 빙결·진동 기절·화상이 1턴으로 끝납니다.");
        }
    }

    internal sealed class DulledCrystalEffect : BaseEffect, Effects.Negative.IReactionDampener
    {
        public DulledCrystalEffect() : base(0) { }
        public override bool CountsAsReagentBuff => false;
        public float HarmfulReactionPotency => 0.5f;
        public int ReactionTurnCap => 1;
    }
}
