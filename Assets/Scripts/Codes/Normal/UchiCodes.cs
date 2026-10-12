using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Normal
{
    /// <summary>
    /// 우치 — 일반행동 「분신술」(Detail_08 §10.3). 분신 1기를 만든다. 우치 자신은 때리지 않는다.
    /// 일반행동이므로 마나 +20을 받는다(행동 해결 알림).
    /// </summary>
    public sealed class UchiCloneArt : NormalCode
    {
        public UchiCloneArt(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            CodeName = "분신술";
        }

        public override bool HasValidTarget() => UchiCloneStrike.Enemies(Caster).Count > 0;

        public override void CastCode()
        {
            if (Caster == null) return;
            AudioManager.PlayAction(Caster, SfxActionSlot.Normal, SfxCuePhase.Cast, CodeName);
            Summons.Create(Caster);
            Caster.Invoke(BaseEnums.UnitEventType.OnNormalActionResolved, new EventContext(Caster));
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(ShowCreation());
        }

        private IEnumerator ShowCreation()
        {
            yield return new WaitForSeconds(.36f);
            if (Caster != null) Caster.isCasting = false;
        }
    }

    /// <summary>
    /// 분신의 일반행동(일반행동 네임스페이스 500 — 소환수 코드). 제 차례에 우치 공격력 120%로 한 번 치고 사라진다.
    /// 피해는 <b>우치 명의</b>(처치 · 마나 귀속)로 기록하고 소환수 피해 태그를 붙여 갈라테아 · 쿠베라 후원이 받게 한다.
    /// 분신은 우치의 스탯 · 버프를 물려받지 않지만, 피해 계산은 소환자 현재 스탯으로 한다(Detail_03 §4.7).
    /// </summary>
    public sealed class UchiCloneStrike : NormalCode
    {
        public const int CodeId = 500;
        private const float AttackRatio = 1.20f;

        public override int AttackForm => DamageTag.Strike;

        public UchiCloneStrike(NormalCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Normal;
            CodeName = "분신 일격";
        }

        public override bool HasValidTarget() => Caster != null && Caster.SummonOwner != null && Enemies(Caster.SummonOwner).Count > 0;

        public override void CastCode()
        {
            if (Caster == null) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(StrikeSequence());
        }

        private IEnumerator StrikeSequence()
        {
            yield return Effects.Vfx.UchiFx.PrepareStrike(Caster);
            StrikeAndVanish(Caster, 1f);
        }

        /// <summary>
        /// 분신이 우선도 적 하나를 치고 사라진다. 넘침은 <paramref name="ratioScale"/> 0.5로 부른다(평소의 절반).
        /// </summary>
        public static void StrikeAndVanish(Unit clone, float ratioScale)
        {
            if (clone == null || !clone.isActive) return;
            Unit owner = clone.SummonOwner;
            if (owner != null && owner.isActive)
            {
                Unit target = PickTarget(owner);
                if (target != null)
                {
                    if (ratioScale < 1f) clone.GetComponent<Entities.View.SummonCardView>()?.Overflow(target);
                    Effects.Vfx.UchiFx.Strike(clone, target, ratioScale);
                    HitAsOwner(owner, target, AttackRatio * ratioScale, StrikeTags());
                }
            }
            clone.Die(null);
        }

        private static List<int> StrikeTags() => new()
            { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.ContactAttack, DamageTag.Strike, DamageTag.SummonAttack };

        /// <summary>
        /// 소환자 명의 피해 한 방. 치명타도 소환자의 확률 · 피해로 굴린다. 지휘 일격이면 <paramref name="command"/>가
        /// 컨텍스트에 표지 · 최종 배율을 붙이고, 지휘가 이미 끝났으면 판정을 열지 않는다. 실제로 피해를 넣었으면 참.
        /// </summary>
        public static bool HitAsOwner(Unit owner, Unit target, float attackRatio, List<int> tags,
            BaseEnums.CodeType codeType = BaseEnums.CodeType.Normal, CommandedAttackScope command = null)
        {
            if (owner == null || target == null || !target.isActive || (command != null && !command.Active)) return false;
            bool crit = Random.value < owner.CritChanceCurr;
            float multiplier = crit ? owner.CritMultiplierCurr : 1f;
            int damage = Mathf.Max(1, Mathf.RoundToInt(owner.StatValue(BaseEnums.StatType.Attack) * attackRatio * multiplier));
            var context = new DamageContext(owner, damage, codeType, tags, crit);
            command?.Stamp(context);
            if (!CommandedAttackScope.TryAdmit(context)) return false;
            target.TakeDamage(context);
            return true;
        }

        // ── 나폴레옹 지휘(Detail_08 §20.3) — 분신도 일반 공격을 가진 아군이다 ──

        /// <summary>소환자가 서 있는 분신은 지휘에 응한다. 우치 자신(분신술)은 공격하지 않아 응하지 않는다.</summary>
        public override bool CanJoinCommand => Caster != null && Caster.SummonOwner != null && Caster.SummonOwner.isActive;

        protected override List<int> CommandDamageTags() => StrikeTags();

        public override int EstimateCommandedDamage(Unit target)
        {
            Unit owner = Caster?.SummonOwner;
            if (owner == null || target == null || !target.isActive) return 0;
            var probe = new DamageContext(owner,
                Mathf.Max(1, Mathf.RoundToInt(owner.StatValue(BaseEnums.StatType.Attack) * AttackRatio)),
                BaseEnums.CodeType.Normal, StrikeTags()) { OutgoingDamageMultiplier = CommandedAttackScope.FinalDamageRatio };
            return CommandedAttackScope.EstimateFinalDamage(probe, target);
        }

        /// <summary>
        /// 지휘 일격 — 고정 대상에게 평소와 같은 일격(소환자 명의 · 소환 태그 · 치명)을 한 번 넣고 실제 사망 판정으로 사라진다
        /// (아누비스 영혼 · 사망 이벤트 유지). 지휘가 판정 전에 취소되면 치지 않고 소모되지도 않는다.
        /// 분신 자신의 차례 · 행동치 · 일반행동 이벤트는 생기지 않는다.
        /// </summary>
        protected override IEnumerator CommandedDeliver(Unit target)
        {
            Unit clone = Caster;
            CommandedAttackScope command = CurrentCommand;
            Unit owner = clone != null ? clone.SummonOwner : null;
            if (clone == null || owner == null || command == null) yield break;
            // 부분 전진은 분신이 사라진다는 전제의 연출이라 쓰지 않는다. 취소돼 남는 분신이 앞에 걸려 있지 않게 한다.
            yield return new WaitForSeconds(0.24f);
            if (!command.Active || target == null || !target.isActive || !clone.isActive) yield break;
            Effects.Vfx.UchiFx.Strike(clone, target);
            HitAsOwner(owner, target, AttackRatio, StrikeTags(), command: command);
            clone.Die(null);
        }

        public static Unit PickTarget(Unit owner)
        {
            List<Unit> enemies = Enemies(owner);
            if (enemies.Count == 0) return null;
            int top = enemies.Max(unit => unit.Priority);
            List<Unit> best = enemies.Where(unit => unit.Priority == top).ToList();
            return best[Random.Range(0, best.Count)];
        }

        public static List<Unit> Enemies(Unit side)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || side == null) return new List<Unit>();
            List<Unit> pool = side.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField && !unit.IsUntargetable).ToList();
        }
    }
}
