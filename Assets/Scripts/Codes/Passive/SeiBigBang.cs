using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 세이 — 특성 「빅뱅」.
    ///
    /// 고유 중첩 '창세의 결정'. 필드 위에 <b>보호막을 두른 아군</b>이 적의 공격을 받을 때마다 1중첩을 얻는다
    /// (세이 자신도 아군이다). 7개가 모이면 무작위 적에게 방어력의 25%짜리 결정 파쇄를 7번 일으킨다.
    ///
    ///   · 보호막 여부는 <b>맞기 직전</b>에 본다 — 그 한 방에 보호막이 깨져도 중첩은 쌓인다.
    ///   · 지속피해 · 원소 반응은 '공격'이 아니라 세지 않는다.
    ///   · 조각은 조각마다 대상을 새로 뽑고, <b>대상 우선도(도발 · 전열)를 무시한다.</b>
    ///   · 파쇄는 추가행동이다. 결정이 찬 즉시 줄을 서고, 시전하는 순간 7개를 모두 태운다.
    ///     줄을 서 있는 동안에는 더 쌓이지 않는다(상한 7).
    ///   · 추가행동이 끝나면 아군 전체에게 <c>100 + 방어력의 5%</c> 보호막을 두른다.
    ///     보호막을 두른 아군이 맞아야 다시 쌓이므로, 한 번 터지면 다음 빅뱅의 재료가 스스로 깔린다.
    /// </summary>
    public sealed class SeiBigBang : UniquePassiveCode
    {
        public override int AttackForm => DamageTag.Spell;

        public const string ResourceId = "sei_genesis_crystal";
        public const int Threshold = 7;
        private const float ShardDefenseRatio = 0.25f;
        private const float ShieldFlat = 100f;
        private const float ShieldDefenseRatio = 0.05f;

        private bool _subscribed;

        public SeiBigBang(PassiveCodeContext context) : base(context)
        {
            CodeName = "빅뱅";
            Combat.CombatResourceLabels.Register(ResourceId, "창세의 결정");
        }

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.SetCombatResourceMaximum(ResourceId, Threshold, resetCurrent: true);
            if (_subscribed) return;
            Unit.AnyDamageIncoming += OnDamageIncoming;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (!_subscribed) return;
            Unit.AnyDamageIncoming -= OnDamageIncoming;
            _subscribed = false;
        }

        private void OnDamageIncoming(Unit target, DamageContext context)
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            if (target == null || target.IsEnemy != Caster.IsEnemy || !target.IsOnField) return;
            if (target.ShieldCurr <= 0) return;
            if (context == null || context.Attacker == null || context.Attacker.IsEnemy == Caster.IsEnemy) return;
            if (context.CodeType == BaseEnums.CodeType.Effect) return;
            if (Caster.GetCombatResource(ResourceId) >= Threshold) return;

            int now = Caster.AddCombatResource(ResourceId, 1);
            GeoFx.CrystalGained(Caster, target, Caster);
            if (now >= Threshold)
            {
                GameManager.Instance?.ActionScheduler?.EnqueuePriorityAdditional(Caster, "big_bang", CodeName, Release);
            }
        }

        /// <summary>추가행동 본체. 결정 7개를 태우고 뽑힌 대상 위치에서 파쇄를 일으킨다.</summary>
        private void Release()
        {
            if (Caster == null || !Caster.isActive) return;
            if (!Caster.TryConsumeCombatResource(ResourceId, Threshold)) return;
            if (AliveEnemies().Count == 0) return;

            ActionAnnouncer.AnnouncePassive(Caster, this);
            Caster.isCasting = true;
            Caster.StartCoroutine(Fire());
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>자동 검수에서 실제 추가행동 본체를 프리롤 뒤 정확히 시작한다.</summary>
        public bool DebugReleaseExecutable()
        {
            if (Caster == null || !Caster.isActive || AliveEnemies().Count == 0) return false;
            Caster.AddCombatResource(ResourceId, Threshold);
            Release();
            return Caster.isCasting;
        }
#endif

        private IEnumerator Fire()
        {
            // 카드가 떠오른 채 결정을 두르고, 일곱 파쇄를 마친 뒤 내려앉는다.
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Cast, CodeName);
            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.15f, 1.05f, 0.3f));
            yield return GeoFx.BigBang(Caster, Threshold, PickRandomEnemy, HitShard);
            yield return ShieldAllies();
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Resolve, CodeName);
            Caster.isCasting = false;
        }

        /// <summary>파쇄가 끝난 뒤 — 흩어진 결정광이 아군 둘레로 모여 방패가 된다.</summary>
        private IEnumerator ShieldAllies()
        {
            if (Caster == null || !Caster.isActive) yield break;
            int amount = Mathf.RoundToInt(StatDamage(ShieldFlat, BaseEnums.StatType.Defense, ShieldDefenseRatio));
            yield return new WaitForSeconds(0.12f);
            VfxComposer.PlayScreen("sei.bigbang", Caster, AliveAllies());

            foreach (Unit ally in AliveAllies())
            {
                ally.AddShield(amount, Caster);
                GeoFx.StoneShield(ally, 1f, "sei.bigbang");
                yield return new WaitForSeconds(0.05f);
            }
        }

        /// <summary>대상 우선도를 무시한다 — 살아 있는 적 가운데 균등하게 하나.</summary>
        private Unit PickRandomEnemy()
        {
            List<Unit> enemies = AliveEnemies();
            return enemies.Count == 0 ? null : enemies[Random.Range(0, enemies.Count)];
        }

        private void HitShard(Unit target)
        {
            if (target == null || !target.isActive || Caster == null) return;
            bool crit = RollCritical(target, out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(StatDamage(0f, BaseEnums.StatType.Defense, ShardDefenseRatio) * multiplier));
            target.TakeDamage(new DamageContext(Caster, damage, BaseEnums.CodeType.Passive, new List<int>
            {
                DamageTag.MultiTarget, DamageTag.PassiveAttack, DamageTag.NonContactAttack, DamageTag.Spell,
            }, crit));
        }

        private List<Unit> AliveAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit && unit.isActive && unit.IsOnField).ToList();
        }

        private List<Unit> AliveEnemies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit && unit.IsOnField && !unit.IsUntargetable).ToList();
        }
    }
}
