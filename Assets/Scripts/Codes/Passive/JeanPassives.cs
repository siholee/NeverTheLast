using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Neutral;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class JeanCodeIds
    {
        public const int Innate = 207;
        public const int MisfireGuard = 104;
        public const int PhysicalCoach = 105;
        public const int BulkUp = 106;
        public const int Standardbearer = 107;
    }

    internal static class JeanStatusIds
    {
        public const int MaidOfOrleans = 6530;
        public const int BulkUp = 6531;
        public const int Standardbearer = 6532;
    }

    /// <summary>
    /// 잔 고유 P — 최고의 방어.
    ///
    /// 전투 시작 시 3중첩으로 서고, <b>적의 행동 대상으로 지정되는 순간</b> 중첩을 한 개 태워
    /// 상대보다 먼저 <c>철권제재</c>를 꽂는다.
    ///
    /// 스카디의 반격이 <c>OnAfterDamageTaken</c>(맞은 뒤)을 듣는 것과 달리, 이쪽은
    /// <c>OnTargeted</c>(맞기 전)를 듣고 <b>그 자리에서 동기적으로</b> 해결한다.
    /// 큐에 추가행동을 예약하면 진행 중인 상대 행동이 끝난 뒤에 풀려 '먼저'가 되지 않기 때문이다.
    /// 되받아친 일격에 상대가 쓰러지면 상대의 행동은 통째로 접힌다
    /// (<see cref="Combat.Targeting.Announce"/>가 그 판정을 한다).
    /// </summary>
    public sealed class JeanBestDefense : UniquePassiveCode, ICounterAttackProvider
    {
        public const string ResourceId = "jean_best_defense";
        public const int MaxStacks = 3;

        /// <summary>철권제재 — STR×0.8.</summary>
        private const int RiposatePower = 80;

        private Action<EventContext> _targetedHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;
        private bool _resolving;

        public JeanBestDefense(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "최고의 방어";
            Power = RiposatePower;
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            // 전투 시작은 언제나 만충이다. 첫 합에서 곧바로 되받아칠 수 있어야 딜탱으로 선다.
            Caster.SetCombatResourceMaximum(ResourceId, MaxStacks, resetCurrent: true);
            Caster.AddCombatResource(ResourceId, MaxStacks);

            _targetedHandler = OnTargeted;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnTargeted, _targetedHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;

            Caster.RemoveListener(BaseEnums.UnitEventType.OnTargeted, _targetedHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _targetedHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        /// <summary>중첩을 최대치까지 되돌린다. 궁극기 <c>오를레앙의 성녀</c>가 부른다.</summary>
        public void RefillStacks()
        {
            if (Caster == null) return;
            Caster.AddCombatResource(ResourceId, MaxStacks);
        }

        private void OnTargeted(EventContext context)
        {
            Unit attacker = context?.Grantor;
            if (_resolving || Caster == null || !_registered || !Caster.isActive ||
                attacker == null || !attacker.isActive || attacker.IsEnemy == Caster.IsEnemy ||
                attacker.IsUntargetable) return;
            if (!Caster.TryConsumeCombatResource(ResourceId, 1)) return;

            _resolving = true;
            try
            {
                Riposte(attacker);
            }
            finally
            {
                _resolving = false;
            }
        }

        /// <summary>Sp 철권제재 — 지정한 상대에게 STR 기반 위력 80의 접촉 반격.</summary>
        private void Riposte(Unit attacker)
        {
            // 추가행동 축(바스테트 '야수의 시선')이 이 일격을 세도록 신호를 함께 낸다.
            Caster.Invoke(BaseEnums.UnitEventType.OnAdditionalActivates, new EventContext(Caster));

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.STR) * critMultiplier));

            attacker.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.CounterAttack,
                    DamageTag.ContactAttack, DamageTag.Physical,
                },
                isCrit));
        }
    }

    /// <summary>Lv.7 오사방지 — 아군이 아군에게 만드는 해로운 원소 반응을 막는다.</summary>
    public sealed class JeanMisfireGuard : PassiveCode
    {
        public JeanMisfireGuard(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "오사방지";
            IgnoresActivationChance = true;
        }

        public override void CastCode() => FriendlyReactionGuard.Apply(Caster);
    }

    /// <summary>Lv.11 피지컬 코치 — STR 집중 훈련에 배치되면 효율 +10%. TrainingManager가 읽는다.</summary>
    public sealed class JeanPhysicalCoach : PassiveCode
    {
        public const float TrainingBonus = 0.10f;

        public JeanPhysicalCoach(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "피지컬 코치";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>
    /// Lv.26 벌크업 — 전투 시작 후 <b>첫 일반행동</b>이 대체행동으로 바뀐다.
    ///
    /// 코드 자체는 표식만 세우고, 실제 행동 교체는 잔의 일반행동이 이 표식을 보고 처리한다.
    /// </summary>
    public sealed class JeanBulkUp : PassiveCode
    {
        public const string ResourceId = "jean_bulk_up";

        public JeanBulkUp(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "벌크업";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            // 1이면 아직 안 썼다는 뜻이다. 라운드마다 패시브가 다시 시전되며 되채워진다.
            Caster.SetCombatResourceMaximum(ResourceId, 1, resetCurrent: true);
            Caster.AddCombatResource(ResourceId, 1);
        }

        /// <summary>대체행동으로 바꿀 차례인가. 소비까지 함께 처리한다.</summary>
        public static bool TryConsume(Unit unit)
            => unit != null && unit.TryConsumeCombatResource(ResourceId, 1);

        /// <summary>대체행동 '벌크업' — 자신의 STR +10%. 중첩된다.</summary>
        public static void Apply(Unit unit)
        {
            if (unit == null) return;

            unit.AddStatus(BuffStatus.Create(
                JeanStatusIds.BulkUp, "jean_bulk_up_str", "벌크업",
                unit, unit, new PrimaryStatMultiplierEffect(1.10f, BaseEnums.PrimaryStat.STR),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "STR이 10% 증가합니다."));
        }
    }

    /// <summary>Lv.45 기수 — 우선도 +1을 상시로 유지한다. 사실상 꺼지지 않는 도발이다.</summary>
    public sealed class JeanStandardbearer : PassiveCode
    {
        public JeanStandardbearer(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "기수";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null) return;

            // 지속시간을 주지 않아(-1) 라운드 내내 유지된다. 공용 도발 상태를 그대로 쓰므로
            // Taunt.Has 판정과 수르트·스카디의 대체행동 조건에도 똑같이 걸린다.
            Caster.AddStatus(BuffStatus.Create(
                Taunt.StatusId, Taunt.StatusKey, CodeName, Caster, Caster,
                new TauntEffect(0, 1),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Neutral,
                isBeneficial: true,
                description: "우선도가 1 증가한 상태를 상시로 유지합니다."));
        }
    }
}
