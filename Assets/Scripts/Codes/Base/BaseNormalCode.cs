using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Base
{
    /// <summary>
    /// 모든 일반행동의 뼈대. 대상 선택 → 공지 → <b>전달</b>(돌진 또는 투사체) → 적중 → 마무리.
    ///
    /// 전달이 연출과 판정을 한데 묶는다. 피해는 <b>카드가 부딪히는 프레임</b>, 또는
    /// <b>투사체가 카드 중앙에 꽂히는 프레임</b>에 들어간다 — 하스스톤처럼 눈에 보이는 순간이 곧 판정이다.
    /// 하위 클래스는 피해 공식(<see cref="BaseDamage"/>)과 모양(<see cref="IsContact"/> ·
    /// <see cref="ProjectileFor"/>)만 정하면 된다.
    /// </summary>
    public abstract class BaseNormalCode : NormalCode
    {
        protected BaseNormalCode(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            CastingDelay = 0.1f;
        }

        /// <summary>카드가 직접 날아가 부딪히는 공격인가. 아니면 카드 중앙에서 투사체가 나간다.</summary>
        protected virtual bool IsContact => false;

        /// <summary>치명타 · 경감 전의 피해. 예) 55 + 방어력의 50% → <c>StatDamage(55, Defense, 0.5f)</c>.</summary>
        protected abstract int BaseDamage(Unit target);

        public override void CastCode()
        {
            // 지휘 범위는 지휘한 특수행동 하나에만 속한다. 비정상 종료로 남았어도 실제 일반행동으로 새지 않게 먼저 닫는다.
            EndStaleCommand();
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(SkillCoroutine());
        }

        protected override IEnumerator SkillCoroutine()
        {
            bool castCompleted = true;
            yield return WaitForCast(ok => castCompleted = ok);
            if (!castCompleted)
            {
                StopCode();
                yield break;
            }

            TargetUnits = SelectTarget();
            Combat.AttackTargetResolution routing = Combat.AttackTargetRouter.Prepare(Caster, TargetUnits,
                CodeType, GetDamageTags(), CodeName);
            yield return routing.ResolvePreAttack();
            TargetUnits = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, TargetUnits))
            {
                StopCode();
                yield break;
            }

            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);

            foreach (Unit target in TargetUnits.ToList())
            {
                if (target == null || !target.isActive) continue;
                yield return Deliver(target);
            }

            NotifyActionResolved();
            StopCode();
        }

        /// <summary>
        /// 한 대상에게 공격을 전달한다. 기본은 근접이면 돌진, 아니면 투사체 한 발이다.
        /// 여러 발 · 특수한 모양은 하위 클래스가 통째로 바꾼다.
        /// </summary>
        protected virtual IEnumerator Deliver(Unit target)
        {
            DamageContext context = BuildContext(target);
            if (IsContact)
            {
                Color accent = ElementPalette.ColorFor(Caster.Element);
                yield return CardMotion.Charge(Caster, target, (point, direction) =>
                {
                    ImpactFx.Slam(point, direction, accent, context.IsCrit ? 1.25f : 1f);
                    Hit(target, context);
                });
                yield break;
            }

            CardMotion.Throw(Caster, target);
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Projectile, CodeName);
            yield return ProjectileFx.Fly(Caster, target, ProjectileFor(target), () => Hit(target, context));
        }

        /// <summary>이 대상에게 날릴 투사체의 모양. 기본은 원소 색 빛구슬이다.</summary>
        protected virtual ProjectileStyle ProjectileFor(Unit target)
        {
            return ProjectileFx.ElementStyle(ElementPalette.Parse(Caster.Element));
        }

        /// <summary>
        /// 치명타를 굴려 피해 정보를 만든다. 원시 피해는 언제나 100%다 — 지휘 중이면 컨텍스트에 표지를 붙여
        /// 최종 배율만 <see cref="DamageContext.OutgoingDamageMultiplier"/>에 한 번 곱한다.
        /// </summary>
        protected DamageContext BuildContext(Unit target)
        {
            bool crit = RollCritical(target, out float multiplier);
            int damage = Mathf.Max(1, Mathf.RoundToInt(BaseDamage(target) * multiplier));
            var context = new DamageContext(Caster, damage, BaseEnums.CodeType.Normal, GetDamageTags(), crit);
            StampCommand(context);
            return context;
        }

        /// <summary>적중 — 피해를 넣고 적중 이벤트를 알린다.</summary>
        protected void Hit(Unit target, DamageContext context, bool playSfx = true)
        {
            if (target == null || !target.isActive) return;
            // 지휘가 취소 · 종료된 뒤 늦게 도착한 판정은 피해 · 적중 이벤트 · 후처리를 모두 열지 않는다.
            if (!Combat.CommandedAttackScope.TryAdmit(context)) return;
            if (playSfx)
                AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Hit, CodeName);
            target.TakeDamage(context);
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalAttackHit, new EventContext(Caster, target, context));
            OnAttackResolved(target, context);
        }

        /// <summary>적중 직후의 캐릭터별 후처리 훅(원소 부여 등).</summary>
        protected virtual void OnAttackResolved(Unit target, DamageContext context) { }

        /// <summary>
        /// 일반행동이 실제로 끝났음을 알린다. 마나 +20이 여기서 들어온다(<see cref="Unit.ManaPerNormalAction"/>).
        /// 시전만 하고 방해받은 경우에는 발행하지 않는다.
        /// </summary>
        protected void NotifyActionResolved()
            => Caster?.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));

        public override void StopCode()
        {
            base.StopCode();
            if (Caster != null) Caster.isCasting = false;
        }

        public override bool HasValidTarget() => GetAvailableEnemies().Count > 0;

        /// <summary>우선도가 가장 높은 적. 같은 우선도면 지금 노리던 대상을 계속 친다.</summary>
        protected virtual List<Unit> SelectTarget()
        {
            List<Unit> enemies = GetAvailableEnemies();
            if (enemies.Count == 0) return new List<Unit>();

            int maxPriority = enemies.Max(enemy => enemy.Priority);
            List<Unit> highest = enemies.Where(enemy => enemy.Priority == maxPriority).ToList();
            if (Caster.currentNormalTarget != null && Caster.currentNormalTarget.isActive &&
                highest.Contains(Caster.currentNormalTarget))
            {
                return new List<Unit> { Caster.currentNormalTarget };
            }

            Caster.currentNormalTarget = highest[Random.Range(0, highest.Count)];
            return new List<Unit> { Caster.currentNormalTarget };
        }

        protected List<Unit> GetAvailableEnemies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit && unit.IsOnField && !unit.IsUntargetable).ToList();
        }

        // ══════════════════════════════════════════════════════════
        // 지휘 사격 — NormalCode 계약의 BaseNormalCode 구현(나폴레옹, Detail_08 §20.3)
        // ══════════════════════════════════════════════════════════

        /// <summary>공격 형태가 있는 일반행동만 지휘에 응한다(회복 · 유틸 제외).</summary>
        public override bool CanJoinCommand => AttackForm > 0;

        /// <summary>피해 명의. 소환자 명의로 치는 소환수는 소환자를 돌려준다(추정 피해 · 치명 기준).</summary>
        protected virtual Unit DamageOwner => Caster;

        protected override List<int> CommandDamageTags() => GetDamageTags();

        /// <summary>기본은 이 코드의 <see cref="Deliver"/> 그대로라 다단 · 표식 · 원소 · 적중 후처리가 유지된다.</summary>
        protected override IEnumerator CommandedDeliver(Unit target) => Deliver(target);

        /// <summary>추정용 탐침을 실제 전달과 같은 모양으로 맞춘다(수리야 종언탄의 방어 인정 배율 등).</summary>
        protected virtual void PrepareEstimateProbe(Unit target, DamageContext probe) { }

        /// <summary>
        /// 지휘 공격 한 번의 결정적 추정 피해(치명 없음). 실제와 같은 원시 피해 · 태그 · 방어 인정에 최종 배율을 곱한
        /// 탐침을 공통 추정식(<see cref="Combat.CommandedAttackScope.EstimateFinalDamage"/>)에 넣는다.
        /// </summary>
        public override int EstimateCommandedDamage(Unit target)
        {
            Unit owner = DamageOwner;
            if (owner == null || target == null || !target.isActive) return 0;
            var probe = new DamageContext(owner, Mathf.Max(1, BaseDamage(target)), BaseEnums.CodeType.Normal, GetDamageTags())
            {
                OutgoingDamageMultiplier = Combat.CommandedAttackScope.FinalDamageRatio,
            };
            PrepareEstimateProbe(target, probe);
            return Combat.CommandedAttackScope.EstimateFinalDamage(probe, target);
        }

        protected virtual List<int> GetDamageTags()
        {
            var tags = new List<int>
            {
                DamageTag.SingleTarget, DamageTag.NormalAttack,
                IsContact ? DamageTag.ContactAttack : DamageTag.NonContactAttack,
            };
            if (AttackForm > 0) tags.Add(AttackForm);
            return tags;
        }
    }
}
