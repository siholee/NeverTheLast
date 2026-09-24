using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 드레이크 U(11) — 럭키세븐.
    ///
    /// 마나가 아니라 <b>아군의 추가행동</b>이 자원이다. 누가 추가행동을 하든 1중첩이 쌓이고
    /// 7중첩에서 터진다. 중첩 수만큼 연속으로 때리되, 도중에 대상이 쓰러지면 다음 상대로 옮긴다.
    /// </summary>
    public sealed class DrakeLuckySeven : SimpleUltimate
    {
        private const int HitPower = 50;
        private const float HitDexCoefficient = 0.5f;

        private readonly Dictionary<Unit, Action<EventContext>> _handlers = new();
        private bool _registered;

        public DrakeLuckySeven(UltimateCodeContext context) : base(context, "럭키세븐", 0.4f) { }

        /// <summary>
        /// 마나가 아니라 아군의 추가행동이 자원이라, 궁극기 스스로 자원을 굴린다.
        /// 고유 패시브(사략면장)가 전투 시작에 한 번 불러 준다.
        /// </summary>
        public void StartPassive()
        {
            if (Caster == null || _registered) return;

            Caster.SetCombatResourceMaximum(
                DrakeCombat.LuckySevenResource, DrakeCombat.LuckySevenThreshold, true);
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                if (ally == null || _handlers.ContainsKey(ally)) continue;
                Action<EventContext> handler = _ => Charge();
                ally.AddListener(BaseEnums.UnitEventType.OnAdditionalActivates, handler);
                _handlers[ally] = handler;
            }
            _registered = true;
        }

        public void StopPassive()
        {
            foreach (KeyValuePair<Unit, Action<EventContext>> pair in _handlers)
            {
                pair.Key?.RemoveListener(BaseEnums.UnitEventType.OnAdditionalActivates, pair.Value);
            }
            _handlers.Clear();
            _registered = false;
        }

        private void Charge()
        {
            if (Caster == null || !Caster.isActive) return;
            int stacks = Caster.AddCombatResource(DrakeCombat.LuckySevenResource, 1);
            if (stacks < DrakeCombat.LuckySevenThreshold) return;
            Caster.CastUltimateCode();
        }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            int hits = Mathf.Max(1, Caster.GetCombatResource(DrakeCombat.LuckySevenResource));
            Caster.TryConsumeCombatResource(DrakeCombat.LuckySevenResource, hits);

            int power = HitPower + Mathf.RoundToInt(Caster.GetBaseDex() * HitDexCoefficient);
            Unit target = CombatTargets.PickByPriority(CombatTargets.AliveEnemies(Caster));

            for (int i = 0; i < hits; i++)
            {
                if (target == null || !target.isActive || target.IsUntargetable)
                {
                    target = CombatTargets.PickByPriority(CombatTargets.AliveEnemies(Caster));
                }
                if (target == null) return;

                bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(power, BaseEnums.PrimaryStat.DEX) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.SingleTarget, DamageTag.Physical, DamageTag.NonContactAttack,
                    },
                    isCrit));
            }
        }
    }


    /// <summary>
    /// 히폴리테 U(27) — 밀림의 가호. 패시브형 궁극기라 스스로 발동하지 않는다.
    /// '사냥감'이 붙은 적이 쓰러질 때마다 아군 전체의 DEX가 3%씩(아마조네스는 6%) 는다. 중첩된다.
    /// </summary>
    public sealed class HippolyteJungleBlessing : UltimateCode
    {
        private const int StatusId = 6473;

        private Action<Unit, Unit> _deathHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public override bool IsAutoCast => false;

        public HippolyteJungleBlessing(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "밀림의 가호";
            CastingDelay = 0f;
        }

        public void StartPassive()
        {
            if (Caster == null || _registered) return;

            _deathHandler = OnAnyUnitDied;
            _cleanupHandler = _ => StopCode();
            Unit.AnyUnitDied += _deathHandler;
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void CastCode() => StartPassive();

        public override void StopCode()
        {
            if (!_registered) return;

            Unit.AnyUnitDied -= _deathHandler;
            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _deathHandler = null;
            _cleanupHandler = null;
            _registered = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;

        private void OnAnyUnitDied(Unit dead, Unit killer)
        {
            if (Caster == null || !Caster.isActive || dead == null) return;
            if (dead.IsEnemy == Caster.IsEnemy) return;
            // 사냥감을 달고 죽은 적만 센다.
            if (!dead.HasStatusKey(HippolyteCombat.PreyStatusKey)) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                float ratio = ally.HasUnitTag(HippolyteCombat.AmazonessTag)
                    ? HippolyteCombat.BlessingDexRatio * 2f
                    : HippolyteCombat.BlessingDexRatio;
                ally.AddStatus(BuffStatus.Create(
                    StatusId, $"hippolyte_blessing_{ally.GetEntityId()}", CodeName,
                    Caster, ally, new PrimaryStatMultiplierEffect(1f + ratio, BaseEnums.PrimaryStat.DEX),
                    stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                    isBeneficial: true,
                    description: $"사냥감을 잡을 때마다 DEX +{Mathf.RoundToInt(ratio * 100)}%. 중첩됩니다."));
            }
        }
    }

    /// <summary>
    /// 히미코 U(44) — 아군 전체에게 3턴간 적 방어력 20% 무시를 두른다.
    /// </summary>
    public sealed class HimikoDivineDecree : SimpleUltimate
    {
        private const int StatusId = 6474;
        private const float DefenseIgnore = 0.20f;
        private const int Duration = 3;

        public HimikoDivineDecree(UltimateCodeContext context) : base(context, "신탁의 칙령", 0.4f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                ally.AddStatus(BuffStatus.Create(
                    StatusId, $"himiko_decree_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new DefenseIgnoreEffect(DefenseIgnore),
                    duration: Duration,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: $"{Duration}턴 동안 적 방어력을 {Mathf.RoundToInt(DefenseIgnore * 100)}% 무시합니다."));
            }
        }
    }

    /// <summary>공격할 때 상대 방어력을 정해진 비율만큼 깎아 계산한다.</summary>
    internal sealed class DefenseIgnoreEffect : BaseEffect
    {
        private readonly float _ignoreRatio;

        public DefenseIgnoreEffect(float ignoreRatio) : base(0, ignoreRatio)
            => _ignoreRatio = Mathf.Clamp01(ignoreRatio);

        public override bool IsBeneficial => true;

        public override float DefenseStatMultiplierModifier(Unit attacker, Unit target, DamageContext context)
            => attacker == Target ? 1f - _ignoreRatio : 1f;
    }

    /// <summary>
    /// 엘리자베스 U(12) — 처녀 여왕.
    /// 아군 전체가 지금 들고 있는 방어막 총량에 비례해 적 전체를 친다. 방어막은 소모하지 않는다.
    /// </summary>
    public sealed class ElizabethVirginQueen : SimpleUltimate
    {
        /// <summary>방어막 총량 1당 피해. 방어막이 커질수록 보상이 크다.</summary>
        private const float DamagePerShield = 0.6f;

        public ElizabethVirginQueen(UltimateCodeContext context) : base(context, "처녀 여왕", 0.5f) { }

        protected override void Resolve()
        {
            if (Caster == null || !Caster.isActive) return;

            int shieldTotal = CombatTargets.AliveAlliesIncludingSelf(Caster)
                .Sum(ally => Mathf.Max(0, ally.ShieldCurr));
            if (shieldTotal <= 0) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                shieldTotal * DamagePerShield * (isCrit ? Caster.CritMultiplierCurr : 1f)));

            foreach (Unit enemy in CombatTargets.AliveEnemies(Caster)
                         .Where(unit => unit != null && !unit.IsUntargetable).ToList())
            {
                enemy.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
            }
        }
    }
}
