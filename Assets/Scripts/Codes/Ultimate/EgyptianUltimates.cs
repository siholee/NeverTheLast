using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Effects.Negative;
using Effects.Projectiles;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>호루스 U — 우제트. 실제 4타 판정은 HorusNormalAttack이 스택 게이지와 함께 처리한다.</summary>
    public sealed class HorusWadjet : UltimateCode
    {
        public override bool IsAutoCast => false;
        public HorusWadjet(UltimateCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Ultimate; CodeName = "우제트"; CastingDelay = 0f; }
        public override bool HasValidTarget() => false;
    }

    /// <summary>아누비스 U — 적 전열(비어 있으면 후열) 전체를 바위 창으로 꿰뚫고 전열 아군을 보호한다.</summary>
    public sealed class AnubisStoneSpear : UltimateCode
    {
        public AnubisStoneSpear(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "저승의 바위 창";
            CastingDelay = 0.55f;
            Power = 80;
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
            if (Caster == null || !Caster.isActive || Caster.isControlled)
            { StopCode(); yield break; }

            List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable && unit.currentCell != null)
                .ToList();
            if (enemies.Count == 0) { StopCode(); yield break; }

            GridManager grid = GridManager.Instance;
            int front = grid != null ? grid.GetFrontColumn(enemies[0].IsEnemy) : (enemies[0].IsEnemy ? 1 : -1);
            int rear = grid != null ? grid.GetRearColumn(enemies[0].IsEnemy) : (enemies[0].IsEnemy ? 2 : -2);
            List<Unit> targets = enemies.Where(unit => unit.currentCell.xPos == front).ToList();
            if (targets.Count == 0) targets = enemies.Where(unit => unit.currentCell.xPos == rear).ToList();
            if (targets.Count == 0) targets = enemies;

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(80, BaseEnums.PrimaryStat.STR) * crit));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.MultiTarget, DamageTag.UltAttack,
                    DamageTag.Special, DamageTag.NonContactAttack,
                }, isCrit);

            var token = new ProjectileImpactToken();
            foreach (Unit target in targets)
            {
                GameManager.Instance?.sfxManager?.FireElementalProjectile(
                    Caster, target, 0.42f, ProjectilePathType.Linear,
                    ProjectileFlight.DataFor(ProjectilePathType.Linear), null,
                    token.MarkImpact, context);
            }
            yield return ProjectileFlight.WaitForImpact(token, 0.42f);
            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive).ToList())
                target.TakeDamage(context);

            int ownFront = grid != null ? grid.GetFrontColumn(Caster.IsEnemy) : (Caster.IsEnemy ? 1 : -1);
            if (Caster.currentCell != null && Caster.currentCell.xPos == ownFront)
            {
                int shield = Mathf.Max(1, Caster.SkillDamage(140, BaseEnums.PrimaryStat.CON));
                foreach (Unit ally in global::Target.GetAllAllies(Caster)
                             .Where(unit => unit != null && unit.isActive && unit.currentCell != null &&
                                            unit.currentCell.xPos == ownFront))
                {
                    ally.AddShield(shield, Caster);
                }
            }

            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }

    /// <summary>바스테트 U — 반격을 보유한 모든 아군의 추가행동·반격을 추격으로 연결한다.</summary>
    public sealed class BastetApexExecution : UltimateCode
    {
        private readonly HashSet<Unit> _linkedAllies = new();
        private System.Action<DamageResolvedContext> _damageHandler;
        private int _lastTriggeredAction = int.MinValue;
        private bool _registered;
        private bool _resolving;

        public override bool IsAutoCast => false;
        public BastetApexExecution(UltimateCodeContext context) : base(context)
        { CodeType = BaseEnums.CodeType.Ultimate; CodeName = "야수의 추격"; CastingDelay = 0f; }

        public void StartPassive()
        {
            if (Caster == null || _registered) return;
            foreach (Unit ally in global::Target.GetAllAllies(Caster)
                .Where(unit => unit != null && unit.isActive && unit.currentCell != null && unit.currentCell.yPos > 0)
                .Where(HasCounterAttack))
            {
                _linkedAllies.Add(ally);
            }
            if (_linkedAllies.Count == 0) return;

            _lastTriggeredAction = int.MinValue;
            _damageHandler = OnAnyDamageDealt;
            Unit.AnyDamageDealt += _damageHandler;
            _registered = true;
        }

        public void StopPassive()
        {
            if (!_registered) return;
            Unit.AnyDamageDealt -= _damageHandler;
            _linkedAllies.Clear();
            _damageHandler = null;
            _registered = false;
            _resolving = false;
        }

        public override void StopCode() => StopPassive();

        private static bool HasCounterAttack(Unit unit)
            => unit != null && unit.ActivePassiveCodes.Concat(unit.ActiveItemPassiveCodes)
                .Any(code => code is ICounterAttackProvider);

        private void OnAnyDamageDealt(DamageResolvedContext context)
        {
            List<int> tags = context?.DamageContext?.DamageTags;
            if (_resolving || Caster == null || !Caster.isActive ||
                context?.Attacker == null || !_linkedAllies.Contains(context.Attacker) ||
                !context.Attacker.isActive || context.DamageDealt <= 0 ||
                tags == null ||
                (!tags.Contains(DamageTag.AdditionalAttack) && !tags.Contains(DamageTag.CounterAttack))) return;

            int action = GameManager.Instance?.ActionScheduler?.CurrentActionId ?? 0;
            if (_lastTriggeredAction == action) return;
            _lastTriggeredAction = action;
            GameManager.Instance?.ActionScheduler?.EnqueueAdditional(
                Caster, "bastet_beast_pursuit", CodeName, ResolvePursuit);
        }

        private void ResolvePursuit()
        {
            if (Caster == null || !Caster.isActive) return;
            List<Unit> enemies = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .ToList();
            if (enemies.Count == 0) return;

            bool isCrit = UnityEngine.Random.value <= Caster.CritChanceCurr;
            int pursuitPower = 47 + Mathf.RoundToInt(
                Caster.GetBaseDex() * 4f * (Mathf.Min(enemies.Count, 4) - 1));
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(pursuitPower, BaseEnums.PrimaryStat.DEX) *
                (isCrit ? Caster.CritMultiplierCurr : 1f)));
            _resolving = true;
            try
            {
                foreach (Unit enemy in enemies)
                {
                    enemy.TakeDamage(new DamageContext(
                        Caster, damage, BaseEnums.CodeType.Ultimate,
                        new List<int>
                        {
                            DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.AdditionalAttack,
                            DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
                        },
                        isCrit));
                }
            }
            finally
            {
                _resolving = false;
            }
        }

        public override bool HasValidTarget() => false;
    }

    /// <summary>세트 U — STR×1.2 참격 후 CON 명중 판정으로 3턴 화상.</summary>
    public sealed class SetBurningSlash : UltimateCode
    {
        public SetBurningSlash(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "화염 참격";
            CastingDelay = 0.5f;
            Power = 120;
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
            if (Caster == null || !Caster.isActive || Caster.isControlled)
            { StopCode(); yield break; }

            Unit target = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .OrderByDescending(unit => unit.Priority)
                .FirstOrDefault();
            if (target == null) { StopCode(); yield break; }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(120, BaseEnums.PrimaryStat.STR) * crit));
            var damageContext = new DamageContext(
                Caster, damage, BaseEnums.CodeType.Ultimate,
                new List<int>
                {
                    DamageTag.SingleTarget, DamageTag.UltAttack,
                    DamageTag.Physical, DamageTag.ContactAttack, DamageTag.Slash,
                }, isCrit);

            GameManager.Instance?.sfxManager?.TryPlayMeleeAttack(Caster, target, damageContext);
            yield return new WaitForSeconds(SfxManager.MeleeImpactDelay);
            if (target != null && target.isActive)
            {
                target.TakeDamage(damageContext);
                if (target.isActive && !ElementalReaction.TryApplyBurn(Caster, target, 3))
                {
                    Debug.Log($"[화염 참격] {target.UnitName}이(가) 화상에 저항했습니다.");
                }
            }
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive &&
            global::Target.GetAllEnemies(Caster).Any(unit => unit != null && unit.isActive && !unit.IsUntargetable);
    }

    /// <summary>토트 U — 3턴간 아군 전체가 감소시킨 강인도의 30%를 실제 피해로 더한다.</summary>
    public sealed class ThothEyeOfWisdom : UltimateCode
    {
        public ThothEyeOfWisdom(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "지혜의 눈";
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
            if (Caster == null || !Caster.isActive || Caster.isControlled)
            { StopCode(); yield break; }

            foreach (Unit ally in global::Target.GetAllAllies(Caster)
                         .Where(unit => unit != null && unit.isActive))
            {
                ally.AddStatus(BuffStatus.Create(
                    EgyptianStatusIds.ThothEyeOfWisdom, $"thoth_eye_of_wisdom_{Caster.GetEntityId()}", CodeName,
                    Caster, ally, new ToughnessEchoEffect(0.30f),
                    duration: 3,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "감소시킨 강인도의 30%를 실제 피해로 더합니다."));
            }
            StopCode();
        }

        public override void StopCode()
        {
            if (Caster == null) return;
            Caster.isCasting = false;
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
    }

    /// <summary>이시스 U — 적 전체 3연타, 바위 부착, 행동 게이지 50% 지연.</summary>
    public sealed class IsisDesertDeluge : UltimateCode
    {
        public IsisDesertDeluge(UltimateCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Ultimate;
            CodeName = "사막의 격류";
            CastingDelay = 0.55f;
            Power = 40;
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
            if (Caster == null || !Caster.isActive || Caster.isControlled)
            { StopCode(); yield break; }

            List<Unit> targets = global::Target.GetAllEnemies(Caster)
                .Where(unit => unit != null && unit.isActive && !unit.IsUntargetable)
                .ToList();
            if (targets.Count == 0) { StopCode(); yield break; }

            bool isCrit = Random.value <= Caster.CritChanceCurr;
            float crit = isCrit ? Caster.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                Caster.SkillDamage(40, BaseEnums.PrimaryStat.INT) * crit));

            for (int hit = 0; hit < 3; hit++)
            {
                foreach (Unit target in targets.Where(unit => unit != null && unit.isActive).ToList())
                {
                    target.TakeDamage(new DamageContext(
                        Caster, damage, BaseEnums.CodeType.Ultimate,
                        new List<int>
                        {
                            DamageTag.AllTarget, DamageTag.UltAttack,
                            DamageTag.Special, DamageTag.NonContactAttack,
                        }, isCrit));
                }
                if (hit < 2) yield return new WaitForSeconds(0.14f);
            }

            foreach (Unit target in targets.Where(unit => unit != null && unit.isActive).ToList())
            {
                target.GrantCombatElement(BaseEnums.UnitElement.Geo, Unit.CommonElementAuraDuration, Caster);
                GameManager.Instance?.ActionScheduler?.DelayAction(target, 0.5f);
            }
            StopCode();
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
