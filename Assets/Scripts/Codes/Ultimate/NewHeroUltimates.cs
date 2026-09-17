using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Ultimate
{
    /// <summary>
    /// 라이트 U — 하늘을 나는 꿈.
    /// 최대 마나가 가장 큰 아군에게 라이트 INT만큼 마나를 주고, 아군 전체의 부착 원소를
    /// 바람 하나로 교체한다. 위대한 비행 3스택을 소비했다면 부정적 상태도 모두 제거한다.
    ///
    /// 원소 교체는 <b>시전 시점 한 번</b>이다. 지속 효과가 아니므로 이후에 붙는 원소는 그대로 남는다.
    /// 걷어내는 것은 부착된 원소뿐이라 아군의 고유 원소(속성)는 건드리지 않는다 —
    /// 바람이 만료되면 부착이 없는 상태로 돌아갈 뿐이다.
    /// </summary>
    public sealed class LightFlyingDream : UltimateCode
    {
        public LightFlyingDream(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "하늘을 나는 꿈";
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            List<Unit> allies = CombatTargets.AliveAlliesIncludingSelf(Caster);

            if (allies.Count > 0)
            {
                int maximum = allies.Max(EffectiveMaximumMana);
                List<Unit> candidates = allies.Where(unit => EffectiveMaximumMana(unit) == maximum).ToList();
                Unit manaTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                if (manaTarget.UltimateResourceType == BaseEnums.UltimateResourceType.Mana)
                    manaTarget.AddUltimateResource(Mathf.Max(0, Caster.GetBaseInt()));
            }

            bool purify = Caster.ActivePassiveCodes
                .OfType<LightGreatFlight>()
                .FirstOrDefault()
                ?.TryConsumePurification() == true;

            foreach (Unit ally in allies)
            {
                foreach (BaseEnums.UnitElement element in AttachableElements)
                {
                    ally.RemoveCombatElement(element);
                }

                if (purify) ally.RemoveAllNegativeStatuses();
                ally.GrantCombatElement(
                    BaseEnums.UnitElement.Anemo,
                    Unit.CommonElementAuraDuration,
                    Caster);
            }

            StopCode();
        }

        /// <summary>부착 가능한 원소 목록. 시전마다 열거형을 다시 훑지 않도록 한 번만 만든다.</summary>
        private static readonly BaseEnums.UnitElement[] AttachableElements =
            Enum.GetValues(typeof(BaseEnums.UnitElement))
                .Cast<BaseEnums.UnitElement>()
                .Where(element => element != BaseEnums.UnitElement.None)
                .ToArray();

        private static int EffectiveMaximumMana(Unit unit)
            => unit != null && unit.UltimateResourceType == BaseEnums.UltimateResourceType.Mana
                ? unit.ManaMax
                : 0;

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>플라이어 U(500) — 적 전체에게 INT 위력 80. 소환수 공격이다.</summary>
    public sealed class FlyerSkyfall : SimpleUltimate
    {
        private const int SkyfallPower = 80;

        public FlyerSkyfall(UltimateCodeContext context) : base(context, "낙하", 0.4f)
        {
            Power = SkyfallPower;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        protected override void Resolve()
        {
            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT)
                * crit * Summons.DamageMultiplier(Caster.SummonOwner)));

            foreach (Unit target in Enemies())
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.SummonAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
            }
        }
    }

    /// <summary>가우디 U — 적 전체에게 INT×0.8 피해 후 풀 원소 부착.</summary>
    public sealed class GaudiImmortalLegacy : UltimateCode
    {
        public GaudiImmortalLegacy(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "불멸의 유산";
            CastingDelay = 0.5f;
            Power = 80;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (!cast)
            {
                StopCode();
                yield break;
            }

            List<Unit> targets = CombatTargets.AliveEnemies(Caster);
            if (targets.Count == 0)
            {
                StopCode();
                yield break;
            }

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float critMultiplier = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(CurrentPower, BaseEnums.PrimaryStat.INT) * critMultiplier));
            foreach (Unit target in targets)
            {
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int>
                    {
                        DamageTag.AllTarget, DamageTag.UltAttack,
                        DamageTag.Special, DamageTag.NonContactAttack,
                    },
                    isCrit));
                if (target.isActive)
                    target.GrantCombatElement(
                        BaseEnums.UnitElement.Dendro, Unit.CommonElementAuraDuration, Caster);
            }
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget()
            => Caster != null && Caster.isActive && CombatTargets.AliveEnemies(Caster).Count > 0;
    }

    public sealed class AsclepiusFlask : UltimateCode
    {
        public AsclepiusFlask(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "의신의 영약";
            CastingDelay = 0.5f;
            CodeTags = new List<int> { DamageTag.Special, DamageTag.NonContactAttack };
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            yield return new WaitForSeconds(CastingDelay);
            if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }

            Unit primary = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .OrderByDescending(unit => unit.Priority)
                .FirstOrDefault();
            if (primary == null) { StopCode(); yield break; }

            FireProjectile(primary, 0.3f);
            yield return new WaitForSeconds(0.3f);

            foreach (Unit ally in PiercedAllies(primary))
            {
                int shield = Mathf.Max(1, Caster.SkillDamage(120, BaseEnums.PrimaryStat.CON));
                ally.AddShield(shield, Caster);
            }

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            Deal(primary, 80, DamageTag.SingleTarget, isCrit, crit);

            foreach (Unit enemy in global::Target.GetAllEnemies(Caster)
                         .Where(unit => IsInBlast(primary, unit)).ToList())
            {
                Deal(enemy, 60, DamageTag.MultiTarget, isCrit, crit);
            }
            StopCode();
        }

        private IEnumerable<Unit> PiercedAllies(Unit target)
        {
            if (Caster.currentCell == null || target?.currentCell == null) return Enumerable.Empty<Unit>();
            int minX = Mathf.Min(Caster.currentCell.xPos, target.currentCell.xPos);
            int maxX = Mathf.Max(Caster.currentCell.xPos, target.currentCell.xPos);
            int row = Caster.currentCell.yPos;
            return global::Target.GetAllAllies(Caster).Where(ally =>
                ally != null && ally != Caster && ally.isActive && ally.currentCell != null &&
                ally.currentCell.yPos == row && ally.currentCell.xPos >= minX && ally.currentCell.xPos <= maxX);
        }

        private static bool IsInBlast(Unit center, Unit target)
        {
            if (center?.currentCell == null || target?.currentCell == null || !target.isActive) return false;
            return Mathf.Abs(center.currentCell.xPos - target.currentCell.xPos) +
                   Mathf.Abs(center.currentCell.yPos - target.currentCell.yPos) <= 1;
        }

        private void Deal(Unit target, int power, int targetTag, bool isCrit, float crit)
        {
            int damage = Mathf.Max(1, Mathf.RoundToInt(Caster.SkillDamage(power, BaseEnums.PrimaryStat.INT) * crit));
            target.TakeDamage(new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int> { targetTag, DamageTag.UltAttack, DamageTag.Special, DamageTag.NonContactAttack },
                isCrit));
        }

        /// <summary>의신의 영약은 특수 피해라 직선형이다.</summary>
        private void FireProjectile(Unit target, float delay)
        {
            if (GameManager.Instance?.sfxManager?.ProjectilePrefabs != null &&
                GameManager.Instance.sfxManager.ProjectilePrefabs.TryGetValue("FireBlast", out var prefab))
                GameManager.Instance.sfxManager.FireSingleProjectile(
                    prefab, Caster, target, delay,
                    ProjectilePathType.Linear, ProjectileFlight.DataFor(ProjectilePathType.Linear),
                    null, true);
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }

    public sealed class AmaterasuPortalBarrage : UltimateCode
    {
        private const int Duration = 3;

        /// <summary>한 번의 일제사격에 열리는 차원문 수.</summary>
        private const int PortalCount = 4;

        /// <summary>차원문 하나가 뱉는 화살 수. 같은 차원문의 화살은 같은 대상을 노린다.</summary>
        private const int ArrowsPerPortal = 2;

        /// <summary>화살 한 발의 피해는 방아쇠가 된 일반행동 <b>최종 피해</b>의 0.1배다.</summary>
        private const float ArrowDamageRatio = 0.1f;

        /// <summary>화살이 허공에서 대상까지 날아가는 시간.</summary>
        private const float ArrowFlightTime = 0.25f;

        /// <summary>같은 차원문에서 나온 화살이 겹쳐 보이지 않게 벌리는 거리(월드 단위).</summary>
        private const float ArrowSpread = 1.6f;

        /// <summary>
        /// 화살 몇 발이 적중할 때마다 불을 한 번 붙이는가. 한 번의 일제사격(8발)에 한 번꼴이다.
        ///
        /// 화살마다 붙이면 불+불 화상이 매 사격에 연달아 터진다. 셈은 시전 동안 사격을 넘어 이어진다.
        /// </summary>
        private const int ArrowsPerPyroAttach = 8;

        private int _arrowHitCount;

        private Action<EventContext> _normalHitHandler;
        private Action<EventContext> _cleanupHandler;

        public AmaterasuPortalBarrage(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "태양문 개방";
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            yield return new WaitForSeconds(CastingDelay);
            if (Caster == null || !Caster.isActive || Caster.isControlled) { StopCode(); yield break; }
            ClearPortalHook();
            _arrowHitCount = 0;
            // 상태를 먼저 건다. 재시전으로 이전 상태가 교체되며 부르는 정리가
            // 방금 단 새 훅까지 떼어 내지 않도록, 훅은 교체가 끝난 뒤에 단다.
            Caster.AddStatus(BuffStatus.Create(
                5750, "amaterasu_portals", CodeName,
                Caster, Caster, new LifetimeMarkerEffect(ClearPortalHook),
                duration: Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"일반행동을 맞힐 때마다 아군 진영 허공에 차원문 {PortalCount}개가 열려 " +
                             $"화살 {PortalCount * ArrowsPerPortal}발을 한 번에 쏜다. 차원문마다 대상을 무작위로 " +
                             "정하고, 한 차원문의 화살은 같은 대상을 노린다. 화살 한 발은 그 일반행동 " +
                             $"최종 피해의 {ArrowDamageRatio:0.#}배를 추가행동으로 준다."));
            _normalHitHandler = OnNormalHit;
            _cleanupHandler = _ => ClearPortalHook();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalHitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            StopCode();
        }

        /// <summary>
        /// 일반행동이 적중하면 차원문 일제사격이 뒤따른다.
        ///
        /// 화살 피해는 그 일반행동이 실제로 뽑아낸 최종 피해에서 갈라 나온다.
        /// 치명타로 크게 터진 평타는 뒤따르는 화살도 그만큼 굵어진다.
        /// </summary>
        private void OnNormalHit(EventContext context)
        {
            if (Caster == null || !Caster.isActive) return;

            int normalDamage = context?.DmgCtx?.Damage ?? 0;
            if (normalDamage <= 0) return;

            int arrowDamage = Mathf.Max(1, Mathf.RoundToInt(normalDamage * ArrowDamageRatio));
            Caster.StartCoroutine(FirePortalVolley(arrowDamage, context.DmgCtx.IsCrit));
        }

        /// <summary>차원문 전부가 동시에 화살을 뱉고, 도착하는 순간 한꺼번에 판정한다.</summary>
        private IEnumerator FirePortalVolley(int arrowDamage, bool isCrit)
        {
            List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable).ToList();
            if (enemies.Count == 0) yield break;

            // 화살이 전부 같은 순간 같은 시간을 날아가므로 표식 하나를 함께 쓴다.
            var token = new ProjectileImpactToken();
            var targets = new List<Unit>(PortalCount * ArrowsPerPortal);
            for (int portal = 0; portal < PortalCount; portal++)
            {
                Unit target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
                Vector3 origin = RandomAllyAirPoint();

                for (int arrow = 0; arrow < ArrowsPerPortal; arrow++)
                {
                    // 같은 차원문에서 나온 화살이 한 점에 겹치지 않도록 조금씩 어긋나게 놓는다.
                    float offset = (arrow - (ArrowsPerPortal - 1) * 0.5f) * ArrowSpread;
                    FireArrow(origin + new Vector3(0f, offset, 0f), target, token, arrowDamage, isCrit);
                    targets.Add(target);
                }
            }

            yield return ProjectileFlight.WaitForImpact(token, ArrowFlightTime);
            foreach (Unit target in targets) ResolveArrow(target, arrowDamage, isCrit);
        }

        /// <summary>
        /// 화살 한 발의 판정.
        ///
        /// 궁극기가 열어 준 <b>추가행동</b>이다. 일반행동으로는 세지 않으므로
        /// 평타에 얹히는 효과(태양의 박자 등)를 다시 굴리지 않는다.
        /// </summary>
        private void ResolveArrow(Unit target, int arrowDamage, bool isCrit)
        {
            if (Caster == null || !Caster.isActive) return;
            if (target == null || !target.isActive || target.IsUntargetable)
                target = global::Target.GetAllEnemies(Caster)
                    .FirstOrDefault(unit => unit != null && unit.isActive && !unit.IsUntargetable);
            if (target == null) return;

            target.TakeDamage(new DamageContext(
                Caster, arrowDamage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
                },
                isCrit));

            if (++_arrowHitCount % ArrowsPerPyroAttach == 0 && target.isActive)
                target.GrantCombatElement(BaseEnums.UnitElement.Pyro, Unit.CommonElementAuraDuration, Caster);
        }

        /// <summary>
        /// 차원문이 열릴 아군 진영 안의 한 점.
        ///
        /// 차원문 자체는 그리지 않는다. 화살이 허공에서 튀어나오는 것으로만 보인다.
        /// </summary>
        private Vector3 RandomAllyAirPoint()
        {
            int side = Caster?.currentCell != null && Caster.currentCell.xPos < 0 ? -1 : 1;
            if (GridManager.Instance != null && GridManager.Instance.TryGetSideBounds(side, out Bounds area))
                return new Vector3(
                    UnityEngine.Random.Range(area.min.x, area.max.x),
                    UnityEngine.Random.Range(area.min.y, area.max.y),
                    0f);

            return Caster != null ? Caster.transform.position : Vector3.zero;
        }

        /// <summary>차원문에서 나온 화살은 허공의 한 점에서 대상까지 직선으로 날아간다.</summary>
        private void FireArrow(Vector3 origin, Unit target, ProjectileImpactToken token,
            int arrowDamage, bool isCrit)
        {
            var visualContext = new DamageContext(
                Caster, arrowDamage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.AdditionalAttack,
                    DamageTag.Physical, DamageTag.NonContactAttack, DamageTag.Arrow,
                },
                isCrit);
            GameManager.Instance?.sfxManager?.FireProjectileFromPoint(
                origin, Caster, target, ArrowFlightTime,
                ProjectilePathType.Linear, ProjectileFlight.DataFor(ProjectilePathType.Linear),
                token.MarkImpact, visualContext);
        }

        private void ClearPortalHook()
        {
            if (Caster != null && _normalHitHandler != null)
                Caster.RemoveListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalHitHandler);
            if (Caster != null && _cleanupHandler != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            }
            _normalHitHandler = null;
            _cleanupHandler = null;
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }
}
