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
    /// 찬드라 — 특수행동 「창야」. 바유 다음, 다른 로카팔라보다 먼저 나선다.
    /// 묶음이 끝날 때까지 아군 전체에게 치명타 확률 25%를 주고 최대 체력 100%의 광역 피해를 가한다.
    /// </summary>
    public sealed class ChandraChangya : SpecialCode
    {
        private const float MaxHpRatio = 1f;
        private const float HealRatio = 0.20f;
        private const float CritRateBonus = 0.25f;
        private const int CritStatusId = 7062;
        private const string CritStatusKey = "chandra_changya_crit";

        public ChandraChangya(SpecialCodeContext context) : base(context)
        {
            CodeName = "창야";
            CastingDelay = 0.10f;
        }

        public override int OpenOrder => -50;
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
                // 수렴이 끝나 성역이 닫힐 때 월광과 묶음 버프를 실제 적용한다.
                Caster.ActivePassiveCodes.OfType<ChandraNishakara>().FirstOrDefault()
                    ?.GainMoonlight(playVfx: false);
                ApplyBatchCritBuff();
                foreach (Unit target in targets)
                {
                    bool crit = RollCritical(out float multiplier);
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

            Caster.Heal(Mathf.RoundToInt(totalDamage * HealRatio), Caster);
            yield return new WaitForSeconds(0.10f);
            StopCode();
        }

        private void ApplyBatchCritBuff()
        {
            List<Unit> allies = Allies();
            foreach (Unit ally in allies)
            {
                var status = BuffStatus.Create(
                    CritStatusId, CritStatusKey, "창야의 월광", Caster, ally,
                    new StatModifierEffect(BaseEnums.StatType.CritRate, CritRateBonus),
                    duration: 0,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "로카팔라 특수행동이 끝날 때까지 치명타 확률이 25% 증가합니다.");
                // ChandraFx가 묶음 부여 직후 caster 색 affected를 재생해 중복을 막는다.
                status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
                ally.AddStatus(status);
            }

            Combat.SpecialAction.RegisterBatchCleanup(() =>
            {
                foreach (Unit ally in allies.Where(unit => unit != null)) ally.RemoveStatusByKey(CritStatusKey);
            });
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
