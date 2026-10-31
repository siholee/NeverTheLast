using BaseClasses;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>편성과 무관하게 적용하는 적 기본 전투 예산. 강제 종료·해제 가능한 강화가 아니다.</summary>
    public static class EnemyCombatBudget
    {
        public const int GrowthStartStage = 16;
        public const float NormalHpMultiplier = .55f;
        public const float NormalOpeningFactor = .35f;
        public const float NormalOpeningRounds = .65f;
        public const float NormalBaselineRound = 1.15f;
        public const float NormalFirstActionProtection = .20f;
        public const float NormalFirstActionDeadline = 1.35f;
        public const float NormalPressurePerRound = .35f;
        public const float NormalPressureCap = 2f;
        public const string StatusKey = "enemy_combat_budget";
        // 적 수는 전투 시작에 고정한다. 처치/증원 때 남은 적의 개별 화력이 갑자기 바뀌지 않는다.
        public static float NormalEncounterFactor(int initialEnemyCount) => Mathf.Sqrt(2f / Mathf.Max(2, initialEnemyCount));
        public static float DamageMultiplier(StageKind kind, int stage)
        {
            if (kind == StageKind.Event) return 1f;
            int depth = Mathf.Max(0, stage - GrowthStartStage);
            return kind == StageKind.Normal ? 2.2f + 2.6f * depth :
                kind is StageKind.Boss or StageKind.MidBoss ? 2.5f + .40f * depth : 1.6f + .12f * depth;
        }
        public static float HpMultiplier(StageKind kind, int stage, int enemyId = 0)
            => kind == StageKind.Normal ? NormalHpMultiplier : stage <= GrowthStartStage || enemyId is 3201 or 3202 or 1121 ? 1f :
                kind == StageKind.Elite ? 1.15f : kind is StageKind.Boss or StageKind.MidBoss ? 1.40f : 1f;
        /// <summary>연출 시간이 아닌 전투 누적 AV/100. 추가행동·초월기·일시정지는 자체로 압박을 키우지 않는다.</summary>
        public static float EffectiveDamageMultiplier(StageKind kind, int stage, float rounds)
        {
            float baseline = DamageMultiplier(kind, stage);
            if (kind == StageKind.Event) return baseline;
            if (kind != StageKind.Normal)
                return baseline * Mathf.Min(4f, 1f + .12f * Mathf.Max(0f, rounds - 2f) + .50f * Mathf.Max(0f, rounds - 10f));
            float pressure = rounds <= NormalBaselineRound
                ? Mathf.Lerp(NormalOpeningFactor, 1f, Mathf.Clamp01((rounds - NormalOpeningRounds) / (NormalBaselineRound - NormalOpeningRounds)))
                : Mathf.Min(NormalPressureCap, 1f + NormalPressurePerRound * (rounds - NormalBaselineRound));
            // 첫 두 범용 구간의 기존 피해 ×2.2는 낮추지 않는다.
            return Mathf.Max(2.2f, baseline * pressure);
        }
        public static float SpeedMultiplier(StageKind kind, int stage)
        {
            if (kind == StageKind.Event) return 1f;
            int depth = Mathf.Max(0, stage - GrowthStartStage);
            return kind == StageKind.Normal ? 1.10f + .01f * depth : 1f + .008f * depth;
        }
        public static void Apply(Unit enemy)
        {
            var game = GameManager.Instance;
            var round = game?.RoundManager;
            if (enemy == null || !enemy.IsOnField || !enemy.IsEnemy || round == null || game.IsEventBattleInProgress ||
                round.CurrentCombatKind == StageKind.Event || enemy.HasStatusKey(StatusKey)) return;
            var kind = round.CurrentCombatKind;
            float damage = DamageMultiplier(kind, round.Stage), hp = HpMultiplier(kind, round.Stage, enemy.ID), speed = SpeedMultiplier(kind, round.Stage);
            // 공통 상태로 표시·수명만 관리하며, 효과도 중립이라 디버프 수·강화 획득에 포함하지 않는다.
            enemy.AddStatus(BuffStatus.Create(71624, StatusKey, "기본 전투 예산", enemy, enemy,
                new BudgetEffect(kind, round.Stage, hp, speed), category: BaseEnums.StatusCategory.Neutral,
                description: kind == StageKind.Normal
                    ? $"개전에는 첫 일반행동 전의 아군에게 주는 피해가 완화됩니다. 이 완충은 시간이 지나면 끝나며 전투가 길어질수록 피해가 강해집니다. 무리 편성은 개체당 피해가 완화됩니다. 체력 ×{hp:0.##} · 속도 ×{speed:0.##}. 해제 불가."
                    : $"기본 피해 ×{damage:0.##}. 전투가 길어질수록 피해가 강해집니다. 체력 ×{hp:0.##} · 속도 ×{speed:0.##}. 해제 불가."));
        }
        private sealed class BudgetEffect : BaseEffect
        {
            private readonly float _hp, _speed;
            private readonly StageKind _kind;
            private readonly int _stage;
            public BudgetEffect(StageKind kind, int stage, float hp, float speed) : base(0)
            { _kind = kind; _stage = stage; _hp = hp; _speed = speed; Category = BaseEnums.EffectCategory.Neutral; }
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target) return 1f;
                var scheduler = GameManager.Instance?.ActionScheduler;
                float rounds = scheduler?.ReferenceCyclesElapsed ?? 0f;
                float result = EffectiveDamageMultiplier(_kind, _stage, rounds);
                // 적 전체의 저화력 구간을 늘리지 않고 느린 아군에게도 첫 일반행동의 기회를 남긴다.
                if (_kind == StageKind.Normal && target != null && !target.IsEnemy && !target.IsSummon &&
                    scheduler != null && rounds < NormalFirstActionDeadline && !scheduler.HasStartedNormalAction(target))
                    result = Mathf.Min(result, Mathf.Max(2.2f, DamageMultiplier(_kind, _stage) * NormalFirstActionProtection));
                if (_kind == StageKind.Normal)
                    result = Mathf.Max(2.2f, result * NormalEncounterFactor(scheduler?.InitialEnemyCount ?? 0));
                return result;
            }
            public override float StatPercentModifier(Unit unit, BaseEnums.StatType stat)
                => unit != Target ? 0f : stat == BaseEnums.StatType.MaxHp ? _hp - 1f :
                    stat == BaseEnums.StatType.Speed ? _speed - 1f : 0f;
        }
    }
}
