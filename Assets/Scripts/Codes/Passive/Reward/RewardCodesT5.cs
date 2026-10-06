using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// T5 보상 코드 중 데이터만으로 되지 않는 것(Detail_17 §4.5) — 901 · 902 · 903 · 904 · 905 · 907. (906은 데이터 구동)
    /// </summary>
    public static partial class RewardCodes
    {
        static partial void CreateT5(int codeId, PassiveCodeContext context, ref PassiveCode code)
        {
            code = codeId switch
            {
                901 => new PerfectRecoveryCode(context),
                902 => new NaturalFateCode(context),
                903 => new OceanVerdictCode(context),
                904 => new ReviveTheDeadCode(context),
                905 => new TriumphalArchCode(context),
                907 => new InfiniteTowerCode(context),
                _ => code,
            };
        }
    }

    /// <summary>901 완벽한 재기 — 체력이 30% 미만이 되면 3턴에 걸쳐 최대 체력 100% 회복. 전투당 1회(기사회생의 상위).</summary>
    public sealed class PerfectRecoveryCode : RewardPassiveCode
    {
        private const float Threshold = 0.30f;
        private const int Turns = 3;
        private bool _used;

        public PerfectRecoveryCode(PassiveCodeContext context) : base(context, 901) { }

        protected override void OnCast()
        {
            _used = false;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamage);
        }

        protected override void OnStop()
        {
            Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnAfterDamage);
        }

        private void OnAfterDamage(EventContext context)
        {
            // 쓰러진 뒤에는 회복할 수 없다. 임계 아래로 떨어진 그 피해에서 한 번만 발동한다.
            if (_used || !HolderWorking || Caster.HpCurr <= 0) return;
            if (Caster.HpCurr >= Caster.HpMax * Threshold) return;

            _used = true;
            // 한 번에 채우지 않고 턴마다 나누어 준다. 총량이 최대 체력 100%가 되도록 턴당 비율은 100/3 %.
            AddStatus(Caster, new PercentHealOverTimeEffect(100f / Turns), "_regen", Turns);
        }
    }

    /// <summary>
    /// 902 당연한 운명 — 공격 후 최대 체력 12% 미만인 일반 적, 6% 미만인 정예 적을 즉시 처형(천살성의 상위).
    /// 지속피해 · 원소 반응이 아니라 보유자의 직접 공격 피해 뒤에 본다.
    /// </summary>
    public sealed class NaturalFateCode : RewardPassiveCode
    {
        private const float NormalThreshold = 0.12f;
        private const float EliteThreshold = 0.06f;
        private bool _executing;

        public NaturalFateCode(PassiveCodeContext context) : base(context, 902) { }

        protected override void OnCast()
        {
            _executing = false;
            Unit.AnyDamageDealt += OnAnyDamageDealt;
        }

        protected override void OnStop()
        {
            Unit.AnyDamageDealt -= OnAnyDamageDealt;
            _executing = false;
        }

        private void OnAnyDamageDealt(DamageResolvedContext resolved)
        {
            // 처형 자신의 피해가 다시 처형을 부르지 않게 막는다.
            if (_executing || resolved == null || !HolderWorking || resolved.Attacker != Caster) return;
            DamageContext damage = resolved.DamageContext;
            if (damage == null || damage.CodeType == BaseEnums.CodeType.Effect) return;

            Unit target = resolved.Target;
            if (target == null || target.IsEnemy == Caster.IsEnemy || !target.isActive || target.HpCurr <= 0) return;

            float threshold = target.UnitTier switch
            {
                "normal" => NormalThreshold,
                "elite" => EliteThreshold,
                _ => 0f,
            };
            if (threshold <= 0f || target.HpCurr >= target.HpMax * threshold) return;

            Execute(target);
        }

        private void Execute(Unit target)
        {
            _executing = true;
            try
            {
                // 받는 피해 경감 효과가 처형을 깎을 수 있어, 살아 있으면 남은 만큼 한두 번 더 확정 피해를 준다.
                for (int attempt = 0; attempt < 3 && target != null && target.isActive && target.HpCurr > 0; attempt++)
                {
                    target.TakeDamage(new DamageContext(Caster, target.HpCurr + target.ShieldCurr,
                        BaseEnums.CodeType.Passive, new List<int>
                        {
                            DamageTag.SingleTarget, DamageTag.PassiveAttack, DamageTag.TrueDamage, DamageTag.ShieldPenetration,
                        }));
                }
            }
            finally
            {
                _executing = false;
            }
        }
    }

    /// <summary>
    /// 903 대해의 판결 — 필드에 있으면 아군 전원의 원소 반응 피해 +45%, 반응을 일으킨 아군 마나 +5(원소술사의 상위).
    /// 피해 오라는 "reaction" 계열의 높은 하나만 센다. 마나는 보유자가 여럿이어도 중복 지급하지 않도록 필드 아군 중 첫 보유자만 준다.
    /// </summary>
    public sealed class OceanVerdictCode : RewardPassiveCode, IRewardAuraSource
    {
        public const string Family = "reaction";
        private const int ManaPerReaction = 5;

        public OceanVerdictCode(PassiveCodeContext context) : base(context, 903) { }

        public string AuraFamily => Family;
        public float AuraValue => 0.45f;

        protected override void OnCast()
        {
            foreach (Unit ally in FieldAllies())
                AddStatus(ally, new ReactionDamageAuraEffect(), key: RewardAura.KeyOf(Family));
            Unit.AnyElementalReaction += OnElementalReaction;
        }

        protected override void OnStop() => Unit.AnyElementalReaction -= OnElementalReaction;

        private void OnElementalReaction(Unit source, Unit target, string reactionName)
        {
            if (source == null || !HolderWorking || source.IsEnemy != Caster.IsEnemy || !source.isActive) return;
            // 903을 든 아군이 둘이어도 마나는 한 번만 — 필드에서 가장 앞선 보유자가 대표로 지급한다.
            Unit designated = FieldAllies().FirstOrDefault(ally =>
                ally.ActivePassiveCodesResolved().Any(passive => passive is OceanVerdictCode));
            if (designated != Caster) return;
            source.RecoverMana(ManaPerReaction);
        }

        private sealed class ReactionDamageAuraEffect : BaseEffect
        {
            public ReactionDamageAuraEffect() : base(0) { }

            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && context != null && context.HasTag(DamageTag.ElementalReaction)
                    ? 1f + RewardAura.Best(Target, Family)
                    : 1f;
        }
    }

    /// <summary>904 죽은 자의 소생 — 전투가 끝날 때 쓰러진 아군 하나를 최대 체력 50%로 부활. 보유자가 쓰러져 있으면 발동하지 않는다.</summary>
    public sealed class ReviveTheDeadCode : RewardPassiveCode
    {
        public ReviveTheDeadCode(PassiveCodeContext context) : base(context, 904) { }

        protected override void OnCast() { }

        public override void OnBattleSettled(bool victory, bool holderSurvived)
        {
            if (!holderSurvived || Caster == null) return;
            Managers.GridManager grid = Managers.GridManager.Instance;
            if (grid == null) return;

            // 대기석 유닛은 이 전투에 없었다. 쓰러졌다 복원된 칸 위의 아군만 후보다.
            List<Unit> fallen = grid.heroList
                .Where(unit => unit != null && !unit.IsEnemy && !unit.isActive && unit.currentCell != null &&
                               unit.LastActiveId > 0 && !unit.IsBench)
                .ToList();
            if (fallen.Count == 0) return;

            fallen[Random.Range(0, fallen.Count)].ReviveAfterBattle(0.5f);
        }
    }

    /// <summary>
    /// 905 개선문 — 필드 아군이 가진 보상 코드가 합쳐 15개 이상이면 전투 시작 후 4턴간 아군 전원 치명타 확률 +50%p.
    /// 여러 명이 들어도 같은 키라 한 번만 걸린다.
    /// </summary>
    public sealed class TriumphalArchCode : RewardPassiveCode
    {
        private const int RequiredCodes = 15;
        private const int Turns = 4;
        private const float CritRateBonus = 0.5f;

        public TriumphalArchCode(PassiveCodeContext context) : base(context, 905) { }

        protected override void OnCast()
        {
            List<Unit> allies = FieldAllies();
            if (!allies.Contains(Caster)) return;
            if (allies.Sum(ally => ally.RewardCodeIds.Count) < RequiredCodes) return;

            foreach (Unit ally in allies)
                AddStatus(ally, new StatModifierEffect(BaseEnums.StatType.CritRate, CritRateBonus), "_party", Turns,
                    key: "reward_905_party");
        }
    }

    /// <summary>907 무한의 탑 — 자신의 영구 중첩 50개마다 최대 체력 · 공격력 · 방어력 · 특수방어력 +2%(상한 없음).</summary>
    public sealed class InfiniteTowerCode : RewardPassiveCode
    {
        private const int StacksPerStep = 50;
        private const float PercentPerStep = 0.02f;

        public InfiniteTowerCode(PassiveCodeContext context) : base(context, 907) { }

        public override float PermanentStatPercentModifier(BaseEnums.StatType stat)
        {
            if (stat != BaseEnums.StatType.MaxHp && stat != BaseEnums.StatType.Attack &&
                stat != BaseEnums.StatType.Defense && stat != BaseEnums.StatType.SpecialDefense) return 0f;
            return TotalStacks() / StacksPerStep * PercentPerStep;
        }

        /// <summary>보유자의 성장 코드들이 쌓는 영구 중첩의 합. 같은 키는 한 번만 센다.</summary>
        private int TotalStacks()
        {
            if (Caster == null) return 0;
            return Caster.ActivePassiveCodesResolved()
                .Select(passive => passive.PermanentStackKey)
                .Where(key => !string.IsNullOrEmpty(key))
                .Distinct()
                .Sum(key => Caster.GetPermanentStacks(key));
        }

        // 스탯은 PermanentStatPercentModifier가 준다 — 라운드 안에서 따로 할 일이 없다.
        protected override void OnCast() { }
    }
}
