using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;
using Effects.Projectiles;

namespace Codes.Ultimate
{
    public sealed class AsclepiusFlask : UltimateCode
    {
        public AsclepiusFlask(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "의신의 영약";
            Cooldown = 4;
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
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }

    public sealed class AmaterasuPortalBarrage : UltimateCode
    {
        private const int Duration = 3;   // 6초 → 3턴

        /// <summary>한 번의 일제사격에 열리는 차원문 수.</summary>
        private const int PortalCount = 4;

        /// <summary>차원문 하나가 뱉는 화살 수. 같은 차원문의 화살은 같은 대상을 노린다.</summary>
        private const int ArrowsPerPortal = 2;

        /// <summary>화살 한 발의 피해는 방아쇠가 된 일반공격 <b>최종 피해</b>의 0.1배다.</summary>
        private const float ArrowDamageRatio = 0.1f;

        /// <summary>화살이 허공에서 대상까지 날아가는 시간.</summary>
        private const float ArrowFlightTime = 0.25f;

        /// <summary>같은 차원문에서 나온 화살이 겹쳐 보이지 않게 벌리는 거리(월드 단위).</summary>
        private const float ArrowSpread = 1.6f;

        private Action<EventContext> _normalHitHandler;
        private Action<EventContext> _cleanupHandler;

        public AmaterasuPortalBarrage(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "태양문 개방";
            Cooldown = 4;
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
            _normalHitHandler = OnNormalHit;
            _cleanupHandler = _ => ClearPortalHook();
            Caster.AddListener(BaseEnums.UnitEventType.OnNormalAttackHit, _normalHitHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.AddStatus(BuffStatus.Create(
                5750, "amaterasu_portals", CodeName,
                Caster, Caster, new MarkerBuffEffect(),
                duration: Duration,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: $"기본공격을 맞힐 때마다 아군 진영 허공에 차원문 {PortalCount}개가 열려 " +
                             $"화살 {PortalCount * ArrowsPerPortal}발을 한 번에 쏜다. 차원문마다 대상을 무작위로 " +
                             "정하고, 한 차원문의 화살은 같은 대상을 노린다. 화살 한 발은 그 일반공격 " +
                             $"최종 피해의 {ArrowDamageRatio:0.#}배를 추가공격으로 준다."));
            Caster.StartCoroutine(ExpirePortals());
            StopCode();
        }

        /// <summary>
        /// 기본공격이 적중하면 차원문 일제사격이 뒤따른다.
        ///
        /// 화살 피해는 그 일반공격이 실제로 뽑아낸 최종 피해에서 갈라 나온다.
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
        /// 궁극기가 열어 준 <b>추가공격</b>이다. 일반공격으로는 세지 않으므로
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
        }

        private IEnumerator ExpirePortals()
        {
            yield return new WaitForSeconds(Duration);
            ClearPortalHook();
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
            Caster.ultimateCooldown = Cooldown;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }
}
