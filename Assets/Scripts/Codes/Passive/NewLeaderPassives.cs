using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    // ── 히폴리테 ────────────────────────────────────────────────────

    /// <summary>
    /// 히폴리테 P(227) — 아마조네스.
    ///
    /// 표식 '사냥감'이 축이다. 필드에 사냥감이 하나도 없으면 <b>공세 명령</b>으로 새로 찍고,
    /// 사냥감이 붙은 적이 맞을 때마다 <b>공세 지원</b>으로 덧붙여 때린다.
    /// 두 행동 모두 추가행동이라 차례를 쓰지 않는다.
    /// </summary>
    public sealed class HippolyteAmazoness : UniquePassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _turnHandler;
        private Action<EventContext> _cleanupHandler;
        private int _lastSupportAction = int.MinValue;
        private bool _resolving;
        private bool _registered;

        public HippolyteAmazoness(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "아마조네스";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _damageHandler = OnAnyDamageDealt;
            _turnHandler = _ => TryCommand();
            _cleanupHandler = _ => StopCode();
            Unit.AnyDamageDealt += _damageHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;

            if (Caster.ActiveUltimateCode is Codes.Ultimate.HippolyteJungleBlessing blessing)
                blessing.StartPassive();
            TryCommand();
        }

        public override void StopCode()
        {
            if (!_registered) return;

            Unit.AnyDamageDealt -= _damageHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnTurnStart, _turnHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _damageHandler = null;
            _turnHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        /// <summary>필드에 사냥감이 없으면 새로 찍는다.</summary>
        private void TryCommand()
        {
            if (Caster == null || !Caster.isActive) return;
            if (HippolyteCombat.AnyPrey(Caster)) return;

            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "hippolyte_command", "공세 명령", ResolveCommand);
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || context?.Target == null) return;
            if (context.Attacker == null || context.Attacker.IsEnemy != Caster.IsEnemy) return;
            if (!context.Target.HasStatusKey(HippolyteCombat.PreyStatusKey)) return;
            // 공세 명령·지원이 낸 피해에 스스로 다시 반응하면 사냥감이 죽을 때까지 연쇄된다.
            // 추가행동마다 CurrentActionId가 새로 발급되어 중복 억제로는 막히지 않는다.
            if (_resolving) return;

            // 같은 행동에서 여러 번 적중해도 지원은 한 번만 붙는다.
            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastSupportAction == action) return;
            _lastSupportAction = action;

            Unit prey = context.Target;
            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "hippolyte_support", "공세 지원", () => ResolveSupport(prey));
        }

        /// <summary>공세 명령 — 최대 체력이 가장 낮은 적을 찍어 사냥감을 남긴다.</summary>
        private void ResolveCommand()
        {
            if (Caster == null || !Caster.isActive) return;

            Unit prey = CombatTargets.AliveEnemies(Caster)
                .Where(enemy => enemy != null && !enemy.IsUntargetable)
                .OrderBy(enemy => enemy.HpMax)
                .ThenBy(enemy => enemy.HpCurr)
                .FirstOrDefault();
            if (prey == null) return;

            Strike(prey, HippolyteCombat.CommandPower, HippolyteCombat.CommandStrCoefficient);
            if (!prey.isActive) return;

            prey.AddStatus(BuffStatus.Create(
                HippolyteCombat.PreyStatusId, HippolyteCombat.PreyStatusKey, "사냥감",
                Caster, prey, new HippolytePreyEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "방어력 -10%, 받는 치유량 -20%, 대상 우선도 +2."));
        }

        /// <summary>공세 지원 — 사냥감이 맞을 때마다 덧붙이는 짧은 일격.</summary>
        private void ResolveSupport(Unit prey)
        {
            if (Caster == null || !Caster.isActive) return;
            if (prey == null || !prey.isActive || prey.IsUntargetable) return;
            Strike(prey, 0, HippolyteCombat.SupportStrCoefficient);
        }

        private void Strike(Unit prey, int flatPower, float coefficient)
        {
            _resolving = true;
            try
            {
            int power = flatPower + Mathf.RoundToInt(Caster.GetBaseStr() * coefficient);
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));
            prey.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Passive,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Pierce,
                },
                isCrit));
            }
            finally { _resolving = false; }
        }
    }

    /// <summary>'사냥감' 표식의 실제 효과. 방어·치유·우선도를 한 덩어리로 들고 있다.</summary>
    internal sealed class HippolytePreyEffect : BaseEffect
    {
        public HippolytePreyEffect() : base(0)
        {
            Category = BaseEnums.EffectCategory.Negative;
        }

        public override float OwnedDefenseStatMultiplierModifier(Unit unit, DamageContext context)
            => unit == Target ? 1f - HippolyteCombat.PreyDefenseDown : 1f;

        public override float HealingReceivedMultiplierModifier(Unit unit)
            => unit == Target ? 1f - HippolyteCombat.PreyHealingDown : 1f;

        public override int TargetPriorityAdditiveModifier(Unit unit)
            => unit == Target ? HippolyteCombat.PreyPriorityBonus : 0;
    }

    // ── 히미코 ──────────────────────────────────────────────────────

    /// <summary>히미코 전투 상수.</summary>
    public static class HimikoCombat
    {
        /// <summary>아군마다 따로 쌓이는 중첩 자원.</summary>
        public const string OracleResource = "himiko_oracle";

        /// <summary>중첩 하나가 올려 주는 추가공격 피해.</summary>
        public const float DamagePerStack = 0.05f;

        /// <summary>일반행동이 중첩 하나를 INT 몇 배로 바꾸는가.</summary>
        public const float BurstIntCoefficient = 0.2f;

        public const int StackStatusId = 6471;
        public const string StackStatusKey = "himiko_oracle_stack";

        /// <summary>필드 위 아군이 들고 있는 중첩 총합.</summary>
        public static int TotalStacks(Unit caster)
            => caster == null ? 0 : CombatTargets.AliveAlliesIncludingSelf(caster)
                .Sum(ally => Mathf.Max(0, ally.GetCombatResource(OracleResource)));
    }

    /// <summary>
    /// 히미코 P(244) — 아군 전체가 저마다 신탁 중첩을 쌓는다.
    /// 아군이 추가행동을 할 때마다 그 아군의 중첩이 1 오르고, 중첩 하나당 <b>그 아군의</b>
    /// 추가공격 피해가 5% 커진다. 히미코의 일반행동이 이 중첩을 모아 한 번에 터뜨린다.
    /// </summary>
    public sealed class HimikoOracle : UniquePassiveCode
    {
        private readonly Dictionary<Unit, Action<EventContext>> _handlers = new();
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public HimikoOracle(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "신탁";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                Attach(ally);
            }
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            foreach (KeyValuePair<Unit, Action<EventContext>> pair in _handlers)
            {
                pair.Key?.RemoveListener(BaseEnums.UnitEventType.OnAdditionalActivates, pair.Value);
            }
            _handlers.Clear();
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _cleanupHandler = null;
            _registered = false;
        }

        private void Attach(Unit ally)
        {
            if (ally == null || _handlers.ContainsKey(ally)) return;

            ally.AddStatus(BuffStatus.Create(
                HimikoCombat.StackStatusId, HimikoCombat.StackStatusKey, CodeName,
                Caster, ally, new HimikoStackEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "추가행동을 할 때마다 신탁 중첩이 쌓이고, 중첩 하나마다 추가공격 피해가 5% 증가합니다."));

            Action<EventContext> handler = _ => ally.AddCombatResource(HimikoCombat.OracleResource, 1);
            ally.AddListener(BaseEnums.UnitEventType.OnAdditionalActivates, handler);
            _handlers[ally] = handler;
        }
    }

    /// <summary>신탁 중첩이 추가공격 피해에만 얹히도록 태그를 확인한다.</summary>
    internal sealed class HimikoStackEffect : BaseEffect
    {
        public HimikoStackEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
        {
            if (attacker != Target || context?.DamageTags == null) return 1f;
            if (!context.DamageTags.Contains(DamageTag.AdditionalAttack)) return 1f;

            int stacks = Mathf.Max(0, attacker.GetCombatResource(HimikoCombat.OracleResource));
            return 1f + stacks * HimikoCombat.DamagePerStack;
        }
    }

    // ── 엘리자베스 ──────────────────────────────────────────────────

    /// <summary>엘리자베스 전투 상수.</summary>
    public static class ElizabethCombat
    {
        public const int RoseStatusId = 6472;
        public const string RoseStatusKey = "elizabeth_rose_crest";

        /// <summary>필드 전체에 이만큼 쌓이면 선봉장이 나간다.</summary>
        public const int VanguardThreshold = 12;

        public const int VanguardPower = 80;
        public const float VanguardStrCoefficient = 0.8f;

        public const int ShieldFlat = 200;
        public const float ShieldStrCoefficient = 0.8f;

        /// <summary>필드 위 적들이 달고 있는 장미 문장 총합.</summary>
        public static int TotalCrests(Unit caster)
            => caster == null ? 0 : CombatTargets.AliveEnemies(caster)
                .Count(enemy => enemy != null && enemy.HasStatusKey(RoseStatusKey));
    }

    /// <summary>
    /// 엘리자베스 P(212) — 고결한 장미.
    ///
    /// 엘리자베스가 필드에 있는 동안, <b>그 외의 아군</b>이 공격을 끝낼 때마다 맞은 적 하나에
    /// 장미 문장을 남긴다. 한 행동에 여럿을 때려도 한 장만 붙는다 — 가장 튼튼한 대상에 남긴다.
    /// 필드의 문장이 12장에 닿으면 추가행동 '제국주의의 선봉장'이 나간다.
    /// </summary>
    public sealed class ElizabethNobleRose : UniquePassiveCode
    {
        private Action<DamageResolvedContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private int _lastCrestAction = int.MinValue;
        private bool _registered;

        public ElizabethNobleRose(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "고결한 장미";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _damageHandler = OnAnyDamageDealt;
            _cleanupHandler = _ => StopCode();
            Unit.AnyDamageDealt += _damageHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;

            Unit.AnyDamageDealt -= _damageHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _damageHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            if (context?.Attacker == null || context.Target == null) return;
            // 자기 공격으로는 문장이 붙지 않는다 — '해당 유닛 외 아군'이 조건이다.
            if (context.Attacker == Caster) return;
            if (context.Attacker.IsEnemy != Caster.IsEnemy) return;
            if (context.Target.IsEnemy == Caster.IsEnemy || !context.Target.isActive) return;

            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastCrestAction == action) return;
            _lastCrestAction = action;

            Unit mark = context.Target;
            if (mark.HasStatusKey(ElizabethCombat.RoseStatusKey))
            {
                // 이미 달고 있으면 아직 없는 적 중 가장 튼튼한 쪽으로 옮겨 찍는다.
                mark = CombatTargets.AliveEnemies(Caster)
                    .Where(enemy => enemy != null && !enemy.IsUntargetable &&
                                    !enemy.HasStatusKey(ElizabethCombat.RoseStatusKey))
                    .OrderByDescending(enemy => enemy.HpMax)
                    .FirstOrDefault();
                if (mark == null) return;
            }

            mark.AddStatus(BuffStatus.Create(
                ElizabethCombat.RoseStatusId, ElizabethCombat.RoseStatusKey, "장미 문장",
                Caster, mark, new MarkerBuffEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                category: BaseEnums.StatusCategory.Negative,
                isBeneficial: false,
                description: "엘리자베스가 남긴 문장. 필드에 12장이 모이면 선봉장이 나섭니다."));

            if (ElizabethCombat.TotalCrests(Caster) < ElizabethCombat.VanguardThreshold) return;

            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "elizabeth_vanguard", "제국주의의 선봉장", ResolveVanguard);
        }

        /// <summary>
        /// 제국주의의 선봉장 — 적 전체를 치고, 실제로 넣은 피해만큼 아군 전체에 방어막을 두른다.
        /// 문장은 이 행동으로 모두 걷힌다.
        /// </summary>
        private void ResolveVanguard()
        {
            if (Caster == null || !Caster.isActive) return;

            int power = ElizabethCombat.VanguardPower +
                Mathf.RoundToInt(Caster.GetBaseStr() * ElizabethCombat.VanguardStrCoefficient);
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(power, BaseEnums.PrimaryStat.STR) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));

            int dealt = 0;
            foreach (Unit enemy in CombatTargets.AliveEnemies(Caster)
                         .Where(unit => unit != null && !unit.IsUntargetable).ToList())
            {
                var context = new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Passive,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.AdditionalAttack,
                        DamageTag.Special, DamageTag.ContactAttack,
                    },
                    isCrit);
                enemy.TakeDamage(context);
                dealt += Mathf.Max(0, context.ResolvedDamage);
                enemy.RemoveStatusByKey(ElizabethCombat.RoseStatusKey);
            }

            if (dealt <= 0) return;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddShield(dealt, Caster);
            }
        }
    }
}
