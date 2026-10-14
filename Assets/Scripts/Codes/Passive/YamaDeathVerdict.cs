using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 야마 — 특성 「죽음의 선고」(Detail_08 §15.2). 야마가 살아서 전장에 있는 동안, 감전 상태의 적은 같은 진영 아군에게
    /// 받는 모든 피해가 +20%다. 감전의 출처 · 개수와 무관하게 선고는 하나이고, 감전이 없어지면 효력도 즉시 사라진다.
    ///
    /// 선고는 <b>감전에서 파생된 해로운 상태</b>다. 디버프로 세므로 사바흐의 빈틈 포착 · 해로운 상태 이벤트가 그대로 읽는다.
    /// 다만 원본 감전이 이미 효과 명중 · 상태 저항을 거쳤으므로 선고 자체는 어느 쪽도 다시 굴리지 않는다.
    /// 원본이 남아 있는 한 선고만 지워도 곧바로 다시 붙고, 마지막 감전이 사라질 때 함께 사라진다.
    ///
    /// 붙이고 떼는 시점은 상태가 붙거나 떨어지는 <see cref="Unit.AnyStatusChanged"/>뿐이다 — 프레임 폴링이 없다.
    /// 효력도 질의 시점에 감전 · 야마 생존을 다시 확인하므로 화면에 보이는 선고와 실제 피해가 항상 같다.
    /// 지속피해 즉시 발동은 이 특성이 아니라 야마의 행동이 한다.
    /// </summary>
    public sealed class YamaDeathVerdict : UniquePassiveCode
    {
        public const int MarkStatusId = 70610;
        public const string MarkKey = "yama_death_verdict";
        public const float DamageMultiplier = 1.20f;

        private bool _subscribed;
        private Action<EventContext> _cleanupHandler;

        public YamaDeathVerdict(PassiveCodeContext context) : base(context) => CodeName = "죽음의 선고";

        // ── 즉시 발동 비율 (Detail_08 §15.3) ──

        /// <summary>q = clamp01(현재 특수공격력 / (현재 특수공격력 + K) × 현재 속도 / 100), K = 100 + 6 × (레벨 − 1).</summary>
        public static float DetonationRatio(Unit yama)
        {
            if (yama == null) return 0f;
            float specialAttack = Mathf.Max(0f, yama.StatValue(BaseEnums.StatType.SpecialAttack));
            float speed = Mathf.Max(0f, yama.StatValue(BaseEnums.StatType.Speed));
            float constant = UnitStats.ArmorConstant(yama.Level);
            float denominator = specialAttack + constant;
            if (denominator <= 0f) return 0f;
            return Mathf.Clamp01(specialAttack / denominator * speed / 100f);
        }

        /// <summary>일반행동 발동 비율 q.</summary>
        public static float NormalRatio(Unit yama) => DetonationRatio(yama);

        /// <summary>초월기 발동 비율 min(1, 1.5q).</summary>
        public static float UltimateRatio(Unit yama) => Mathf.Min(1f, 1.5f * DetonationRatio(yama));

        // ── 감전 판정 ──

        /// <summary>감전(물 + 번개 반응이 남긴 지속피해)이 하나라도 붙어 있는가. 출처 · 개수는 묻지 않는다.</summary>
        public static bool IsShocked(Unit unit)
            => unit != null && unit.GetStatuses().Any(IsShockStatus);

        /// <summary>선고가 붙어 있는가.</summary>
        public static bool HasVerdict(Unit unit) => unit != null && unit.HasStatusKey(MarkKey);

        private static bool IsShockStatus(UnitStatus status)
            => status != null && status.StatusId == ElementalReaction.ShockStatusId;

        /// <summary>선고가 지금 효력을 낼 수 있는가 — 야마가 살아서 전장에 서 있다.</summary>
        private static bool Working(Unit yama) => yama != null && yama.isActive && yama.IsOnField;

        // ── 수명 ──

        public override void CastCode()
        {
            if (Caster == null) return;
            if (!_subscribed)
            {
                Unit.AnyStatusChanged += OnStatusChanged;
                _cleanupHandler = _ => StopCode();
                Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                _subscribed = true;
            }

            // 이미 감전된 적도 포함한다 — 구독 전에 붙은 감전을 한 번 훑는다.
            foreach (Unit enemy in Opponents()) Sync(enemy);
        }

        public override void StopCode()
        {
            if (_subscribed)
            {
                Unit.AnyStatusChanged -= OnStatusChanged;
                Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                _subscribed = false;
            }
            // 구독을 먼저 끊어야 떼어 낸 선고가 남은 감전 때문에 다시 붙지 않는다.
            RemoveAllMarks();
        }

        private void OnStatusChanged(Unit owner, UnitStatus status, bool added)
        {
            if (owner == null || Caster == null || owner.IsEnemy == Caster.IsEnemy) return;
            // 감전의 증감이나, 정화 등으로 선고만 떨어진 경우에만 다시 맞춘다.
            if (!IsShockStatus(status) && status?.Key != MarkKey) return;
            Sync(owner);
        }

        /// <summary>감전이 있고 야마가 일하고 있으면 선고를 두고, 아니면 걷는다.</summary>
        private void Sync(Unit enemy)
        {
            if (enemy == null || Caster == null) return;
            bool wanted = Working(Caster) && enemy.isActive && IsShocked(enemy);
            bool present = HasVerdict(enemy);
            if (wanted && !present)
            {
                UnitStatus verdict = BuffStatus.Create(MarkStatusId, MarkKey, CodeName, Caster, enemy,
                    new DeathVerdictEffect(Caster), duration: 0,
                    stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                    category: BaseEnums.StatusCategory.Negative,
                    description: "감전에서 비롯된 해로운 상태: 야마의 아군에게 받는 모든 피해 +20%. " +
                                 "감전이 모두 사라지면 함께 사라지고, 감전이 남아 있으면 선고만 지워도 다시 붙습니다.");
                // 원본 감전이 이미 명중 · 저항을 거쳤다. 파생 오라가 두 번째 관문이 되지 않게 둘 다 건너뛴다.
                verdict.SkipEffectHitRoll = true;
                verdict.SkipStatusResistanceRoll = true;
                enemy.AddStatus(verdict);
            }
            else if (!wanted && present)
            {
                enemy.RemoveStatusByKey(MarkKey);
            }
        }

        private void RemoveAllMarks()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return;
            foreach (Unit enemy in (Caster.IsEnemy ? grid.heroList : grid.enemyList).ToList())
            {
                if (HasVerdict(enemy)) enemy.RemoveStatusByKey(MarkKey);
            }
        }

        private IEnumerable<Unit> Opponents()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return Array.Empty<Unit>();
            return (Caster.IsEnemy ? grid.heroList : grid.enemyList).Where(unit => unit != null && unit.isActive).ToList();
        }

        /// <summary>선고의 효과. 같은 진영 아군의 피해에만 ×1.2이고, 감전 · 야마 생존을 질의 시점에 다시 본다.</summary>
        private sealed class DeathVerdictEffect : BaseEffect
        {
            private readonly Unit _yama;

            public DeathVerdictEffect(Unit yama) : base(0)
            {
                _yama = yama;
                Category = BaseEnums.EffectCategory.Negative;
            }

            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            {
                if (unit != Target || context?.Attacker == null || !Working(_yama)) return 1f;
                if (context.Attacker.IsEnemy != _yama.IsEnemy || unit.IsEnemy == _yama.IsEnemy) return 1f;
                return IsShocked(unit) ? DamageMultiplier : 1f;
            }
        }
    }
}
