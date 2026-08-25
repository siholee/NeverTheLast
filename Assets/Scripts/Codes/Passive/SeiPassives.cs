using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Base;
using Effects.Buffs;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>
    /// 세이 고유 패시브 — 빅뱅.
    ///
    /// 6초마다 무작위 적에게 투사체 6발을 각각 따로 조준해 발사한다. 각 타수는 INT 위력 40.
    ///
    /// 원안은 각 타수 위력 80(6초당 480)이었다. 세이의 일반공격이 위력 50이고 AV 스케줄러상
    /// 한 유닛은 실전에서 수 초에 한 번만 행동하므로, 원안대로면 패시브 하나가 일반공격의
    /// 10배 가까운 피해를 낸다. 40으로 낮춰 '공격형 서포터가 딜러급으로 자란다'는 컨셉은
    /// 유지하되 전용 딜러를 압도하지 않게 맞췄다.
    /// </summary>
    public sealed class SeiBigBang : UniquePassiveCode
    {
        private const int IntervalTurns = 3;   // 6초 → 3턴
        private const int MissileCount = 6;
        private const int MissilePower = 40;
        private const float MissileLaunchInterval = 0.06f;
        private const float MissileFlightDuration = 0.42f;

        public SeiBigBang(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "빅뱅";
            IgnoresActivationChance = true;
            Transferable = false;
        }

        public override void CastCode() => Caster?.AddStatus(BuffStatus.Create(
            SeiStatusIds.BigBang, "sei_big_bang", CodeName,
            Caster, Caster, new BigBangEffect(IntervalTurns, MissileCount, MissilePower,
                MissileLaunchInterval, MissileFlightDuration),
            stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
            isBeneficial: true,
            description: $"{IntervalTurns}턴마다 무작위 적에게 INT 위력 {MissilePower}의 투사체 {MissileCount}발을 발사합니다."));
    }

    /// <summary>세이가 쓰는 상태 ID 대역.</summary>
    public static class SeiStatusIds
    {
        public const int BigBang = 5111;
    }

    /// <summary>
    /// 빅뱅 본체. 유닛의 행동(AV)과 무관하게 자체 타이머로 굴러간다.
    /// 발사와 명중을 분리해 투사체가 날아가는 동안 대상이 죽어도 다른 적으로 넘어가게 했다.
    /// </summary>
    internal sealed class BigBangEffect : BaseEffect
    {
        private readonly int _intervalTurns;
        private readonly int _missileCount;
        private readonly int _missilePower;
        private readonly float _launchInterval;
        private readonly float _flightDuration;

        private int _turnsSinceFire;

        public BigBangEffect(int intervalTurns, int missileCount, int missilePower,
            float launchInterval, float flightDuration) : base(0, missilePower)
        {
            _intervalTurns = Mathf.Max(1, intervalTurns);
            _missileCount = missileCount;
            _missilePower = missilePower;
            _launchInterval = launchInterval;
            _flightDuration = flightDuration;
        }

        /// <summary>
        /// 세이의 턴마다 세고, 주기가 차면 <b>스케줄러에 패시브로 예약</b>한다.
        /// 그 자리에서 터뜨리지 않는 이유는 한 번에 하나만 행동해야 하기 때문이다.
        /// 예약된 패시브는 추가공격·본 행동보다 먼저 나간다.
        /// </summary>
        public override void OnOwnerTurn()
        {
            if (Target == null || !Target.isActive) return;

            _turnsSinceFire++;
            if (_turnsSinceFire < _intervalTurns) return;
            if (RandomEnemy() == null) return;

            _turnsSinceFire = 0;
            Managers.GameManager.Instance?.ActionScheduler.EnqueuePassive(
                Target, "sei_big_bang", "빅뱅",
                () => Target.StartCoroutine(FireVolley()));
        }

        private IEnumerator FireVolley()
        {
            Unit caster = Target;
            for (int index = 0; index < _missileCount; index++)
            {
                if (caster == null || !caster.isActive) yield break;

                Unit target = RandomEnemy();
                if (target == null) yield break;

                float angle = index * Mathf.PI * 2f / _missileCount;
                Vector3 midpoint = (caster.transform.position + target.transform.position) * 0.5f;
                Vector3 fanOffset = new(Mathf.Cos(angle) * 3.2f, Mathf.Sin(angle) * 2.4f, 0f);
                ProjectilePathData path = ProjectilePathData.CreateBezier(0f);
                path.bezierControlPoint = midpoint + fanOffset;

                var missilePrefab = ElementalProjectiles.PrefabFor(BaseEnums.UnitElement.Anemo)
                    ?? ElementalProjectiles.PrefabFor(BaseEnums.UnitElement.Geo);
                GameManager.Instance?.sfxManager?.FireTintedProjectile(
                    missilePrefab,
                    BaseEnums.UnitElement.Geo,
                    caster,
                    target,
                    _flightDuration,
                    ProjectilePathType.BezierCurve,
                    path);

                caster.StartCoroutine(ResolveMissile(caster, target));
                yield return new WaitForSeconds(_launchInterval);
            }
        }

        private IEnumerator ResolveMissile(Unit caster, Unit plannedTarget)
        {
            yield return new WaitForSeconds(_flightDuration);
            if (caster == null || !caster.isActive) yield break;

            Unit target = plannedTarget != null && plannedTarget.isActive && !plannedTarget.IsUntargetable
                ? plannedTarget
                : RandomEnemy();
            if (target == null) yield break;

            bool isCrit = UnityEngine.Random.value <= caster.CritChanceCurr;
            float critMultiplier = isCrit ? caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                caster.SkillDamage(_missilePower, BaseEnums.PrimaryStat.INT) * critMultiplier));

            var tags = new List<int>
            {
                DamageTag.SingleTarget,
                DamageTag.AdditionalAttack,
                DamageTag.Special,
                DamageTag.NonContactAttack,
            };
            target.TakeDamage(new DamageContext(caster, damage, BaseEnums.CodeType.Passive, tags, isCrit));
        }

        private Unit RandomEnemy()
        {
            if (Target == null) return null;
            List<Unit> enemies = global::Target.GetAllEnemies(Target)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .ToList();
            return enemies.Count > 0 ? enemies[UnityEngine.Random.Range(0, enemies.Count)] : null;
        }
    }

    /// <summary>
    /// 사전준비 (Lv.50) — 전투 시작 시 자신의 궁극기 충전량이 100%가 된다.
    /// 마나를 쓰지 않는 특수 궁극기 보유자에게는 효과가 없다.
    /// </summary>
    public sealed class SeiPreparation : PassiveCode
    {
        public SeiPreparation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사전준비";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            Caster?.FillUltimateResource(manaOnly: true);
        }
    }

    /// <summary>
    /// 창세의 노래 (Lv.92) — 전투 중 <b>최초로</b> 궁극기를 발동하면 아군 전체의 궁극기 충전량이 100%가 된다.
    /// 마나를 쓰지 않는 특수 궁극기 보유자에게도 적용된다.
    /// </summary>
    public sealed class SeiFirstSong : PassiveCode
    {
        private bool _registered;
        private bool _consumed;
        private Action<EventContext> _ultimateHandler;
        private Action<EventContext> _cleanupHandler;

        public SeiFirstSong(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "창세의 노래";
            IgnoresActivationChance = true;
            Transferable = false;   // 전수 불가능
        }

        public override void CastCode()
        {
            _consumed = false;
            if (Caster == null || _registered) return;

            _ultimateHandler = OnUltimateActivated;
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _ultimateHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = false;
        }

        private void OnUltimateActivated(EventContext context)
        {
            if (_consumed || Caster == null || !Caster.isActive) return;
            _consumed = true;

            foreach (Unit ally in Target.GetAllAllies(Caster).Where(unit => unit != null && unit.isActive))
            {
                // manaOnly: false — 스택형 자원을 쓰는 아군도 가득 채운다.
                ally.FillUltimateResource(manaOnly: false);
            }
            Debug.Log($"[창세의 노래] {Caster.UnitName}: 아군 전체 궁극기 충전 100%");
        }
    }
}
