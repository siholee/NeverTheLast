using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Negative;
using Effects.Vfx;
using Entities;

namespace Codes.Ultimate
{
    /// <summary>적 공용 궁극기 — 1000번대(공용 블록). 새 로스터가 들어오기 전까지 시험용 적이 쓴다.</summary>
    public static class EnemyBasicUltimateIds
    {
        public const int Smash = 1001;
        public const int Volley = 1002;
        public const int GroundBreaker = 1003;
        public const int VoidBreath = 1004;
    }

    /// <summary>「강타」 — 야수는 강화 할퀴기, 나머지는 제자리 중타격으로 공격력의 200% 피해를 준다.</summary>
    public sealed class EnemySmash : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Strike;

        private const float AttackRatio = 2.0f;

        public EnemySmash(UltimateCodeContext context) : base(context) { CodeName = "강타"; }

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.ContactAttack, AttackForm };
            Combat.AttackTargetResolution routing = PrepareTargets(new[] { PickPriorityEnemy() }, tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;
            Unit target = targets[0];
            DamageContext context = BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), tags);
            System.Action impact = () =>
            {
                Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                    Managers.SfxCuePhase.Hit, CodeName);
                target.TakeDamage(context);
            };
            if (EnemyNormalFx.UsesGroundRake(Caster))
                yield return EnemyNormalFx.BeastHeavyClaw(Caster, target, impact);
            else
                yield return EnemyNormalFx.UltimateSingle("enemy.ultimate.smash", Caster, target,
                    impact, 0.72f, 0.48f);
        }
    }

    /// <summary>「연사」 — 총구광과 대상 피격을 같은 리듬으로 세 번 잇달아 연다. 각 공격력의 60%.</summary>
    public sealed class EnemyVolley : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Shot;

        private const float AttackRatio = 0.6f;
        private const int Shots = 3;

        public EnemyVolley(UltimateCodeContext context) : base(context) { CodeName = "연사"; }

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, AttackForm };
            Combat.AttackTargetResolution routing = PrepareTargets(new[] { PickPriorityEnemy() }, tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;
            Unit lockedTarget = targets[0];

            System.Action BuildImpact(Unit target)
            {
                DamageContext context = BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio),
                    new List<int>(tags));
                return () =>
                {
                    Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                        Managers.SfxCuePhase.Hit, CodeName);
                    target.TakeDamage(context);
                };
            }

            yield return EnemyNormalFx.RepeatedUltimate("enemy.ultimate.volley", Caster, Shots, 0.12f,
                () => lockedTarget != null && lockedTarget.isActive ? lockedTarget : null, BuildImpact,
                repeatCastPerHit: true);
        }
    }

    /// <summary>「대지 분쇄」 — 제자리 반동과 대상 중심 충격으로 공격력의 180% 피해와 1턴 기절을 건다.</summary>
    public sealed class EnemyGroundBreaker : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Strike;

        private const float AttackRatio = 1.8f;

        public EnemyGroundBreaker(UltimateCodeContext context) : base(context) { CodeName = "대지 분쇄"; }

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.SingleTarget, DamageTag.UltAttack, DamageTag.ContactAttack, AttackForm };
            Combat.AttackTargetResolution routing = PrepareTargets(new[] { PickPriorityEnemy() }, tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;
            Unit target = targets[0];
            DamageContext context = BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio), tags);
            yield return EnemyNormalFx.UltimateSingle("enemy.ultimate.ground_breaker", Caster, target, () =>
            {
                Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                    Managers.SfxCuePhase.Hit, CodeName);
                target.TakeDamage(context);
                if (target.isActive) ControlStatuses.ApplyFixedStun(target, Caster, 1);
            }, 0.78f, 0.56f);
        }
    }

    /// <summary>「공허의 숨결」 — 한 번 집속하고 아군 전원 위치에 피격을 연다. 대상마다 공격력의 110%.</summary>
    public sealed class EnemyVoidBreath : BaseUltimateCode
    {
        public override int AttackForm => DamageTag.Spell;

        private const float AttackRatio = 1.1f;

        public EnemyVoidBreath(UltimateCodeContext context) : base(context) { CodeName = "공허의 숨결"; }

        protected override IEnumerator Perform()
        {
            var tags = new List<int> { DamageTag.AllTarget, DamageTag.UltAttack, DamageTag.NonContactAttack, AttackForm };
            Combat.AttackTargetResolution routing = PrepareTargets(AliveEnemies(), tags);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets)) yield break;

            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.2f, 0.45f, 0.3f));
            int targetIndex = 0;
            Unit PickNext() => targetIndex < targets.Count ? targets[targetIndex++] : null;
            System.Action BuildImpact(Unit target)
            {
                DamageContext context = BuildContext(StatDamage(0f, BaseEnums.StatType.Attack, AttackRatio),
                    new List<int>(tags));
                return () =>
                {
                    Managers.AudioManager.PlayAction(Caster, Managers.SfxActionSlot.Ultimate,
                        Managers.SfxCuePhase.Hit, CodeName);
                    target.TakeDamage(context);
                };
            }

            yield return EnemyNormalFx.RepeatedUltimate("enemy.ultimate.void_breath", Caster,
                targets.Count, 0.05f, PickNext, BuildImpact, 0.30f, 1.34f, 0.04f);
        }
    }
}
