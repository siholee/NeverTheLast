using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Special
{
    /// <summary>
    /// 찬드라 — 특수행동 「창야」. 회복만 맡으므로 로카팔라 묶음의 <b>맨 끝</b>(+100)에 선다(Detail_08 §3.7).
    /// 최대 체력 100%의 광역 피해를 가하고, 실제로 깎은 피해의 20%를 살아 있는 아군 전원에게 똑같이 나눠 회복한다.
    /// 특수행동 피해 보조는 인드라의 몫이라 예전의 묶음 치명타 버프는 없앴다.
    /// </summary>
    public sealed class ChandraChangya : SpecialCode
    {
        public override int AttackForm => DamageTag.Spell;

        private const float MaxHpRatio = 1f;
        private const float HealRatio = 0.20f;

        public ChandraChangya(SpecialCodeContext context) : base(context)
        {
            CodeName = "창야";
            CastingDelay = 0.10f;
        }

        public override int OpenOrder => 100;
        public override bool HasValidTarget() => Enemies().Count > 0;

        public override void CastCode()
        {
            if (!HasValidTarget())
            {
                StopCode();
                return;
            }

            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Perform());
        }

        private IEnumerator Perform()
        {
            AudioManager.PlayAction(Caster, SfxActionSlot.Special, SfxCuePhase.Cast, CodeName);

            List<Unit> allies = Allies();
            var attackTags = new List<int>
            {
                DamageTag.AllTarget,
                DamageTag.SpecialAttack,
                DamageTag.NonContactAttack,
                DamageTag.SpecialDamage,
                DamageTag.Spell,
            };
            Combat.AttackTargetResolution routing = Combat.AttackTargetRouter.Prepare(Caster, Enemies(),
                BaseEnums.CodeType.Special, attackTags, CodeName);
            yield return routing.ResolvePreAttack();
            List<Unit> targets = routing.Targets;
            if (!routing.CanContinue || !Combat.Targeting.Announce(Caster, targets))
            {
                StopCode();
                yield break;
            }
            var contexts = new Dictionary<Unit, DamageContext>();

            int totalDamage = 0;
            yield return ChandraFx.Changya(Caster, allies, targets, () =>
            {
                // 시전 정점의 월광 획득·피해 준비 순서를 유지한다. 예전 묶음 치명타 버프는 없다.
                Caster.ActivePassiveCodes.OfType<ChandraNishakara>().FirstOrDefault()
                    ?.GainMoonlight(playVfx: false);
                foreach (Unit target in targets)
                {
                    bool crit = RollCritical(target, out float multiplier);
                    int damage = Mathf.Max(1, Mathf.RoundToInt(
                        Caster.StatValue(BaseEnums.StatType.MaxHp) * MaxHpRatio * multiplier));
                    contexts[target] = new DamageContext(Caster, damage, BaseEnums.CodeType.Special,
                        new List<int>(attackTags), crit);
                }
            }, target =>
            {
                if (target == null || !target.isActive || !contexts.TryGetValue(target, out DamageContext context)) return;
                target.TakeDamage(context);
                totalDamage += context.ResolvedDamage;
            });

            // 회복 몫을 살아 있는 아군 전원에게 똑같이 나눈다 — 찬드라가 묶음의 유지력을 맡는다.
            List<Unit> survivors = Allies().Where(unit => unit != null && unit.isActive).ToList();
            if (survivors.Count > 0 && totalDamage > 0)
            {
                int share = Mathf.Max(1, Mathf.RoundToInt(totalDamage * HealRatio / survivors.Count));
                VfxComposer.PlayScreen("chandra.changya", Caster, survivors);
                foreach (Unit ally in survivors)
                {
                    using (StatusAffectedVfx.SupportVariant("chandra.changya"))
                        ally.Heal(share, Caster);
                }
                AudioManager.PlayAction(Caster, SfxActionSlot.Special, SfxCuePhase.Resolve, CodeName);
            }
            // 회복광이 끝날 때까지 기존 isCasting을 유지해 다음 특수행동과 표시 길이를 맞춘다.
            yield return new WaitForSeconds(0.50f);
            StopCode();
        }

        private List<Unit> Enemies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.heroList : grid.enemyList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField && !unit.IsUntargetable).ToList();
        }

        private List<Unit> Allies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList();
        }
    }
}
