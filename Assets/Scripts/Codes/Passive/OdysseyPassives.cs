using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 오디세이 계열 상태 ID.
    ///
    /// 6500~6551은 사바흐·이카리아(6500~)·마리(6510~)·니콜(6520~)·잔(6530~)·베다 SP(6540~)·
    /// 수리야(6550~)가 이미 쓴다. 지금 비어 있는 첫 구간이 6560이다.
    /// </summary>
    internal static class OdysseyStatusIds
    {
        public const int Supple = 6560;
        public const int Fascination = 6561;
        public const int Methamphetamine = 6562;
        public const int WailingWall = 6563;
        public const int Overgrowth = 6564;
        public const int Vengeance = 6565;
        public const int EndlessHunger = 6566;
        public const int Meltdown = 6570;
        public const int Predation = 6571;
        public const int SirenChorus = 6572;
        public const int LookAtMe = 6573;
        public const int LifeEcho = 6574;
    }

    /// <summary>오디세이 계열 공용 상수.</summary>
    public static class OdysseyCombat
    {
        /// <summary>세이렌의 돌림노래 중첩. 행동 제어를 당하면 비워진다.</summary>
        public const string ChorusResource = "siren_chorus";

        /// <summary>돌림노래 한 중첩이 더하는 INT 배수.</summary>
        public const float ChorusIntPerStack = 0.8f;

        /// <summary>히포캅투스가 전투 내내 달고 있는 대상 우선도.</summary>
        public const int TauntPriority = 3;
    }

    // ── 공용 해금 패시브 ────────────────────────────────────────────

    /// <summary>
    /// 유연함(1900) — 접촉 공격으로 받는 피해를 줄인다.
    /// 지속피해는 태그 목록이 비어 있어 자동으로 빠진다(<c>ContactAttack</c>이 없으므로).
    /// </summary>
    public class OdysseySupple : PassiveCode
    {
        protected virtual float Reduction => 0.25f;
        protected virtual int StatusId => OdysseyStatusIds.Supple;
        protected virtual string StatusKey => "odyssey_supple";

        public OdysseySupple(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "유연함";
            IgnoresActivationChance = true;
            SupersededByCodeId = 1901;   // 매혹을 배우면 이쪽은 발동하지 않는다
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                StatusId, StatusKey, CodeName, Caster, Caster,
                new ContactDamageReductionEffect(Reduction),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"접촉 공격으로 받는 피해가 {Mathf.RoundToInt(Reduction * 100)}% 감소합니다."));
        }

        public override void StopCode() => Caster?.RemoveStatusByKey(StatusKey);
    }

    /// <summary>매혹(1901) — 유연함의 상위. 접촉 피해 감소가 두 배다.</summary>
    public sealed class OdysseyFascination : OdysseySupple
    {
        protected override float Reduction => 0.50f;
        protected override int StatusId => OdysseyStatusIds.Fascination;
        protected override string StatusKey => "odyssey_fascination";

        public OdysseyFascination(PassiveCodeContext context) : base(context)
        {
            CodeName = "매혹";
            Grade = BaseEnums.CodeGrade.Enhanced;
            SupersededByCodeId = 0;
        }
    }

    /// <summary>접촉 태그를 달고 들어온 피해만 줄인다.</summary>
    internal sealed class ContactDamageReductionEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ContactDamageReductionEffect(float reduction) : base(0, reduction)
            => _multiplier = 1f - Mathf.Clamp01(reduction);

        public override bool IsBeneficial => true;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
        {
            // 접촉 여부는 태그가 '있는지'로 본다. 지속피해는 태그가 비어 있어 걸리지 않는다.
            if (unit != Target || context?.DamageTags == null) return 1f;
            return context.DamageTags.Contains(DamageTag.ContactAttack) ? _multiplier : 1f;
        }
    }

    /// <summary>
    /// 메스암페타민(1902) — 자기과신의 강화 등급. 처치마다 특수 태그 피해가 늘고 중첩된다.
    /// </summary>
    public sealed class OdysseyMethamphetamine : PassiveCode
    {
        private const float BonusPerKill = 0.10f;
        private const string StatusKey = "odyssey_methamphetamine";

        public OdysseyMethamphetamine(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "메스암페타민";
            IgnoresActivationChance = true;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            // 은금 사다리는 자기과신(66) 쪽에 적혀 있다 — 여기서 걷어낼 것이 없다.
            Caster?.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.Methamphetamine, StatusKey, CodeName, Caster, Caster,
                new KillStackTaggedDamageEffect(DamageTag.Special, BonusPerKill),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "적을 처치할 때마다 특수 태그로 가하는 피해 +10%. 중첩됩니다."));
        }

        public override void StopCode() => Caster?.RemoveStatusByKey(StatusKey);
    }

    /// <summary>
    /// 통곡의 벽(1903) — 철벽의 상위. 일반행동을 할 때마다 이번 전투 동안 CON이 는다.
    /// </summary>
    public sealed class OdysseyWailingWall : PassiveCode
    {
        private const float ConPerAction = 0.02f;
        private const string StatusKey = "odyssey_wailing_wall";

        private Action<EventContext> _actionHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public OdysseyWailingWall(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "통곡의 벽";
            IgnoresActivationChance = true;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _actionHandler = _ => Grow();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, _actionHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
                Caster.RemoveStatusByKey(StatusKey);
            }
            _actionHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void Grow()
        {
            if (Caster == null || !Caster.isActive) return;

            Caster.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.WailingWall, StatusKey, CodeName, Caster, Caster,
                new PrimaryStatMultiplierEffect(1f + ConPerAction, BaseEnums.PrimaryStat.CON),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "일반행동을 할 때마다 이번 전투 동안 CON +2%. 중첩됩니다."));
        }
    }

    /// <summary>
    /// 과성장(1904) — 이 유닛이 일으킨 <b>활성</b> 반응의 디버프 몫을 두 배로 키운다.
    ///
    /// 🔸 원안의 '촉진'은 이 게임의 반응표에 없다. 풀 + 전기 조합의 약화 반응인
    /// <b>활성</b>(<see cref="ReactionId.Activation"/>)을 그것으로 본다 — Detail_16을 따른다.
    /// </summary>
    public sealed class OdysseyOvergrowth : PassiveCode
    {
        private const string StatusKey = "odyssey_overgrowth";

        public OdysseyOvergrowth(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "과성장";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.Overgrowth, StatusKey, CodeName, Caster, Caster,
                new ActivationAmplifierEffect(2f),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "자신이 일으킨 활성 반응의 약화량이 두 배가 됩니다."));
        }

        public override void StopCode() => Caster?.RemoveStatusByKey(StatusKey);
    }

    /// <summary>활성 반응의 약화 몫을 키운다. 반응 계산이 유발자에게 이 값을 묻는다.</summary>
    internal sealed class ActivationAmplifierEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ActivationAmplifierEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => true;

        public override float ActivationDebuffMultiplier(Unit source)
            => source == Target ? _multiplier : 1f;
    }

    /// <summary>
    /// 복수심(1905) — 같은 진영의 유닛이 쓰러질 때마다 주는 피해가 늘고 중첩된다.
    /// </summary>
    public sealed class OdysseyVengeance : PassiveCode
    {
        private const float BonusPerLoss = 0.10f;
        private const string StatusKey = "odyssey_vengeance";

        private Action<Unit, Unit> _deathHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public OdysseyVengeance(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "복수심";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _deathHandler = OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Unit.AnyUnitDied += _deathHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            Unit.AnyUnitDied -= _deathHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
                Caster.RemoveStatusByKey(StatusKey);
            }
            _deathHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void OnAnyUnitDied(Unit dead, Unit killer)
        {
            if (Caster == null || !Caster.isActive || dead == null || dead == Caster) return;
            // 같은 진영이 쓰러졌을 때만. 적을 잡은 것으로는 오르지 않는다.
            if (dead.IsEnemy != Caster.IsEnemy) return;

            Caster.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.Vengeance, StatusKey, CodeName, Caster, Caster,
                new OutgoingDamageMultiplierEffect(1f + BonusPerLoss),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "같은 진영이 쓰러질 때마다 주는 피해 +10%. 중첩됩니다."));
        }
    }

    /// <summary>
    /// 끝없는 허기(1906) — 피 냄새(1831)의 상위. 빈사 대상에게 주는 피해가 크게 는다.
    /// </summary>
    public sealed class OdysseyEndlessHunger : PassiveCode
    {
        private const float HpThreshold = 0.5f;
        private const float Bonus = 0.30f;
        private const string StatusKey = "odyssey_endless_hunger";

        public OdysseyEndlessHunger(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "끝없는 허기";
            IgnoresActivationChance = true;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.EndlessHunger, StatusKey, CodeName, Caster, Caster,
                new WoundedTargetDamageEffect(HpThreshold, Bonus),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "체력이 50% 이하인 대상에게 주는 피해가 30% 증가합니다."));
        }

        public override void StopCode() => Caster?.RemoveStatusByKey(StatusKey);
    }

    /// <summary>대상의 남은 체력 비율이 기준 아래일 때만 붙는 피해 배율.</summary>
    internal sealed class WoundedTargetDamageEffect : BaseEffect
    {
        private readonly float _threshold;
        private readonly float _bonus;

        public WoundedTargetDamageEffect(float threshold, float bonus) : base(0, bonus)
        {
            _threshold = threshold;
            _bonus = bonus;
        }

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null || target.HpMax <= 0) return 1f;
            return target.HpCurr <= target.HpMax * _threshold ? 1f + _bonus : 1f;
        }
    }

    // ── 종별 고유 패시브 ────────────────────────────────────────────

    /// <summary>
    /// 노심융해(1910) — 공허의 정찰병. 쓰러지면 <b>자기 진영 전체</b>를 함께 태운다.
    /// 여럿을 한 번에 잡으면 연쇄로 터지므로, 한 마리씩 떼어 잡는 편이 안전하다.
    /// </summary>
    public sealed class VoidScoutMeltdown : PassiveCode
    {
        private const float ConCoefficient = 0.5f;

        private Action<EventContext> _deathHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;
        private bool _detonated;

        public VoidScoutMeltdown(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "노심융해";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _detonated = false;
            _deathHandler = _ => Detonate();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _deathHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _deathHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void Detonate()
        {
            if (Caster == null) return;

            // 한 번만 터진다. Unit.Die()는 OnDeath를 DeactivateUnit보다 먼저 쏘므로,
            // 폭발이 옆의 정찰병을 죽이면 그쪽 폭발이 아직 isActive인 이쪽을 다시 때려
            // Die()가 거듭 불린다 — 가드가 없으면 둘이 서로를 끝없이 되받는다.
            // 씨앗의 자폭(GenericSeedPassives)도 같은 이유로 같은 가드를 둔다.
            if (_detonated) return;
            _detonated = true;

            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.GetBaseCon() * ConCoefficient));
            // '모든 아군' — 정찰병 입장의 아군, 즉 같은 진영이다. 자기 자신은 이미 쓰러졌다.
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster)
                         .Where(unit => unit != null && unit != Caster && unit.isActive).ToList())
            {
                ally.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Passive,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.Special, DamageTag.NonContactAttack,
                    }));
            }
        }
    }

    /// <summary>
    /// 포식(1911) — 공허의 학살자. 적을 처치할 때마다 INT와 DEX가 함께 오른다. 중첩된다.
    /// </summary>
    public sealed class VoidSlaughtererPredation : PassiveCode
    {
        private const float GrowthPerKill = 0.10f;
        private const string StatusKey = "void_slaughterer_predation";

        private Action<EventContext> _killHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public VoidSlaughtererPredation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "포식";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _killHandler = _ => Feed();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnKill, _killHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnKill, _killHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
                Caster.RemoveStatusByKey(StatusKey);
            }
            _killHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void Feed()
        {
            if (Caster == null || !Caster.isActive) return;

            foreach (BaseEnums.PrimaryStat stat in new[] { BaseEnums.PrimaryStat.INT, BaseEnums.PrimaryStat.DEX })
            {
                Caster.AddStatus(BuffStatus.Create(
                    OdysseyStatusIds.Predation, $"{StatusKey}_{stat}", CodeName, Caster, Caster,
                    new PrimaryStatMultiplierEffect(1f + GrowthPerKill, stat),
                    stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                    isBeneficial: true,
                    description: "적을 처치할 때마다 INT와 DEX +10%. 중첩됩니다."));
            }
        }
    }

    /// <summary>
    /// 돌림노래(1912) — 세이렌. 필드의 아군이 돌림노래를 부를 때마다 중첩이 쌓이고,
    /// 행동 제어를 당하면 그 중첩이 통째로 날아간다. 궁극기의 위력이 곧 이 중첩이다.
    /// </summary>
    public sealed class SirenChorus : PassiveCode
    {
        private Action<EventContext> _controlHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public SirenChorus(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "돌림노래";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            Caster.SetCombatResourceMaximum(OdysseyCombat.ChorusResource, 99, true);
            // 행동 제어가 노래를 끊는다 — 중첩을 전부 잃는다.
            _controlHandler = _ => ResetStacks();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnControlStarts, _controlHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnControlStarts, _controlHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _controlHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void ResetStacks()
        {
            int held = Caster.GetCombatResource(OdysseyCombat.ChorusResource);
            if (held > 0) Caster.TryConsumeCombatResource(OdysseyCombat.ChorusResource, held);
        }

        /// <summary>같은 진영의 세이렌이 노래할 때마다 모두의 중첩이 오른다.</summary>
        public static void NotifyChorus(Unit singer)
        {
            if (singer == null) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(singer))
            {
                if (ally == null || !ally.isActive) continue;
                if (!ally.ActivePassiveCodes.Any(code => code is SirenChorus)) continue;
                ally.AddCombatResource(OdysseyCombat.ChorusResource, 1);
            }
        }
    }

    /// <summary>
    /// 나를 봐!(1913) — 히포캅투스. 전투가 시작되면 스스로 표적이 된다. 지속 제한이 없다.
    /// </summary>
    public sealed class HippocampusLookAtMe : PassiveCode
    {
        private const string StatusKey = "hippocampus_look_at_me";

        public HippocampusLookAtMe(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "나를 봐!";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.LookAtMe, StatusKey, CodeName, Caster, Caster,
                // 우선도만 올리는 효과는 이미 있다(NewItemPassives). 새로 만들지 않는다.
                new TargetPriorityEffect(OdysseyCombat.TauntPriority),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"전투 내내 대상 우선도 +{OdysseyCombat.TauntPriority}."));
        }

        public override void StopCode() => Caster?.RemoveStatusByKey(StatusKey);
    }

    /// <summary>
    /// 생명의 메아리(1914) — 케토스. 활성(촉진) 약화를 달고 있는 적을 더 세게 친다.
    /// </summary>
    public sealed class KetosLifeEcho : PassiveCode
    {
        private const float Multiplier = 1.2f;
        private const string StatusKey = "ketos_life_echo";

        public KetosLifeEcho(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "생명의 메아리";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.AddStatus(BuffStatus.Create(
                OdysseyStatusIds.LifeEcho, StatusKey, CodeName, Caster, Caster,
                new ActivationTargetDamageEffect(Multiplier),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "활성 반응 약화를 지닌 적에게 가하는 피해가 1.2배가 됩니다."));
        }

        public override void StopCode() => Caster?.RemoveStatusByKey(StatusKey);
    }

    /// <summary>활성 반응 약화를 달고 있는 대상에게만 붙는 피해 배율.</summary>
    internal sealed class ActivationTargetDamageEffect : BaseEffect
    {
        private readonly float _multiplier;

        public ActivationTargetDamageEffect(float multiplier) : base(0, multiplier) => _multiplier = multiplier;

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || target == null) return 1f;
            return target.HasStatusKey(ElementalReaction.ActivationStatusKey) ? _multiplier : 1f;
        }
    }
}
