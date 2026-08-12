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

namespace Codes.Ultimate
{
    public sealed class AsclepiusFlask : UltimateCode
    {
        public AsclepiusFlask(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "의신의 영약";
            Cooldown = 8f;
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

        private void FireProjectile(Unit target, float delay)
        {
            if (GameManager.Instance?.sfxManager?.ProjectilePrefabs != null &&
                GameManager.Instance.sfxManager.ProjectilePrefabs.TryGetValue("FireBlast", out var prefab))
                GameManager.Instance.sfxManager.FireSingleProjectile(prefab, Caster, target, delay);
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
        private const float Duration = 6f;

        /// <summary>
        /// 화살 한 발의 위력. 8발 합계가 일반공격(위력 70) 한 대와 비슷해지도록 잡았다.
        /// 궁 지속 중 평타가 대략 두 배가 되는 셈이다.
        /// </summary>
        private const int ArrowPower = 8;
        private Action<EventContext> _normalHitHandler;
        private Action<EventContext> _cleanupHandler;

        public AmaterasuPortalBarrage(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "태양문 개방";
            Cooldown = 8f;
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
            _normalHitHandler = _ => Caster.StartCoroutine(FirePortalVolley());
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
                description: "기본공격마다 차원문 4개가 열린다. 차원문마다 대상을 무작위로 정하고, 한 차원문의 화살 2발은 같은 대상을 노린다."));
            Caster.StartCoroutine(ExpirePortals());
            StopCode();
        }

        private IEnumerator FirePortalVolley()
        {
            List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable).ToList();
            if (enemies.Count == 0) yield break;

            for (int portal = 0; portal < 4; portal++)
            {
                Unit target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
                for (int arrow = 0; arrow < 2; arrow++) FireProjectile(target, 0.2f);
                Caster.StartCoroutine(ResolvePortalPair(target));
            }
            yield return null;
        }

        private IEnumerator ResolvePortalPair(Unit target)
        {
            yield return new WaitForSeconds(0.2f);
            for (int arrow = 0; arrow < 2; arrow++)
            {
                if (target == null || !target.isActive || target.IsUntargetable)
                    target = global::Target.GetAllEnemies(Caster)
                        .FirstOrDefault(unit => unit != null && unit.isActive && !unit.IsUntargetable);
                if (target == null) yield break;
                bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
                int damage = Mathf.Max(1, Mathf.RoundToInt(
                    Caster.SkillDamage(ArrowPower, BaseEnums.PrimaryStat.DEX) *
                    (isCrit ? Caster.CritMultiplierCurr : 1f)));
                target.TakeDamage(new DamageContext(
                    Caster, damage, BaseEnums.CodeType.Ultimate,
                    new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.Physical, DamageTag.NonContactAttack },
                    isCrit));
            }
        }

        private IEnumerator ExpirePortals()
        {
            yield return new WaitForSeconds(Duration);
            ClearPortalHook();
        }

        private void FireProjectile(Unit target, float delay)
        {
            if (GameManager.Instance?.sfxManager?.ProjectilePrefabs != null &&
                GameManager.Instance.sfxManager.ProjectilePrefabs.TryGetValue("FireBlast", out var prefab))
                GameManager.Instance.sfxManager.FireSingleProjectile(prefab, Caster, target, delay);
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
